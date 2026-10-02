using NewDynamicFormGenAPI.Models.Common;
using NewDynamicFormGenAPI.Models.DTOs.Forms;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Enums;
using NewDynamicFormGenAPI.Models.Interfaces;
using System.Text.Json;

namespace NewDynamicFormGenAPI.Models.Services;

/// <summary>
/// Service for managing form lifecycle operations including creation, versioning, publishing, and rendering.
/// </summary>
/// <remarks>
/// <para>
/// FormService orchestrates all form-related business logic, including:
/// <list type="bullet">
/// <item><description>Form CRUD operations (Create, Read, Update form templates)</description></item>
/// <item><description>Form versioning (creating, updating, and retrieving form versions)</description></item>
/// <item><description>Form publishing (marking versions as publicly available)</description></item>
/// <item><description>Form rendering for frontend display</description></item>
/// <item><description>Publication history tracking for audit trails</description></item>
/// </list>
/// </para>
/// <para>
/// Each form has multiple versions managed through FormVersion entities. FormDefinitionJson
/// serves as the single source of truth for form structure, controls, and embedded rules.
/// This enables form versioning, history tracking, and submission correlation to specific form states.
/// </para>
/// </remarks>
public class FormService : IFormService
{
    private readonly IUnitOfWork _uow;
    private readonly IRuleEngineService _ruleEngine;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="FormService"/> class.
    /// </summary>
    /// <param name="uow">The unit of work for database access across repositories.</param>
    /// <param name="ruleEngine">The rule engine service for rule extraction and processing.</param>
    public FormService(IUnitOfWork uow, IRuleEngineService ruleEngine)
    {
        _uow = uow;
        _ruleEngine = ruleEngine;
    }

    /// <summary>
    /// Retrieves a paginated list of forms with optional search and date filtering.
    /// </summary>
    /// <param name="aNumPage">The page number (1-based) to retrieve.</param>
    /// <param name="aNumPageSize">The maximum number of forms per page.</param>
    /// <param name="aStrSearch">Optional text to search in form name, code, or description.</param>
    /// <param name="fromDate">Optional start date to filter forms by creation date.</param>
    /// <param name="toDate">Optional end date to filter forms by creation date.</param>
    /// <returns>
    /// A <see cref="PagedResult{FormListItemDto}"/> containing filtered forms and pagination metadata.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method supports multiple filtering strategies:
    /// <list type="bullet">
    /// <item><description>Text search – matches FormName, FormCode, or Description</description></item>
    /// <item><description>Date range filtering – includes forms created within the specified range</description></item>
    /// <item><description>Pagination – returns a subset of results based on page and page size</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Results are ordered by modification date (or creation date if not modified), displaying
    /// most recently updated forms first.
    /// </para>
    /// </remarks>
    public async Task<PagedResult<FormListItemDto>> GetFormsAsync(int aNumPage, int aNumPageSize, string? aStrSearch, DateTime? fromDate, DateTime? toDate)
    {
        var lobjQuery = _uow.Repository<Form>().Query();

        if (!string.IsNullOrWhiteSpace(aStrSearch))
            lobjQuery = lobjQuery.Where(f => f.FormName.Contains(aStrSearch) ||
            (!string.IsNullOrEmpty(f.Description) && f.Description.Contains(aStrSearch))
            || f.FormCode.Contains(aStrSearch));

        if (fromDate.HasValue)
            lobjQuery = lobjQuery.Where(f => f.CreatedDate >= fromDate);

        if (toDate.HasValue)
            lobjQuery = lobjQuery.Where(f => f.CreatedDate <= toDate);

        var lnumTotal = lobjQuery.Count();
        var larrItems = lobjQuery
            .OrderByDescending(f => f.ModifiedDate ?? f.CreatedDate)
            .Skip((aNumPage - 1) * aNumPageSize).Take(aNumPageSize)
            .Select(f => new FormListItemDto
            {
                FormId = f.FormId,
                FormCode = f.FormCode,
                FormName = f.FormName,
                Description = f.Description,
                ModifiedDate = f.ModifiedDate ?? f.CreatedDate
            }).ToList();

        return new PagedResult<FormListItemDto> { Items = larrItems, Page = aNumPage, PageSize = aNumPageSize, TotalCount = lnumTotal };
    }

