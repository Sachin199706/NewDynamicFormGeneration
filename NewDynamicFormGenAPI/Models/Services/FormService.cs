
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

    public async Task<PagedResult<FormListItemDto>> GetFormsAsync(int page, int pageSize, string? search)
    {
        var query = _uow.Repository<Form>().Query().Where(f => f.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(f => f.FormName.Contains(search));

        var total = query.Count();
        var items = query
            .OrderByDescending(f => f.ModifiedDate ?? f.CreatedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(f => new FormListItemDto
            {
                FormId = f.FormId,
                FormCode = f.FormCode,
                FormName = f.FormName,
                Description = f.Description,
                Status = f.Status,
                CurrentVersionNo = f.CurrentVersion != null ? f.CurrentVersion.VersionNo : (int?)null,
                ModifiedDate = f.ModifiedDate ?? f.CreatedDate
            }).ToList();

        return new PagedResult<FormListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<Result<FormVersionDto>> SaveVersionAsync(SaveFormVersionDto dto)
    {
        int formId;
        // Runs only on first save — form doesn't exist yet, so create a new row
        // Inserted into the Forms table
        if (!dto.FormId.HasValue || dto.FormId.Value == 0)
        {
            var form = new Form
            {
                FormCode = Guid.NewGuid().ToString("N")[..12],
                FormName = dto.FormName ?? "Untitled Form",
                Status = FormStatus.Draft,
                CreatedDate = DateTime.UtcNow
            };
            await _uow.Repository<Form>().AddAsync(form);
            await _uow.SaveChangesAsync();
            formId = form.FormId;
        }
        else
        {
            formId = dto.FormId.Value;
        }

        // Find Version ID using FormId 
        var nextVersionNo = _uow.Repository<FormVersion>().Query()
            .Where(v => v.FormId == formId)
            .Select(v => (int?)v.VersionNo)
            .Max() ?? 0;
        nextVersionNo++;
        //Prepare Version Object 
        var version = new FormVersion
        {
            FormId = formId,
            VersionNo = nextVersionNo,
            Status = FormStatus.Draft,
            FormDefinitionJson = dto.FormDefinitionJson,
            LayoutDefinitionJson = dto.LayoutDefinitionJson,
            CreatedDate = DateTime.UtcNow
        };
        //Store Data in FormVersion
        await _uow.Repository<FormVersion>().AddAsync(version);
        await _uow.SaveChangesAsync(); // need FormVersionId

        //foreach (var layout in dto.Layouts)
        //{
        //    await _uow.Repository<FormLayout>().AddAsync(new FormLayout
        //    {
        //        FormVersionId = version.FormVersionId,
        //        LayoutType = layout.LayoutType,
        //        ParentLayoutId = layout.ParentLayoutId,
        //        Name = layout.Name,
        //        DisplayOrder = layout.DisplayOrder,
        //        PropertiesJson = layout.PropertiesJson
        //    });
        //}

        var controlTypeIdsByCode = _uow.Repository<ControlType>().Query()
            .ToDictionary(ct => ct.ControlCode, ct => ct.ControlTypeId);

        foreach (var control in dto.Controls)
        {
            if (!controlTypeIdsByCode.TryGetValue(control.ControlTypeCode, out var controlTypeId))
                continue; // unknown control type — skip rather than fail the whole save

            await _uow.Repository<FormControl>().AddAsync(new FormControl
            {
                FormVersionId = version.FormVersionId,
                ControlKey = control.ControlKey,
                ControlTypeId = controlTypeId,
                Label = control.Label,
                Placeholder = control.Placeholder,
                DefaultValue = control.DefaultValue,
                IsRequired = control.IsRequired,
                IsReadOnly = control.IsReadOnly,
                IsVisible = control.IsVisible,
                DisplayOrder = control.DisplayOrder,
                ParentControlId = control.ParentControlId,
                PropertiesJson = control.PropertiesJson,
                DataSourceId = control.DataSourceId,
                CreatedDate = DateTime.UtcNow
            });
        }

        await _uow.SaveChangesAsync();

        // point the form at the version just saved
        var formEntity = await _uow.Repository<Form>().GetByIdAsync(formId);
        if (formEntity != null)
        {
            formEntity.CurrentVersionId = version.FormVersionId;
            formEntity.ModifiedDate = DateTime.UtcNow;
            _uow.Repository<Form>().Update(formEntity);
            await _uow.SaveChangesAsync();
        }

        return await GetLatestVersionAsync(formId);
    }

    public async Task<Result<FormVersionDto>> GetLatestVersionAsync(int formId)
    {
        var version = _uow.Repository<FormVersion>().Query()
            .Where(v => v.FormId == formId)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefault();

        if (version == null)
            return Result<FormVersionDto>.Fail("No versions found for this form.");

        return Result<FormVersionDto>.Ok(await BuildVersionDto(version));
    }

    public async Task<Result<FormRenderDto>> GetRenderPayloadAsync(int formId, int formVersionId)
    {
        var form = await _uow.Repository<Form>().GetByIdAsync(formId);
        if (form == null) return Result<FormRenderDto>.Fail("Form not found.");

        var versionDto = await BuildVersionDto(
            _uow.Repository<FormVersion>().Query().First(v => v.FormVersionId == formVersionId));

        var rules = await _ruleEngine.GetRulesForVersionAsync(formVersionId);

        return Result<FormRenderDto>.Ok(new FormRenderDto
        {
            FormId = form.FormId,
            FormVersionId = formVersionId,
            FormName = form.FormName,
            LayoutDefinitionJson = versionDto.LayoutDefinitionJson,
            Controls = versionDto.Controls,
            Layouts = versionDto.Layouts,
            Rules = rules
        });
    }

    public async Task<Result<bool>> PublishAsync(int formId, int formVersionId)
    {
        var version = await _uow.Repository<FormVersion>().GetByIdAsync(formVersionId);
        if (version == null || version.FormId != formId)
            return Result<bool>.Fail("Version not found.");

        version.Status = FormStatus.Published;
        version.PublishedDate = DateTime.UtcNow;
        _uow.Repository<FormVersion>().Update(version);

        var form = await _uow.Repository<Form>().GetByIdAsync(formId);
        if (form != null)
        {
            form.Status = FormStatus.Published;
            form.CurrentVersionId = formVersionId;
            _uow.Repository<Form>().Update(form);
        }

        await _uow.Repository<FormPublishHistory>().AddAsync(new FormPublishHistory
        {
            FormId = formId,
            FormVersionId = formVersionId,
            PublishedOn = DateTime.UtcNow
        });

        await _uow.SaveChangesAsync();
        return Result<bool>.Ok(true, "Published.");
    }

    private async Task<FormVersionDto> BuildVersionDto(FormVersion version)
    {
        var controlTypeCodeById = _uow.Repository<ControlType>().Query()
            .ToDictionary(ct => ct.ControlTypeId, ct => ct.ControlCode);

        var controls = _uow.Repository<FormControl>().Query()
            .Where(c => c.FormVersionId == version.FormVersionId)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new FormControlDto
            {
                ControlId = c.ControlId,
                ControlKey = c.ControlKey,
                ControlTypeCode = controlTypeCodeById.GetValueOrDefault(c.ControlTypeId, ""),
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

        var layouts = _uow.Repository<FormLayout>().Query()
            .Where(l => l.FormVersionId == version.FormVersionId)
            .OrderBy(l => l.DisplayOrder)
            .Select(l => new FormLayoutDto
            {
                LayoutId = l.LayoutId,
                LayoutType = l.LayoutType,
                ParentLayoutId = l.ParentLayoutId,
                Name = l.Name,
                DisplayOrder = l.DisplayOrder,
                PropertiesJson = l.PropertiesJson
            }).ToList();

        return new FormVersionDto
        {
            FormVersionId = version.FormVersionId,
            FormId = version.FormId,
            VersionNo = version.VersionNo,
            Status = version.Status,
            FormDefinitionJson = version.FormDefinitionJson,
            LayoutDefinitionJson = version.LayoutDefinitionJson,
            Controls = controls,
            Layouts = layouts,
            CreatedDate = version.CreatedDate
        };
    }
}
