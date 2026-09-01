using AutoMapper;
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
        private readonly IMapper _mapper;

        public SubmissionService(IUnitOfWork uow, IRuleEngineService ruleEngine, IFileStorageService fileStorage, IMapper mapper)
        {
            _uow = uow;
            _ruleEngine = ruleEngine;
            _fileStorage = fileStorage;
            _mapper = mapper;
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

            var lobjSubmission = _mapper.Map<FormSubmission>(aobjDto);

            // First save — need a real, database-assigned SubmissionId before the code can be built.
            await _uow.Repository<FormSubmission>().AddAsync(lobjSubmission);
            await _uow.SaveChangesAsync();

            var lobjForm = await _uow.Repository<Form>().GetByIdAsync(aobjDto.FormId);
            var lobjVersion = await _uow.Repository<FormVersion>().GetByIdAsync(aobjDto.FormVersionId);

            // Second save — now SubmissionId actually exists, so the code is correct.
            lobjSubmission.SubmissionCode = $"{lobjForm?.FormCode}-v{lobjVersion?.VersionNo}-{lobjSubmission.SubmissionId}";
            _uow.Repository<FormSubmission>().Update(lobjSubmission);
            await _uow.SaveChangesAsync();

            return Result<int>.Ok(lobjSubmission.SubmissionId, "Submitted successfully.");
        }

        public async Task<Result<bool>> MarkAsReadAsync(int submissionId)
        {
            var submission = await _uow.Repository<FormSubmission>().GetByIdAsync(submissionId);
            if (submission == null)
                return Result<bool>.Fail("Submission not found.");
            submission.IsRead = true;
            _uow.Repository<FormSubmission>().Update(submission);
            await _uow.SaveChangesAsync();
            return Result<bool>.Ok(true, "Marked as read.");
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

        public async Task<PagedResult<SubmissionOverviewItemDto>> GetAllSubmissionsAsync(SubmissionFilterDto aObjFilter)
        {
            var lobjQuery =
                from s in _uow.Repository<FormSubmission>().Query()
                join f in _uow.Repository<Form>().Query() on s.FormId equals f.FormId
                join v in _uow.Repository<FormVersion>().Query() on s.FormVersionId equals v.FormVersionId
                select new { s, f, v };

            if (!string.IsNullOrWhiteSpace(aObjFilter.Search))
            {
                var lstrSearch = aObjFilter.Search.Trim();
                lobjQuery = lobjQuery.Where(x =>
                    x.s.SubmissionCode.Contains(lstrSearch) || x.f.FormName.Contains(lstrSearch));
            }

            if (aObjFilter.FormId.HasValue)
                lobjQuery = lobjQuery.Where(x => x.s.FormId == aObjFilter.FormId.Value);

            if (aObjFilter.IsRead.HasValue)
                lobjQuery = lobjQuery.Where(x => x.s.IsRead == aObjFilter.IsRead.Value);

            if (aObjFilter.FromDate.HasValue)
                lobjQuery = lobjQuery.Where(x => x.s.SubmittedOn >= aObjFilter.FromDate.Value);

            if (aObjFilter.ToDate.HasValue)
                lobjQuery = lobjQuery.Where(x => x.s.SubmittedOn <= aObjFilter.ToDate.Value.AddDays(1).AddTicks(-1));

            var lnumTotal = lobjQuery.Count();

            var larrItems = lobjQuery
                .OrderByDescending(x => x.s.SubmittedOn)
                .Skip((aObjFilter.Page - 1) * aObjFilter.PageSize)
                .Take(aObjFilter.PageSize)
                .Select(x => new SubmissionOverviewItemDto
                {
                    SubmissionId = x.s.SubmissionId,
                    SubmissionCode = x.s.SubmissionCode,
                    FormId = x.f.FormId,
                    FormName = x.f.FormName,
                    VersionNo = x.v.VersionNo,
                    SubmittedOn = x.s.SubmittedOn,
                    IsRead = x.s.IsRead
                })
                .ToList();

            return new PagedResult<SubmissionOverviewItemDto>
            {
                Items = larrItems,
                Page = aObjFilter.Page,
                PageSize = aObjFilter.PageSize,
                TotalCount = lnumTotal
            };
        }

        public async Task<SubmissionStatsDto> GetStatsAsync()
        {
            var lobjQuery = _uow.Repository<FormSubmission>().Query();

            return new SubmissionStatsDto
            {
                TotalSubmissions = lobjQuery.Count(),
                UnreadSubmissions = lobjQuery.Count(s => !s.IsRead),
                ReadSubmissions = lobjQuery.Count(s => s.IsRead)
            };
        }

        public async Task<SubmissionStatsDto> GetStatsAsync(int inumID)
        {
            var lobjQuery = await _uow.Repository<FormSubmission>().GetAllAsync(x => x.FormId == inumID);

            return new SubmissionStatsDto
            {
                TotalSubmissions = lobjQuery.Count(),
                UnreadSubmissions = lobjQuery.Count(s => !s.IsRead),
                ReadSubmissions = lobjQuery.Count(s => s.IsRead)
            };
        }
    }
}