    /// <summary>
    /// Creates a new form template with initial metadata.
    /// </summary>
    /// <param name="aObjDto">The form creation request containing name, code, and description.</param>
    /// <returns>
    /// A <see cref="Result{FormListItemDto}"/> containing the newly created form's details if successful;
    /// otherwise, a failure result with error messages.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Form creation establishes a new form template that can then be versioned and designed.
    /// At this stage, the form has no structure or controls; these are added via versions.
    /// </para>
    /// <para>
    /// The created form entry is assigned an auto-generated FormId and creation timestamp.
    /// </para>
    /// </remarks>
    public async Task<Result<FormListItemDto>> CreateFormAsync(CreateFormDto aObjDto)
    {
        var lobjForm = new Form
        {
            FormName = aObjDto.FormName,
            FormCode = aObjDto.FormCode,
            Description = aObjDto.Description,
            CreatedDate = DateTime.UtcNow
        };

        await _uow.Repository<Form>().AddAsync(lobjForm);
        await _uow.SaveChangesAsync();

        return Result<FormListItemDto>.Ok(new FormListItemDto
        {
            FormId = lobjForm.FormId,
            FormName = lobjForm.FormName,
            FormCode = lobjForm.FormCode,
            Description = lobjForm.Description,
            CreatedDate = lobjForm.CreatedDate,
            ModifiedDate = lobjForm.ModifiedDate ?? lobjForm.CreatedDate
        });
    }

    /// <summary>
    /// Updates an existing form template's metadata (name, code, description).
    /// </summary>
    /// <param name="aNumFormId">The form ID to update.</param>
    /// <param name="aObjDto">The form update request with revised metadata.</param>
    /// <returns>
    /// A <see cref="Result{FormListItemDto}"/> containing the updated form details if successful;
    /// otherwise, HTTP 404 if the form is not found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method updates form-level metadata only. To modify form structure and controls,
    /// use <see cref="SaveVersionAsync"/> to create or update a form version.
    /// </para>
    /// <para>
    /// The ModifiedDate is automatically updated to the current UTC time.
    /// </para>
    /// </remarks>
    public async Task<Result<FormListItemDto>> UpdateFormAsync(int aNumFormId, CreateFormDto aObjDto)
    {
        var lobjForm = await _uow.Repository<Form>().GetByIdAsync(aNumFormId);
        if (lobjForm is null)
            return Result<FormListItemDto>.Fail("Form template not found.");

        lobjForm.FormName = aObjDto.FormName;
        lobjForm.FormCode = aObjDto.FormCode;
        lobjForm.Description = aObjDto.Description;
        lobjForm.ModifiedDate = DateTime.UtcNow;
        _uow.Repository<Form>().Update(lobjForm);
        await _uow.SaveChangesAsync();

        return Result<FormListItemDto>.Ok(new FormListItemDto
        {
            FormId = lobjForm.FormId,
            FormName = lobjForm.FormName,
            FormCode = lobjForm.FormCode,
            Description = lobjForm.Description,
            CreatedDate = lobjForm.CreatedDate,
            ModifiedDate = lobjForm.ModifiedDate.Value
        });
    }

