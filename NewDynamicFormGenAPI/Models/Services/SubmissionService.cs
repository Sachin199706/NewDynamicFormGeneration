using NewDynamicFormGenAPI.Models.Common;
using NewDynamicFormGenAPI.Models.DTOs.Submissions;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Interfaces;
using System.Text.Json;


namespace FormGen.Application.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly IUnitOfWork _uow;
        private readonly IRuleEngineService _ruleEngine;
        private readonly IFileStorageService _fileStorage;

        public SubmissionService(IUnitOfWork uow, IRuleEngineService ruleEngine, IFileStorageService fileStorage)
        {
            _uow = uow;
            _ruleEngine = ruleEngine;
            _fileStorage = fileStorage;
        }

        public async Task<Result<int>> SubmitAsync(SubmitFormDto aobjDto, IFormFileCollection aObjFiles)
        {
            var larrStoredFileNames = new List<string>();

            foreach (var lobjFile in aObjFiles)
            {
                var lstrStoredFileName = await _fileStorage.SaveFileAsync(lobjFile);
                larrStoredFileNames.Add(lstrStoredFileName);
                aobjDto.Values[lobjFile.Name] = lstrStoredFileName;
            }

            // Rules now live inside FormVersions.FormDefinitionJson, keyed by controlKey —
            // no more FormControls table to query for a ControlId <-> ControlKey mapping.
            var rules = await _ruleEngine.GetRulesForVersionAsync(aobjDto.FormVersionId);
            var evaluation = _ruleEngine.Evaluate(rules, aobjDto.Values);

            if (!evaluation.IsValid)
            {
                foreach (var lstrFileName in larrStoredFileNames)
                {
                    _fileStorage.DeleteFile(lstrFileName);
                }

                return Result<int>.Fail(
                    "Validation failed.",
                    evaluation.Failures.Select(f => $"{f.ControlKey}: {f.ErrorMessage}").ToList());
            }

            var submission = new FormSubmission
            {
                FormId = aobjDto.FormId,
                FormVersionId = aobjDto.FormVersionId,
                SubmittedOn = DateTime.UtcNow,
                JsonData = JsonSerializer.Serialize(aobjDto.Values)
            };

            await _uow.Repository<FormSubmission>().AddAsync(submission);
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