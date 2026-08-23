using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
using NewDynamicFormGenAPI.Models.Interfaces;
using NewDynamicFormGenAPI.Models.DTOs.Rules;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Enums;


namespace FormGen.Application.Services
{
    /// <summary>
    /// Server-side implementation of the Rule Engine described in ARCHITECTURE.md §5.
    /// This is the authoritative validator — it is invoked again by SubmissionService even
    /// though the client already validated, because client-side checks can be bypassed.
    /// </summary>
    public class RuleEngineService : IRuleEngineService
    {
        private readonly IUnitOfWork _uow;

        public RuleEngineService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<List<FormRuleDto>> GetRulesForVersionAsync(int formVersionId)
        {
            var rules = _uow.Repository<FormRule>().Query()
                .Where(r => r.FormVersionId == formVersionId && r.IsActive)
                .OrderBy(r => r.DisplayOrder)
                .ToList();

            var controlIds = rules.Select(r => r.ControlId).Distinct().ToList();
            var controlKeys = _uow.Repository<FormControl>().Query()
                .Where(c => controlIds.Contains(c.ControlId))
                .ToDictionary(c => c.ControlId, c => c.ControlKey);

            return rules.Select(r => Map(r, controlKeys.GetValueOrDefault(r.ControlId, ""))).ToList();
        }

        public async Task<FormRuleDto> AddRuleAsync(int formVersionId, CreateFormRuleDto dto)
        {
            var entity = new FormRule
            {
                FormVersionId = formVersionId,
                ControlId = dto.ControlId,
                RuleType = dto.RuleType,
                RuleDetailsJson = dto.RuleDetailsJson,
                ErrorMessage = dto.ErrorMessage,
                Severity = string.IsNullOrWhiteSpace(dto.Severity) ? RuleSeverity.Error : dto.Severity,
                DisplayOrder = dto.DisplayOrder,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };
            await _uow.Repository<FormRule>().AddAsync(entity);
            await _uow.SaveChangesAsync();

            var control = await _uow.Repository<FormControl>().GetByIdAsync(dto.ControlId);
            return Map(entity, control?.ControlKey ?? "");
        }

        public async Task UpdateRuleAsync(int ruleId, CreateFormRuleDto dto)
        {
            var repo = _uow.Repository<FormRule>();
            var entity = await repo.GetByIdAsync(ruleId)
                ?? throw new KeyNotFoundException($"Rule {ruleId} not found");

            entity.ControlId = dto.ControlId;
            entity.RuleType = dto.RuleType;
            entity.RuleDetailsJson = dto.RuleDetailsJson;
            entity.ErrorMessage = dto.ErrorMessage;
            entity.Severity = dto.Severity;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.ModifiedDate = DateTime.UtcNow;

            repo.Update(entity);
            await _uow.SaveChangesAsync();
        }

        public async Task DeleteRuleAsync(int ruleId)
        {
            var repo = _uow.Repository<FormRule>();
            var entity = await repo.GetByIdAsync(ruleId);
            if (entity == null) return;
            repo.Remove(entity);
            await _uow.SaveChangesAsync();
        }

        /// <summary>
        /// Pure evaluation — same semantics as the Angular RuleEngineService's ValidatorFns.
        /// Keep both implementations' switch cases in lockstep when adding a new RuleType.
        /// </summary>
        public RuleEvaluationResultDto Evaluate(
            List<FormRuleDto> rules,
            IReadOnlyDictionary<string, object?> submittedValues,
            IReadOnlyDictionary<int, string> controlKeysById)
        {
            var result = new RuleEvaluationResultDto { IsValid = true };

            foreach (var rule in rules.Where(r => r.IsActive).OrderBy(r => r.DisplayOrder))
            {
                // Visibility rules are UI-only (show/hide) — they never gate submission.
                // The client is the source of truth for what was actually visible when submitted.
                if (rule.RuleType == RuleType.Visibility)
                    continue;

                var key = rule.ControlKey;
                submittedValues.TryGetValue(key, out var rawValue);
                var stringValue = rawValue?.ToString() ?? string.Empty;

                bool passed = rule.RuleType switch
                {
                    RuleType.Required => !string.IsNullOrWhiteSpace(stringValue),
                    RuleType.MinLength => EvaluateMinLength(rule, stringValue),
                    RuleType.MaxLength => EvaluateMaxLength(rule, stringValue),
                    RuleType.Regex => EvaluateRegex(rule, stringValue),
                    RuleType.Range => EvaluateRange(rule, stringValue),
                    RuleType.Email => EvaluateEmail(stringValue),
                    RuleType.Date => EvaluateDate(rule, stringValue),
                    RuleType.CrossField => EvaluateCrossField(rule, stringValue, submittedValues),
                    RuleType.Custom => true, // Custom rules are opt-in server hooks; default to pass, wire up as needed
                    _ => true
                };

                // Empty + optional: skip non-Required checks on empty values (mirrors client behavior)
                if (rule.RuleType != RuleType.Required && string.IsNullOrWhiteSpace(stringValue))
                    continue;

                if (!passed)
                {
                    result.Failures.Add(new RuleFailureDto
                    {
                        ControlId = rule.ControlId,
                        ControlKey = key,
                        RuleType = rule.RuleType,
                        ErrorMessage = rule.ErrorMessage,
                        Severity = rule.Severity
                    });

                    if (rule.Severity == RuleSeverity.Error)
                        result.IsValid = false;
                }
            }

            return result;
        }