    /// <summary>
    /// Creates a new form version or updates an existing one with form structure, controls, and rules.
    /// </summary>
    /// <param name="aObjDto">
    /// The version save request containing FormId, optional FormVersionId, and form definition JSON
    /// with complete control and rule structures.
    /// </param>
    /// <returns>
    /// A <see cref="Result{FormVersionDto}"/> containing the saved form version details.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method supports two scenarios:
    /// <list type="number">
    /// <item><description>
    /// **Creating a new version:** If FormVersionId is not provided or is 0, a new FormVersion record
    /// is created with the next sequential VersionNo. The new version defaults to Draft status.
    /// </description></item>
    /// <item><description>
    /// **Updating an existing version:** If FormVersionId is provided, the specified version is updated
    /// in-place with the new form definition, layout, and description.
    /// </description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The FormDefinitionJson is the single source of truth containing:
    /// <list type="bullet">
    /// <item><description>controls array – all form controls with properties and configuration</description></item>
    /// <item><description>rules array – validation and conditional rules embedded in the definition</description></item>
    /// <item><description>sections (optional) – logical grouping of controls</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The parent form's ModifiedDate is automatically updated regardless of version creation or update.
    /// </para>
    /// </remarks>
    public async Task<Result<FormVersionDto>> SaveVersionAsync(SaveFormVersionDto aObjDto)
    {
        int lnumFormId = aObjDto.FormId;

        // If a FormVersionId is provided, update the existing version
        if (aObjDto.FormVersionId.HasValue && aObjDto.FormVersionId.Value > 0)
        {
            var lobjVersion = await _uow.Repository<FormVersion>().GetByIdAsync(aObjDto.FormVersionId.Value);
            if (lobjVersion == null)
                return Result<FormVersionDto>.Fail("Form version not found.");
            if (lobjVersion.FormId != aObjDto.FormId)
                return Result<FormVersionDto>.Fail("The form version does not belong to the specified form.");
            if (string.Equals(lobjVersion.Status, FormStatus.Published, StringComparison.OrdinalIgnoreCase))
                return Result<FormVersionDto>.Fail("Published form versions cannot be modified. Save as a new Draft version.");

            // update stored JSON/layout/description
            lobjVersion.FormDefinitionJson = aObjDto.FormDefinitionJson;
            lobjVersion.LayoutDefinitionJson = aObjDto.LayoutDefinitionJson;
            lobjVersion.VersionDescription = aObjDto.VersionDescription;
            _uow.Repository<FormVersion>().Update(lobjVersion);
            await _uow.SaveChangesAsync();

            // update parent form modified date
            var lobjFormEntity = await _uow.Repository<Form>().GetByIdAsync(lnumFormId);
            if (lobjFormEntity != null)
            {
                lobjFormEntity.ModifiedDate = DateTime.UtcNow;
                _uow.Repository<Form>().Update(lobjFormEntity);
                await _uow.SaveChangesAsync();
            }

            return await GetVersionByIdAsync(aObjDto.FormVersionId.Value);
        }

        // Otherwise create a new version
        var lnumNextVersionNo = _uow.Repository<FormVersion>().Query()
            .Where(v => v.FormId == lnumFormId)
            .Select(v => (int?)v.VersionNo)
            .Max() ?? 0;
        lnumNextVersionNo++;

        var lobjNewVersion = new FormVersion
        {
            FormId = lnumFormId,
            VersionNo = lnumNextVersionNo,
            VersionDescription = aObjDto.VersionDescription,
            Status = FormStatus.Draft,
            FormDefinitionJson = aObjDto.FormDefinitionJson,
            LayoutDefinitionJson = aObjDto.LayoutDefinitionJson,
            CreatedDate = DateTime.UtcNow
        };

        await _uow.Repository<FormVersion>().AddAsync(lobjNewVersion);
        await _uow.SaveChangesAsync();

        // update parent form modified date
        var lobjForm = await _uow.Repository<Form>().GetByIdAsync(lnumFormId);
        if (lobjForm != null)
        {
            lobjForm.ModifiedDate = DateTime.UtcNow;
            _uow.Repository<Form>().Update(lobjForm);
            await _uow.SaveChangesAsync();
        }

        return await GetVersionByIdAsync(lobjNewVersion.FormVersionId);
    }

