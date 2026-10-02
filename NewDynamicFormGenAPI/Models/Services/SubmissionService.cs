using AutoMapper;
using NewDynamicFormGenAPI.Models.Common;
using NewDynamicFormGenAPI.Models.DTOs.Submissions;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Interfaces;
using System.Text.Json;


namespace FormGen.Application.Services;

/// <summary>
/// Service for managing form submissions including capture, validation, file handling, and analytics.
/// </summary>
/// <remarks>
/// <para>
/// SubmissionService orchestrates the complete form submission lifecycle:
/// <list type="bullet">
/// <item><description>Form submission capture with file attachment processing</description></item>
/// <item><description>Submission data validation and persistence</description></item>
/// <item><description>Submission detail retrieval and admin review workflows</description></item>
/// <item><description>Submission status tracking (read/unread)</description></item>
/// <item><description>Analytics and statistics queries</description></item>
/// </list>
/// </para>
/// <para>
/// Each submission maintains a JSON snapshot of submitted form data, enabling:
/// <list type="bullet">
/// <item><description>Historical record of exactly what data was submitted</description></item>
/// <item><description>Support for form versioning (submissions remain tied to their version)</description></item>
/// <item><description>Audit trails (submission code encodes form, version, and submission ID)</description></item>
/// </list>
/// </para>
/// <para>
/// File attachments are saved via the IFileStorageService abstraction, supporting flexible storage backends.
/// </para>
/// </remarks>
public class SubmissionService : ISubmissionService
{
    private readonly IUnitOfWork _uow;
    private readonly IFileStorageService _fileStorage;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmissionService"/> class.
    /// </summary>
    /// <param name="uow">The unit of work for database access.</param>
    /// <param name="fileStorage">The file storage service for attachment handling.</param>
    /// <param name="mapper">The AutoMapper instance for DTO to entity mapping.</param>
    public SubmissionService(IUnitOfWork uow, IFileStorageService fileStorage, IMapper mapper)
    {
        _uow = uow;
        _fileStorage = fileStorage;
        _mapper = mapper;

    }

    /// <summary>
    /// Submits a completed form with values and optional file attachments.
    /// </summary>
    /// <param name="aobjDto">The submission request containing form ID, version ID, and control values.</param>
    /// <param name="aObjFiles">File attachments (if any) uploaded with the form.</param>
    /// <returns>
    /// A <see cref="Result{int}"/> containing the resulting SubmissionId if successful;
    /// otherwise, a failure result with error messages.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method processes a form submission through the following steps:
    /// <list type="number">
    /// <item><description>Load the form version to capture version information</description></item>
    /// <item><description>Save any attached files via IFileStorageService</description></item>
    /// <item><description>Map the SubmitFormDto to a FormSubmission entity</description></item>
    /// <item><description>Persist the submission to generate a SubmissionId</description></item>
    /// <item><description>Generate a human-readable submission code using form code, version, and ID</description></item>
    /// <item><description>Update the submission with the generated code</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Validation rules are enforced by the frontend (Angular RuleEngineService), not the backend.
    /// File upload size is limited by the MultipartBodyLengthLimit configured in Program.cs (10 MB max).
    /// </para>
    /// <para>
    /// The submission captures a JSON snapshot of submitted values, enabling historical tracking
    /// and version-specific data correlation.
    /// </para>
    /// </remarks>
    public async Task<Result<int>> SubmitAsync(SubmitFormDto aobjDto, IFormFileCollection aObjFiles)
    {
        // Needed for the submission code below — VersionNo is part of it.
        var lobjVersion = await _uow.Repository<FormVersion>().GetByIdAsync(aobjDto.FormVersionId);

        // Rules are enforced in the browser only — see the Angular RuleEngineService.
        // Upload size is capped by MultipartBodyLengthLimit in Program.cs.
        foreach (var lobjFile in aObjFiles)
        {
            aobjDto.Values[lobjFile.Name] = await _fileStorage.SaveFileAsync(lobjFile);
        }

        var lobjSubmission = _mapper.Map<FormSubmission>(aobjDto);

        // First save — need a real, database-assigned SubmissionId before the code can be built.
        await _uow.Repository<FormSubmission>().AddAsync(lobjSubmission);
        await _uow.SaveChangesAsync();

        var lobjForm = await _uow.Repository<Form>().GetByIdAsync(aobjDto.FormId);

        // Second save — now SubmissionId actually exists, so the code is correct.
        lobjSubmission.SubmissionCode = $"{lobjForm?.FormCode}-v{lobjVersion?.VersionNo}-{lobjSubmission.SubmissionId}";
        _uow.Repository<FormSubmission>().Update(lobjSubmission);
        await _uow.SaveChangesAsync();

        return Result<int>.Ok(lobjSubmission.SubmissionId, "Submitted successfully.");
    }

    /// <summary>
    /// Marks a submission as read by an administrator.
    /// </summary>
    /// <param name="submissionId">The submission ID to mark as read.</param>
    /// <returns>
    /// A <see cref="Result{bool}"/> indicating success; HTTP 404 if the submission is not found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method updates the IsRead flag to indicate that an administrator or reviewer
    /// has viewed the submission. Used in admin dashboards to highlight new or unreviewed submissions.
    /// </para>
    /// </remarks>
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
        var lboolAscending = string.Equals(aObjFilter.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var lobjOrderedQuery = (aObjFilter.SortBy ?? "submittedOn").ToLowerInvariant() switch
        {
            "submissioncode" => lboolAscending
                ? lobjQuery.OrderBy(x => x.s.SubmissionCode)
                : lobjQuery.OrderByDescending(x => x.s.SubmissionCode),
            "formname" => lboolAscending
                ? lobjQuery.OrderBy(x => x.f.FormName)
                : lobjQuery.OrderByDescending(x => x.f.FormName),
            "versionno" => lboolAscending
                ? lobjQuery.OrderBy(x => x.v.VersionNo)
                : lobjQuery.OrderByDescending(x => x.v.VersionNo),
            "submittedon" => lboolAscending
                ? lobjQuery.OrderBy(x => x.s.SubmittedOn)
                : lobjQuery.OrderByDescending(x => x.s.SubmittedOn),
            _ => lobjQuery.OrderByDescending(x => x.s.SubmittedOn)
        };

        var larrItems = lobjOrderedQuery
            .ThenBy(x => x.s.SubmissionId)
            .Skip((aObjFilter.Page - 1) * aObjFilter.PageSize)
            .Take(aObjFilter.PageSize)
            .Select(x => new SubmissionOverviewItemDto
            {
                SubmissionId = x.s.SubmissionId,
                SubmissionCode = x.s.SubmissionCode,
                FormId = x.f.FormId,
                FormVersionId = x.s.FormVersionId,
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