        // ---- individual rule evaluators ----

        private static bool EvaluateMinLength(FormRuleDto rule, string value)
        {
            var min = GetInt(rule.RuleDetailsJson, "min") ?? 0;
            return value.Length >= min;
        }

        private static bool EvaluateMaxLength(FormRuleDto rule, string value)
        {
            var max = GetInt(rule.RuleDetailsJson, "max") ?? int.MaxValue;
            return value.Length <= max;
        }

        private static bool EvaluateRegex(FormRuleDto rule, string value)
        {
            var pattern = GetString(rule.RuleDetailsJson, "pattern");
            if (string.IsNullOrEmpty(pattern)) return true;
            return Regex.IsMatch(value, pattern);
        }

        private static bool EvaluateRange(FormRuleDto rule, string value)
        {
            if (!double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
                return false;
            var min = GetDouble(rule.RuleDetailsJson, "min") ?? double.MinValue;
            var max = GetDouble(rule.RuleDetailsJson, "max") ?? double.MaxValue;
            return num >= min && num <= max;
        }

        private static bool EvaluateEmail(string value)
        {
            const string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(value, pattern);
        }

        private static bool EvaluateDate(FormRuleDto rule, string value)
        {
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return false;

            var operatorStr = GetString(rule.RuleDetailsJson, "operator"); // e.g. "<=Today", ">=Today"
            if (string.IsNullOrEmpty(operatorStr)) return true;

            return operatorStr switch
            {
                "<=Today" => date.Date <= DateTime.UtcNow.Date,
                ">=Today" => date.Date >= DateTime.UtcNow.Date,
                "<Today" => date.Date < DateTime.UtcNow.Date,
                ">Today" => date.Date > DateTime.UtcNow.Date,
                _ => true
            };
        }

        private static bool EvaluateCrossField(FormRuleDto rule, string value,
            IReadOnlyDictionary<string, object?> submittedValues)
        {
            var compareKey = GetString(rule.RuleDetailsJson, "compareControlKey");
            var op = GetString(rule.RuleDetailsJson, "operator") ?? "==";
            if (string.IsNullOrEmpty(compareKey)) return true;

            submittedValues.TryGetValue(compareKey, out var compareRaw);
            var compareValue = compareRaw?.ToString() ?? string.Empty;

            // try numeric compare first, fall back to string/date compare
            if (double.TryParse(value, out var a) && double.TryParse(compareValue, out var b))
            {
                return op switch
                {
                    "==" => a == b,
                    "!=" => a != b,
                    "<" => a < b,
                    "<=" => a <= b,
                    ">" => a > b,
                    ">=" => a >= b,
                    _ => true
                };
            }

            if (DateTime.TryParse(value, out var da) && DateTime.TryParse(compareValue, out var db))
            {
                return op switch
                {
                    "==" => da == db,
                    "!=" => da != db,
                    "<" => da < db,
                    "<=" => da <= db,
                    ">" => da > db,
                    ">=" => da >= db,
                    _ => true
                };
            }

            return op == "==" ? value == compareValue : value != compareValue;
        }

        // ---- RuleDetailsJson helpers ----

        private static string? GetString(string? json, string prop)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(prop, out var el) ? el.GetString() : null;
        }

        private static int? GetInt(string? json, string prop)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(prop, out var el) && el.TryGetInt32(out var v) ? v : null;
        }

        private static double? GetDouble(string? json, string prop)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(prop, out var el) && el.TryGetDouble(out var v) ? v : null;
        }

        private static FormRuleDto Map(FormRule r, string controlKey) => new()
        {
            RuleId = r.RuleId,
            FormVersionId = r.FormVersionId,
            ControlId = r.ControlId,
            ControlKey = controlKey,
            RuleType = r.RuleType,
            RuleDetailsJson = r.RuleDetailsJson,
            ErrorMessage = r.ErrorMessage,
            Severity = r.Severity,
            DisplayOrder = r.DisplayOrder,
            IsActive = r.IsActive
        };
    }
}