    public async Task<Result<FormVersionDto>> GetLatestVersionAsync(int aNumFormId)
    {
        var lobjVersion = _uow.Repository<FormVersion>().Query()
            .Where(v => v.FormId == aNumFormId)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefault();

        if (lobjVersion == null)
            return Result<FormVersionDto>.Fail("No versions found for this form.");

        var lobjDto = BuildVersionDto(lobjVersion);

        var lobjForm = await _uow.Repository<Form>().GetByIdAsync(aNumFormId);
        lobjDto.FormName = lobjForm?.FormName ?? "";

        return Result<FormVersionDto>.Ok(lobjDto);
    }

    public async Task<Result<FormRenderDto>> GetRenderPayloadAsync(int aNumFormId, int aNumFormVersionId)
    {
        var lobjForm = await _uow.Repository<Form>().GetByIdAsync(aNumFormId);
        if (lobjForm == null) return Result<FormRenderDto>.Fail("Form not found.");

        var lobjVersion = _uow.Repository<FormVersion>().Query().First(v => v.FormId == lobjForm.FormId && v.FormVersionId == aNumFormVersionId );
        var lobjVersionDto = BuildVersionDto(lobjVersion);
        var larrRules = await _ruleEngine.GetRulesForVersionAsync(aNumFormVersionId);

        return Result<FormRenderDto>.Ok(new FormRenderDto
        {
            FormId = lobjForm.FormId,
            FormVersionId = aNumFormVersionId,
            FormName = lobjForm.FormName,
            LayoutDefinitionJson = lobjVersionDto.LayoutDefinitionJson,
            Controls = lobjVersionDto.Controls,
            Rules = larrRules
        });
    }

    public async Task<Result<bool>> PublishAsync(int aNumFormId, int aNumFormVersionId)
    {
        var lobjVersion = await _uow.Repository<FormVersion>().GetByIdAsync(aNumFormVersionId);
        if (lobjVersion == null || lobjVersion.FormId != aNumFormId)
            return Result<bool>.Fail("Version not found.");

        lobjVersion.Status = FormStatus.Published;
        lobjVersion.PublishedDate = DateTime.UtcNow;
        _uow.Repository<FormVersion>().Update(lobjVersion);

        var lobjForm = await _uow.Repository<Form>().GetByIdAsync(aNumFormId);
        if (lobjForm != null)
        {
            _uow.Repository<Form>().Update(lobjForm);
        }

        await _uow.Repository<FormPublishHistory>().AddAsync(new FormPublishHistory
        {
            FormId = aNumFormId,
            FormVersionId = aNumFormVersionId,
            PublishedOn = DateTime.UtcNow
        });

        await _uow.SaveChangesAsync();
        return Result<bool>.Ok(true, "Published.");
    }

    public async Task<List<FormVersionListItemDto>> GetAllVersionsAsync()
    {
        var larrVersions = _uow.Repository<FormVersion>().Query()
            .Where(v => v.Status == FormStatus.Draft)
            .OrderByDescending(v => v.CreatedDate)
            .ToList();

        var lobjFormNamesById = _uow.Repository<Form>().Query()
            .ToDictionary(f => f.FormId, f => f.FormName);

        return larrVersions.Select(v => new FormVersionListItemDto
        {
            FormId = v.FormId,
            FormVersionId = v.FormVersionId,
            FormName = lobjFormNamesById.GetValueOrDefault(v.FormId, "Unknown"),
            VersionNo = v.VersionNo,
            Status = v.Status,
            ModifiedDate = v.CreatedDate
        }).ToList();
    }

