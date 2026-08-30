using System.Text.Json;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Common;
using NewDynamicFormGenAPI.Models.DTOs.Forms;
using NewDynamicFormGenAPI.Models.Enums;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.Models.Services;

public class FormService : IFormService
{
    private readonly IUnitOfWork _uow;
    private readonly IRuleEngineService _ruleEngine;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public FormService(IUnitOfWork uow, IRuleEngineService ruleEngine)
    {
        _uow = uow;
        _ruleEngine = ruleEngine;
    }

    public async Task<PagedResult<FormListItemDto>> GetFormsAsync(int aNumPage, int aNumPageSize, string? aStrSearch)
    {
        var lobjQuery = _uow.Repository<Form>().Query().Where(f => f.IsActive);

        if (!string.IsNullOrWhiteSpace(aStrSearch))
            lobjQuery = lobjQuery.Where(f => f.FormName.Contains(aStrSearch));

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
                Status = f.Status,
                CurrentVersionId = f.CurrentVersionId,
                CurrentVersionNo = f.CurrentVersion != null ? f.CurrentVersion.VersionNo : (int?)null,
                ModifiedDate = f.ModifiedDate ?? f.CreatedDate
            }).ToList();

        return new PagedResult<FormListItemDto> { Items = larrItems, Page = aNumPage, PageSize = aNumPageSize, TotalCount = lnumTotal };
    }

    public async Task<Result<FormVersionDto>> SaveVersionAsync(SaveFormVersionDto aObjDto)
    {
        int lnumFormId;
        if (!aObjDto.FormId.HasValue || aObjDto.FormId.Value == 0)
        {
            var lobjForm = new Form
            {
                FormCode = Guid.NewGuid().ToString("N")[..12],
                FormName = aObjDto.FormName ?? "Untitled Form",
                Status = FormStatus.Draft,
                CreatedDate = DateTime.UtcNow
            };
            await _uow.Repository<Form>().AddAsync(lobjForm);
            await _uow.SaveChangesAsync();
            lnumFormId = lobjForm.FormId;
        }
        else
        {
            lnumFormId = aObjDto.FormId.Value;
        }

        var lnumNextVersionNo = _uow.Repository<FormVersion>().Query()
            .Where(v => v.FormId == lnumFormId)
            .Select(v => (int?)v.VersionNo)
            .Max() ?? 0;
        lnumNextVersionNo++;

        // Controls (and their embedded rules) live entirely inside FormDefinitionJson now —
        // no FormControls table anymore, so this is just a straight save of the JSON blob
        // the builder already sent. No per-control insert loop needed.
        var lobjVersion = new FormVersion
        {
            FormId = lnumFormId,
            VersionNo = lnumNextVersionNo,
            Status = FormStatus.Draft,
            FormDefinitionJson = aObjDto.FormDefinitionJson,
            LayoutDefinitionJson = aObjDto.LayoutDefinitionJson,
            CreatedDate = DateTime.UtcNow
        };
        await _uow.Repository<FormVersion>().AddAsync(lobjVersion);
        await _uow.SaveChangesAsync();

        var lobjFormEntity = await _uow.Repository<Form>().GetByIdAsync(lnumFormId);
        if (lobjFormEntity != null)
        {
            if (lobjFormEntity.Status != FormStatus.Published)
            {
                lobjFormEntity.CurrentVersionId = lobjVersion.FormVersionId;
            }
            lobjFormEntity.ModifiedDate = DateTime.UtcNow;
            _uow.Repository<Form>().Update(lobjFormEntity);
            await _uow.SaveChangesAsync();
        }

        return await GetLatestVersionAsync(lnumFormId);
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

        var lobjVersion = _uow.Repository<FormVersion>().Query().First(v => v.FormVersionId == aNumFormVersionId);
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
            lobjForm.Status = FormStatus.Published;
            lobjForm.CurrentVersionId = aNumFormVersionId;
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

    public async Task<List<FormPublishHistoryItemDto>> GetPublishHistoryAsync()
    {
        var larrHistory = _uow.Repository<FormPublishHistory>().Query()
            .OrderByDescending(h => h.PublishedOn)
            .ToList();

        var lobjFormNamesById = _uow.Repository<Form>().Query()
            .ToDictionary(f => f.FormId, f => f.FormName);

        var lobjVersionNosById = _uow.Repository<FormVersion>().Query()
            .ToDictionary(v => v.FormVersionId, v => v.VersionNo);

        return larrHistory.Select(h => new FormPublishHistoryItemDto
        {
            FormId = h.FormId,
            FormVersionId = h.FormVersionId,
            FormName = lobjFormNamesById.GetValueOrDefault(h.FormId, "Unknown"),
            VersionNo = lobjVersionNosById.GetValueOrDefault(h.FormVersionId, 0),
            PublishedOn = h.PublishedOn
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
            VersionNo = aObjVersion.VersionNo,
            Status = aObjVersion.Status,
            FormDefinitionJson = aObjVersion.FormDefinitionJson,
            LayoutDefinitionJson = aObjVersion.LayoutDefinitionJson,
            Controls = ParseControls(aObjVersion.FormDefinitionJson),
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

            return larrControls.OrderBy(c => c.DisplayOrder).ToList();
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
            DraftForms = lobjVersions.Where(v => v.Status == FormStatus.Draft).Select(v => v.FormId).Distinct().Count(),
            PublishedForms = lobjVersions.Where(v => v.Status == FormStatus.Published).Select(v => v.FormId).Distinct().Count(),
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
                    ModifiedDate = v.CreatedDate
                }
            ).Take(5).ToList()
        };

        return await Task.FromResult(lobjDashboard);
    }
}