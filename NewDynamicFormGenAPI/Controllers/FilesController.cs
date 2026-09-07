using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

        private readonly IFileStorageService _fileStorage;

        public FilesController(IFileStorageService fileStorage)
        {
            _fileStorage = fileStorage;
        }

        // GET api/files/{aStrFileName}                — inline, used by <img src>
        // GET api/files/{aStrFileName}?download=true  — attachment, used by the Download link
        [HttpGet("{aStrFileName}")]
        public IActionResult GetFile(string aStrFileName, [FromQuery] bool download = false)
        {
            var lstrFullPath = _fileStorage.GetFilePath(aStrFileName);
            if (lstrFullPath is null) return NotFound();

            if (!ContentTypeProvider.TryGetContentType(lstrFullPath, out var lstrContentType))
                lstrContentType = "application/octet-stream";

            var lobjStream = System.IO.File.OpenRead(lstrFullPath);

            return download
                ? File(lobjStream, lstrContentType, StripGuidPrefix(aStrFileName))
                : File(lobjStream, lstrContentType);
        }

        /// <summary>Stored names are "{Guid}_{originalName}" — a download should keep the original.</summary>
        private static string StripGuidPrefix(string aStrStoredFileName)
        {
            var lnumIndex = aStrStoredFileName.IndexOf('_');

            return lnumIndex > 0 && Guid.TryParse(aStrStoredFileName[..lnumIndex], out _)
                ? aStrStoredFileName[(lnumIndex + 1)..]
                : aStrStoredFileName;
        }
    }
}