    public async Task<PagedResult<FormVersionListItemDto>> GetVersionsAsync(int aNumFormId, int aNumPage, int aNumPageSize, string? aStrSearch, DateTime? fromDate, DateTime? toDate, string? status)
    {
        var lobjQuery = _uow.Repository<FormVersion>().Query()
            .Where(v => v.FormId == aNumFormId);

        if (!string.IsNullOrWhiteSpace(aStrSearch))
            lobjQuery = lobjQuery.Where(v => v.Form.FormName.Contains(aStrSearch)
                || v.FormVersionId.ToString().Contains(aStrSearch)
                || v.FormId.ToString().Contains(aStrSearch)
                || v.VersionNo.ToString().Contains(aStrSearch)
                || v.Status.Contains(aStrSearch));

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            lobjQuery = lobjQuery.Where(v => v.Status == status);

        if (fromDate.HasValue)
            lobjQuery = lobjQuery.Where(v => v.CreatedDate >= fromDate.Value);

        if (toDate.HasValue)
            lobjQuery = lobjQuery.Where(v => v.CreatedDate < toDate.Value.AddDays(1));

        var lnumTotal = lobjQuery.Count();
        var larrVersions = lobjQuery
            .OrderByDescending(v => v.CreatedDate)
            .Skip((aNumPage - 1) * aNumPageSize)
            .Take(aNumPageSize)
            .ToList();
        var lstrFormName = _uow.Repository<Form>().Query()
            .Where(f => f.FormId == aNumFormId)
            .Select(f => f.FormName)
            .FirstOrDefault() ?? "Unknown";

        return new PagedResult<FormVersionListItemDto>
        {
            Items = larrVersions.Select(v => new FormVersionListItemDto
            {
                FormId = v.FormId,
                FormVersionId = v.FormVersionId,
                FormName = lstrFormName,
                VersionNo = v.VersionNo,
                Status = v.Status,
                ModifiedDate = v.CreatedDate,
                VersionDescription = v.VersionDescription
            }).ToList(),
            Page = aNumPage,
            PageSize = aNumPageSize,
            TotalCount = lnumTotal
        };
    }

    public async Task<List<FormPublishHistoryItemDto>> GetPublishHistoryAsync()
    {
        var larrHistory = _uow.Repository<FormPublishHistory>().Query()
            .OrderByDescending(h => h.PublishedOn)
            .ToList();

        var lobjFormNamesById = _uow.Repository<Form>().Query()
            .ToDictionary(f => f.FormId, f => f.FormName);

        var lobjVersionNosById = _uow.Repository<FormVersion>().Query()
            .ToDictionary(v => v.FormVersionId, v => new { v.VersionNo, v.VersionDescription });

        return larrHistory.Select(h => new FormPublishHistoryItemDto
        {
            FormId = h.FormId,
            FormVersionId = h.FormVersionId,
            FormName = lobjFormNamesById.GetValueOrDefault(h.FormId, "Unknown"),
            VersionNo = lobjVersionNosById.GetValueOrDefault(h.FormVersionId)?.VersionNo ?? 0,
            PublishedOn = h.PublishedOn,
            VersionDescription = lobjVersionNosById.GetValueOrDefault(h.FormVersionId)?.VersionDescription
        }).ToList();
    }

    public async Task<Result<FormVersionDto>> GetVersionByIdAsync(int aNumFormVersionId)
    {
        var lobjVersion = _uow.Repository<FormVersion>().Query()
            .FirstOrDefault(v => v.FormVersionId == aNumFormVersionId);

        if (lobjVersion == null)
            return Result<FormVersionDto>.Fail("Version not found.");

        var lobjDto = BuildVersionDto(lobjVersion);

        var lobjForm = await _uow.Repository<Form>().GetByIdAsync(lobjVersion.FormId);
        lobjDto.FormName = lobjForm?.FormName ?? "";

        return Result<FormVersionDto>.Ok(lobjDto);
    }

