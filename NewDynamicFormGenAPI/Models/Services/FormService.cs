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
                CurrentVersionId=f.CurrentVersionId,
                CurrentVersionNo = f.CurrentVersion != null ? f.CurrentVersion.VersionNo : (int?)null,
                ModifiedDate = f.ModifiedDate ?? f.CreatedDate
            }).ToList();

        return new PagedResult<FormListItemDto> { Items = larrItems, Page = aNumPage, PageSize = aNumPageSize, TotalCount = lnumTotal };
    }

    public async Task<Result<FormVersionDto>> SaveVersionAsync(SaveFormVersionDto aObjDto)
    {
        int lnumFormId;
        // Runs only on first save — form doesn't exist yet, so create a new row
        // Inserted into the Forms table
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

        // Find Version ID using FormId 
        var lnumNextVersionNo = _uow.Repository<FormVersion>().Query()
            .Where(v => v.FormId == lnumFormId)
            .Select(v => (int?)v.VersionNo)
            .Max() ?? 0;
        lnumNextVersionNo++;
        //Prepare Version Object 
        var lobjVersion = new FormVersion
        {
            FormId = lnumFormId,
            VersionNo = lnumNextVersionNo,
            Status = FormStatus.Draft,
            FormDefinitionJson = aObjDto.FormDefinitionJson,
            LayoutDefinitionJson = aObjDto.LayoutDefinitionJson,
            CreatedDate = DateTime.UtcNow
        };
        //Store Data in FormVersion
        await _uow.Repository<FormVersion>().AddAsync(lobjVersion);
        await _uow.SaveChangesAsync(); // need FormVersionId

        // TODO: FormLayouts not currently used — Layout Designer screen not built yet.
        //foreach (var layout in aObjDto.Layouts)
        //{
        //    await _uow.Repository<FormLayout>().AddAsync(new FormLayout
        //    {
        //        FormVersionId = lobjVersion.FormVersionId,
        //        LayoutType = layout.LayoutType,
        //        ParentLayoutId = layout.ParentLayoutId,
        //        Name = layout.Name,
        //        DisplayOrder = layout.DisplayOrder,
        //        PropertiesJson = layout.PropertiesJson
        //    });
        //}

        var lobjControlTypeIdsByCode = _uow.Repository<ControlType>().Query()
            .ToDictionary(ct => ct.ControlCode, ct => ct.ControlTypeId);

        foreach (var lobjControl in aObjDto.Controls)
        {
            if (!lobjControlTypeIdsByCode.TryGetValue(lobjControl.ControlTypeCode, out var lnumControlTypeId))
                continue; // unknown control type — skip rather than fail the whole save

            await _uow.Repository<FormControl>().AddAsync(new FormControl
            {
                FormVersionId = lobjVersion.FormVersionId,
                ControlKey = lobjControl.ControlKey,
                ControlTypeId = lnumControlTypeId,
                Label = lobjControl.Label,
                Placeholder = lobjControl.Placeholder,
                DefaultValue = lobjControl.DefaultValue,
                IsRequired = lobjControl.IsRequired,
                IsReadOnly = lobjControl.IsReadOnly,
                IsVisible = lobjControl.IsVisible,
                DisplayOrder = lobjControl.DisplayOrder,
                ParentControlId = lobjControl.ParentControlId,
                PropertiesJson = lobjControl.PropertiesJson,
                DataSourceId = lobjControl.DataSourceId,
                CreatedDate = DateTime.UtcNow
            });
        }

        await _uow.SaveChangesAsync();

        // point the form at the version just saved
        var lobjFormEntity = await _uow.Repository<Form>().GetByIdAsync(lnumFormId);
        if (lobjFormEntity != null)
        {
            lobjFormEntity.CurrentVersionId = lobjVersion.FormVersionId;
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

        var lobjDto = await BuildVersionDto(lobjVersion);

        var lobjForm = await _uow.Repository<Form>().GetByIdAsync(aNumFormId);
        lobjDto.FormName = lobjForm?.FormName ?? "";

        return Result<FormVersionDto>.Ok(lobjDto);
    }

    public async Task<Result<FormRenderDto>> GetRenderPayloadAsync(int aNumFormId, int aNumFormVersionId)
    {
        var lobjForm = await _uow.Repository<Form>().GetByIdAsync(aNumFormId);
        if (lobjForm == null) return Result<FormRenderDto>.Fail("Form not found.");

        var lobjVersionDto = await BuildVersionDto(
            _uow.Repository<FormVersion>().Query().First(v => v.FormVersionId == aNumFormVersionId));

        var larrRules = await _ruleEngine.GetRulesForVersionAsync(aNumFormVersionId);

        return Result<FormRenderDto>.Ok(new FormRenderDto
        {
            FormId = lobjForm.FormId,
            FormVersionId = aNumFormVersionId,
            FormName = lobjForm.FormName,
            LayoutDefinitionJson = lobjVersionDto.LayoutDefinitionJson,
            Controls = lobjVersionDto.Controls,
            Layouts = lobjVersionDto.Layouts,
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

    private async Task<FormVersionDto> BuildVersionDto(FormVersion aObjVersion)
    {
        var lobjControlTypeCodeById = _uow.Repository<ControlType>().Query()
            .ToDictionary(ct => ct.ControlTypeId, ct => ct.ControlCode);

        var larrControls = _uow.Repository<FormControl>().Query()
            .Where(c => c.FormVersionId == aObjVersion.FormVersionId)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new FormControlDto
            {
                ControlId = c.ControlId,
                ControlKey = c.ControlKey,
                ControlTypeCode = lobjControlTypeCodeById.GetValueOrDefault(c.ControlTypeId, ""),
                Label = c.Label,
                Placeholder = c.Placeholder,
                DefaultValue = c.DefaultValue,
                IsRequired = c.IsRequired,
                IsReadOnly = c.IsReadOnly,
                IsVisible = c.IsVisible,
                DisplayOrder = c.DisplayOrder,
                ParentControlId = c.ParentControlId,
                PropertiesJson = c.PropertiesJson,
                DataSourceId = c.DataSourceId
            }).ToList();

        // TODO: FormLayouts not currently used — Layout Designer screen not built yet.
        //var larrLayouts = _uow.Repository<FormLayout>().Query()
        //    .Where(l => l.FormVersionId == aObjVersion.FormVersionId)
        //    .OrderBy(l => l.DisplayOrder)
        //    .Select(l => new FormLayoutDto
        //    {
        //        LayoutId = l.LayoutId,
        //        LayoutType = l.LayoutType,
        //        ParentLayoutId = l.ParentLayoutId,
        //        Name = l.Name,
        //        DisplayOrder = l.DisplayOrder,
        //        PropertiesJson = l.PropertiesJson
        //    }).ToList();
        var larrLayouts = new List<FormLayoutDto>();

        return new FormVersionDto
        {
            FormVersionId = aObjVersion.FormVersionId,
            FormId = aObjVersion.FormId,
            VersionNo = aObjVersion.VersionNo,
            Status = aObjVersion.Status,
            FormDefinitionJson = aObjVersion.FormDefinitionJson,
            LayoutDefinitionJson = aObjVersion.LayoutDefinitionJson,
            Controls = larrControls,
            Layouts = larrLayouts,
            CreatedDate = aObjVersion.CreatedDate
        };
    }
}