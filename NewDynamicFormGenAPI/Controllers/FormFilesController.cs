using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewDynamicFormGenAPI.Models.Interfaces;
using FormFile = NewDynamicFormGenAPI.Models.Entities.FormFile;

namespace NewDynamicFormGenAPI.API.Controllers;

[ApiController]
[Route("api/files")]
public class FormFilesController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IWebHostEnvironment _env;

    public FormFilesController(IUnitOfWork uow, IWebHostEnvironment env)
    {
        _uow = uow;
        _env = env;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm(Name = "submissionId")] int aNumSubmissionId, [FromForm(Name = "controlId")] int aNumControlId, [FromForm(Name = "file")] IFormFile aObjFile)
    {
        if (aObjFile == null || aObjFile.Length == 0)
            return BadRequest("No file provided.");

        var lstrUploadsRoot = Path.Combine(_env.ContentRootPath, "App_Data", "Uploads");
        Directory.CreateDirectory(lstrUploadsRoot);

        var lstrStoredFileName = $"{Guid.NewGuid()}_{aObjFile.FileName}";
        var lstrFullPath = Path.Combine(lstrUploadsRoot, lstrStoredFileName);

        using (var lobjStream = new FileStream(lstrFullPath, FileMode.Create))
        {
            await aObjFile.CopyToAsync(lobjStream);
        }

        var lobjEntity = new FormFile
        {
            SubmissionId = aNumSubmissionId,
            ControlId = aNumControlId,
            FileName = aObjFile.FileName,
            StoragePath = lstrFullPath,
            ContentType = aObjFile.ContentType,
            FileSizeBytes = aObjFile.Length,
            UploadedOn = DateTime.UtcNow
        };

        await _uow.Repository<FormFile>().AddAsync(lobjEntity);
        await _uow.SaveChangesAsync();

        return Ok(new { fileId = lobjEntity.FileId, fileName = lobjEntity.FileName });
    }

    [HttpGet("{aNumFileId:int}/download")]
    public async Task<IActionResult> Download(int aNumFileId)
    {
        var lobjEntity = await _uow.Repository<FormFile>().GetByIdAsync(aNumFileId);
        if (lobjEntity == null || !System.IO.File.Exists(lobjEntity.StoragePath))
            return NotFound();

        var lobjBytes = await System.IO.File.ReadAllBytesAsync(lobjEntity.StoragePath);
        return File(lobjBytes, lobjEntity.ContentType ?? "application/octet-stream", lobjEntity.FileName);
    }
}