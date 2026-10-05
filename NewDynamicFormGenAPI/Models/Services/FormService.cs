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
    private readonly IPublicIdEncoder _publicIdEncoder;


    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public FormService(IUnitOfWork uow, IRuleEngineService ruleEngine, IPublicIdEncoder publicIdEncoder)
    {
        _uow = uow;
        _ruleEngine = ruleEngine;
        _publicIdEncoder = publicIdEncoder;
    }

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

        var lobjVersion = _uow.Repository<FormVersion>().Query().First(v => v.FormId == lobjForm.FormId && v.FormVersionId == aNumFormVersionId);
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
            PublicId = _publicIdEncoder.Encode(v.FormVersionId),
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
                PublicId = _publicIdEncoder.Encode(v.FormVersionId),
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
    public async Task<PagedResult<FormPublishHistoryItemDto>> GetPublishHistoryAsync(int aNumPage, int aNumPageSize, string? aStrSearch)
    {
        var lobjQuery =
            from h in _uow.Repository<FormPublishHistory>().Query()
            join f in _uow.Repository<Form>().Query() on h.FormId equals f.FormId
            join v in _uow.Repository<FormVersion>().Query() on h.FormVersionId equals v.FormVersionId
            select new { h, f, v };

        if (!string.IsNullOrWhiteSpace(aStrSearch))
        {
            var lstrSearch = aStrSearch.Trim();
            lobjQuery = lobjQuery.Where(x => x.f.FormName.Contains(lstrSearch)
                || ("v" + x.v.VersionNo.ToString()).Contains(lstrSearch)
                || (x.v.VersionDescription != null && x.v.VersionDescription.Contains(lstrSearch)));
        }

        var lnumTotal = lobjQuery.Count();
        var larrItems = lobjQuery
            .OrderByDescending(x => x.h.PublishedOn)
            .ThenByDescending(x => x.h.PublishHistoryId)
            .Skip((aNumPage - 1) * aNumPageSize)
            .Take(aNumPageSize)
            .Select(x => new FormPublishHistoryItemDto
            {
                FormId = x.h.FormId,
                FormVersionId = x.h.FormVersionId,
                FormName = x.f.FormName,
                VersionNo = x.v.VersionNo,
                PublishedOn = x.h.PublishedOn,
                VersionDescription = x.v.VersionDescription
            })
            .ToList();
        larrItems.ForEach(x => x.PublicId = _publicIdEncoder.Encode(x.FormVersionId));
        var lobjResult = new PagedResult<FormPublishHistoryItemDto>
        {
            Items = larrItems,
            Page = aNumPage,
            PageSize = aNumPageSize,
            TotalCount = lnumTotal
        };

        return await Task.FromResult(lobjResult);
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
        lobjDashboard.RecentForms.ForEach(x => x.PublicId = _publicIdEncoder.Encode(x.FormVersionId));
        return await Task.FromResult(lobjDashboard);
    }
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
    public async Task<PagedResult<FormVersionListItemDto>> GetDashboardVersionsAsync(int aNumPage, int aNumPageSize, string? aStrSearch)
    {
        var lobjQuery = _uow.Repository<FormVersion>().Query();

        if (!string.IsNullOrWhiteSpace(aStrSearch))
        {
            var lstrSearch = aStrSearch.Trim();
            lobjQuery = lobjQuery.Where(v => v.Form.FormName.Contains(lstrSearch)
                || ("v" + v.VersionNo.ToString()).Contains(lstrSearch)
                || (v.VersionDescription != null && v.VersionDescription.Contains(lstrSearch))
                || v.Status.Contains(lstrSearch));
        }

        var lnumTotal = lobjQuery.Count();
        var larrItems = lobjQuery
            .OrderByDescending(v => v.CreatedDate)
            .ThenByDescending(v => v.FormVersionId)
            .Skip((aNumPage - 1) * aNumPageSize)
            .Take(aNumPageSize)
            .Select(v => new FormVersionListItemDto
            {
               
                FormId = v.FormId,
                FormVersionId = v.FormVersionId,
                FormName = v.Form.FormName,
                VersionNo = v.VersionNo,
                Status = v.Status,
                ModifiedDate = v.CreatedDate,
                VersionDescription = v.VersionDescription
            })
            .ToList();
        larrItems.ForEach(x => x.PublicId = _publicIdEncoder.Encode(x.FormVersionId));
        var lobjResult = new PagedResult<FormVersionListItemDto>
        {
            Items = larrItems,
            Page = aNumPage,
            PageSize = aNumPageSize,
            TotalCount = lnumTotal
        };

        return await Task.FromResult(lobjResult);
    }
    public async Task<Result<FormRenderDto>> GetRenderPayloadAsync(string aStrPublicId)
    {
        if (!_publicIdEncoder.TryDecode(aStrPublicId, out var lnumFormVersionId))
            return Result<FormRenderDto>.Fail("Form not found.");
       var lobjVersion = await _uow.Repository<FormVersion>().GetByIdAsync(lnumFormVersionId);
        if (lobjVersion == null) return Result<FormRenderDto>.Fail("Form not found.");

        var lobjForm = await _uow.Repository<Form>().GetByIdAsync(lobjVersion.FormId);
        if (lobjForm == null) return Result<FormRenderDto>.Fail("Form not found.");

        var lobjVersionDto = BuildVersionDto(lobjVersion);
        var larrRules = await _ruleEngine.GetRulesForVersionAsync(lobjVersion.FormVersionId);

        return Result<FormRenderDto>.Ok(new FormRenderDto
        {
            PublicId = _publicIdEncoder.Encode(lobjVersion.FormVersionId),
            FormName = lobjForm.FormName,
            LayoutDefinitionJson = lobjVersionDto.LayoutDefinitionJson,
            Controls = lobjVersionDto.Controls,
            Rules = larrRules
        });
    }

}