    /// <summary>
    /// Controls now live entirely inside FormDefinitionJson — this parses them out
    /// instead of querying a FormControls table, which no longer exists.
    /// </summary>
    private static FormVersionDto BuildVersionDto(FormVersion aObjVersion)
    {
        return new FormVersionDto
        {
            FormVersionId = aObjVersion.FormVersionId,
            FormId = aObjVersion.FormId,
            VersionDescription = aObjVersion.VersionDescription,
            VersionNo = aObjVersion.VersionNo,
            Status = aObjVersion.Status,
            FormDefinitionJson = aObjVersion.FormDefinitionJson,
            LayoutDefinitionJson = aObjVersion.LayoutDefinitionJson,
            Controls = ParseControls(aObjVersion.FormDefinitionJson),
            Sections = ParseSections(aObjVersion.FormDefinitionJson),
            CreatedDate = aObjVersion.CreatedDate
        };
    }

    internal static List<FormControlDto> ParseControls(string aStrFormDefinitionJson)
    {
        if (string.IsNullOrWhiteSpace(aStrFormDefinitionJson)) return new List<FormControlDto>();

        try
        {
            using var lobjDoc = JsonDocument.Parse(aStrFormDefinitionJson);
            if (!lobjDoc.RootElement.TryGetProperty("controls", out var lobjControlsEl)) return new List<FormControlDto>();

            var larrControls = JsonSerializer.Deserialize<List<FormControlDto>>(lobjControlsEl.GetRawText(), JsonOpts)
                ?? new List<FormControlDto>();

            foreach (var lobjControl in larrControls)
            {
                foreach (var lobjRule in lobjControl.Rules)
                {
                    lobjRule.ControlKey = lobjControl.ControlKey;
                }
            }

            return larrControls;
        }
        catch
        {
            return new List<FormControlDto>();
        }
    }
    public async Task<DashboardDTO> GetDashboardCountAsync()
    {
        var lobjForms = _uow.Repository<Form>().Query();
        var lobjVersions = _uow.Repository<FormVersion>().Query();
        var lobjDashboard = new DashboardDTO
        {
            TotalForms = lobjForms.Count(),
            TotalVersions = lobjVersions.Count(),
            DraftForms = lobjVersions.Where(v => v.Status == FormStatus.Draft).Select(v => v.FormVersionId).Distinct().Count(),
            PublishedForms = lobjVersions.Where(v => v.Status == FormStatus.Published).Select(v => v.FormVersionId).Distinct().Count(),
            ArchivedForms = lobjVersions.Where(v => v.Status == FormStatus.Archived).Select(v => v.FormId).Distinct().Count(),
            RecentForms = (
                from v in lobjVersions
                join f in lobjForms on v.FormId equals f.FormId
                orderby v.CreatedDate descending
                select new FormVersionListItemDto
                {
                    FormId = v.FormId,
                    FormVersionId = v.FormVersionId,
                    FormName = f.FormName,
                    VersionNo = v.VersionNo,
                    Status = v.Status,
                    ModifiedDate = v.CreatedDate,
                    VersionDescription = v.VersionDescription
                }
            ).Take(10).ToList()
        };

        return await Task.FromResult(lobjDashboard);
    }

    /// <summary>
    /// Sections sit beside "controls" in FormDefinitionJson. Versions saved before sections
    /// existed have no such property, which is not an error — they parse as an empty list.
    /// </summary>
    internal static List<FormSectionDto> ParseSections(string aStrFormDefinitionJson)
    {
        if (string.IsNullOrWhiteSpace(aStrFormDefinitionJson)) return new List<FormSectionDto>();

        try
        {
            using var lobjDoc = JsonDocument.Parse(aStrFormDefinitionJson);
            if (!lobjDoc.RootElement.TryGetProperty("sections", out var lobjSectionsEl))
                return new List<FormSectionDto>();

            var larrSections = JsonSerializer.Deserialize<List<FormSectionDto>>(lobjSectionsEl.GetRawText(), JsonOpts)
                ?? new List<FormSectionDto>();

            return larrSections.OrderBy(s => s.DisplayOrder).ToList();
        }
        catch
        {
            return new List<FormSectionDto>();
        }
    }

}