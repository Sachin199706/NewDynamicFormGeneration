using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.Entities;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.API.Controllers;

/// <summary>
/// API controller for retrieving available control types for the form builder toolbox.
/// </summary>
/// <remarks>
/// <para>
/// ControlTypesController provides endpoints for accessing the control type catalog,
/// which powers the left-panel toolbox in the form designer. The catalog replaces the
/// legacy controls.xml file and is database-driven for dynamic management.
/// </para>
/// <para>
/// Control types define the available form controls (TextBox, Dropdown, DatePicker, etc.),
/// including their Angular component mappings and default configuration templates.
/// Only active control types are exposed to the form builder.
/// </para>
/// </remarks>
[ApiController]
[Route("api/control-types")]
public class ControlTypesController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ControlTypesController"/> class.
    /// </summary>
    /// <param name="uow">The unit of work for database access.</param>
    public ControlTypesController(IUnitOfWork uow)
    {
        _uow = uow;
    }

    /// <summary>
    /// Retrieves all active control types for the form builder toolbox.
    /// </summary>
    /// <returns>
    /// An HTTP 200 OK response containing a list of <see cref="ControlType"/> objects
    /// sorted by display order for toolbox presentation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This endpoint returns only active control types (IsActive = true), ordered by their
    /// DisplayOrder property. The response includes control metadata such as:
    /// <list type="bullet">
    ///   <item><description>ControlCode – Unique identifier for the control type</description></item>
    ///   <item><description>ControlName – Display name in the toolbox</description></item>
    ///   <item><description>Category – Logical grouping (Basic, Advanced, etc.)</description></item>
    ///   <item><description>ComponentName – Angular component selector for rendering</description></item>
    ///   <item><description>DefaultPropertiesJson – Template JSON for new control instances</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The form designer uses this endpoint to populate the toolbox on application startup,
    /// enabling designers to drag controls onto the canvas. Inactive controls are excluded
    /// from the response and cannot be used in new forms.
    /// </para>
    /// </remarks>
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
