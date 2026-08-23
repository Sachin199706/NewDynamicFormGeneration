using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.API.Controllers;

/// <summary>Toolbox source for the builder's left panel — DB-driven replacement for controls.xml.</summary>
[ApiController]
[Route("api/control-types")]
public class ControlTypesController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public ControlTypesController(IUnitOfWork uow)
    {
        _uow = uow;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var types = _uow.Repository<ControlType>().Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToList();
        return Ok(types);
    }
}
