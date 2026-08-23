using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FormGen.Application.Common;
using FormGen.Application.DTOs.Submissions;
using FormGen.Application.Interfaces;
using FormGen.Domain.Entities;

namespace FormGen.Application.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly IUnitOfWork _uow;
        private readonly IRuleEngineService _ruleEngine;

        public SubmissionService(IUnitOfWork uow, IRuleEngineService ruleEngine)
        {
            _uow = uow;
            _ruleEngine = ruleEngine;
        }

        public async Task<Result<int>> SubmitAsync(SubmitFormDto dto)
        {
            // 1. Load controls for this version, to map ControlKey <-> ControlId
            var controls = _uow.Repository<FormControl>().Query()
                .Where(c => c.FormVersionId == dto.FormVersionId)
                .ToList();

            var controlIdByKey = controls.ToDictionary(c => c.ControlKey, c => c.ControlId);
            var controlKeyById = controls.ToDictionary(c => c.ControlId, c => c.ControlKey);

            // 2. Load active rules and re-validate server-side — this is the actual gate.
            //    The client already validated for UX; we do not trust it.
            var rules = await _ruleEngine.GetRulesForVersionAsync(dto.FormVersionId);
            var evaluation = _ruleEngine.Evaluate(rules, dto.Values, controlKeyById);

            if (!evaluation.IsValid)
            {
                return Result<int>.Fail(
                    "Validation failed.",
                    evaluation.Failures.Select(f => $"{f.ControlKey}: {f.ErrorMessage}").ToList());
            }

            // 3. Persist submission (raw JSON snapshot + normalized per-field rows)
            var submission = new FormSubmission
            {
                FormId = dto.FormId,
                FormVersionId = dto.FormVersionId,
                SubmittedOn = DateTime.UtcNow,
                JsonData = JsonSerializer.Serialize(dto.Values)
            };

            await _uow.Repository<FormSubmission>().AddAsync(submission);
            await _uow.SaveChangesAsync(); // need SubmissionId for child rows

            foreach (var kvp in dto.Values)
            {
                if (!controlIdByKey.TryGetValue(kvp.Key, out var controlId)) continue;

                await _uow.Repository<FormSubmissionValue>().AddAsync(new FormSubmissionValue
                {
                    SubmissionId = submission.SubmissionId,
                    ControlId = controlId,
                    Value = kvp.Value?.ToString()
                });
            }

            await _uow.SaveChangesAsync();

            return Result<int>.Ok(submission.SubmissionId, "Submitted successfully.");
        }

        public async Task<PagedResult<SubmissionListItemDto>> GetSubmissionsAsync(int formId, int page, int pageSize)
        {
            var query = _uow.Repository<FormSubmission>().Query()
                .Where(s => s.FormId == formId)
                .OrderByDescending(s => s.SubmittedOn);

            var total = query.Count();
            var items = query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new SubmissionListItemDto
                {
                    SubmissionId = s.SubmissionId,
                    FormId = s.FormId,
                    SubmittedOn = s.SubmittedOn
                }).ToList();

            return new PagedResult<SubmissionListItemDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        public async Task<Result<SubmissionDetailDto>> GetDetailAsync(int submissionId)
        {
            var submission = await _uow.Repository<FormSubmission>().GetByIdAsync(submissionId);
            if (submission == null)
                return Result<SubmissionDetailDto>.Fail("Submission not found.");

            var form = await _uow.Repository<Form>().GetByIdAsync(submission.FormId);
            var version = _uow.Repository<FormVersion>().Query()
                .FirstOrDefault(v => v.FormVersionId == submission.FormVersionId);

            var values = string.IsNullOrWhiteSpace(submission.JsonData)
                ? new Dictionary<string, object?>()
                : JsonSerializer.Deserialize<Dictionary<string, object?>>(submission.JsonData)
                  ?? new Dictionary<string, object?>();

            return Result<SubmissionDetailDto>.Ok(new SubmissionDetailDto
            {
                SubmissionId = submission.SubmissionId,
                FormId = submission.FormId,
                FormName = form?.FormName ?? "",
                VersionNo = version?.VersionNo ?? 0,
                SubmittedOn = submission.SubmittedOn,
                Values = values
            });
        }
    }
}
