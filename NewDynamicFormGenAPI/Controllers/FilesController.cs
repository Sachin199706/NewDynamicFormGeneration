using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.Controllers
{
    /// <summary>
    /// API controller for serving file attachments uploaded with form submissions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FilesController provides endpoints for retrieving files that were uploaded as attachments
    /// during form submission. Files are stored on the server with GUIDs to ensure uniqueness,
    /// while the original file name is preserved for download purposes.
    /// </para>
    /// <para>
    /// Two retrieval modes are supported:
    /// <list type="bullet">
    /// <item><description>Inline display – for embedding images or previewing files in the browser (GET without download parameter)</description></item>
    /// <item><description>Attachment download – for saving files to the user's device (GET with download=true)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This controller abstracts file storage details through the <see cref="IFileStorageService"/> interface,
    /// enabling flexible storage backend implementations (local file system, cloud, etc.).
    /// </para>
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

        private readonly IFileStorageService _fileStorage;

        /// <summary>
        /// Initializes a new instance of the <see cref="FilesController"/> class.
        /// </summary>
        /// <param name="fileStorage">The file storage service for file path resolution.</param>
        public FilesController(IFileStorageService fileStorage)
        {
            _fileStorage = fileStorage;
        }

        /// <summary>
        /// Retrieves a file attachment with optional download or inline display mode.
        /// </summary>
        /// <param name="aStrFileName">The stored filename (with GUID prefix) to retrieve.</param>
        /// <param name="download">
        /// If <c>true</c>, the file is returned as an attachment with Content-Disposition header,
        /// prompting the browser to save it. If <c>false</c> (default), the file is displayed inline
        /// (e.g., images in an &lt;img&gt; tag).
        /// </param>
        /// <returns>
        /// An HTTP 200 OK response containing the file stream with appropriate Content-Type header
        /// if the file exists; otherwise, HTTP 404 Not Found.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Stored file names follow the format <c>{Guid}_{originalFileName}</c> to ensure uniqueness
        /// while preserving the original name for user reference on download.
        /// </para>
        /// <para>
        /// Usage patterns:
        /// <list type="bullet">
        /// <item><description>Inline: <c>GET /api/files/abc123-def456_document.pdf</c> → File streamed to browser</description></item>
        /// <item><description>Download: <c>GET /api/files/abc123-def456_document.pdf?download=true</c> → File saved with original name <c>document.pdf</c></description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Content type is determined by file extension. If the extension is not recognized,
        /// the default <c>application/octet-stream</c> is used, causing browsers to download the file.
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// Extracts the original filename by removing the GUID prefix from a stored filename.
        /// </summary>
        /// <param name="aStrStoredFileName">
        /// The stored filename in format <c>{Guid}_{originalName}</c>, e.g., <c>a1b2c3d4-e5f6_document.pdf</c>.
        /// </param>
        /// <returns>
        /// The original filename without the GUID prefix, e.g., <c>document.pdf</c>.
        /// If the filename does not contain a GUID prefix (no underscore or invalid GUID), the stored filename is returned unchanged.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is used when returning files as attachments to restore the user-friendly
        /// original filename. For example:
        /// <list type="bullet">
        /// <item><description>Stored: <c>a1b2c3d4-e5f6_MyResume.pdf</c></description></item>
        /// <item><description>Download filename: <c>MyResume.pdf</c></description></item>
        /// </list>
        /// </para>
        /// <para>
        /// The GUID prefix follows UUID format with a single underscore separator.
        /// If the text before the underscore is not a valid GUID, or if no underscore is found,
        /// the original stored filename is preserved.
        /// </para>
        /// </remarks>
        private static string StripGuidPrefix(string aStrStoredFileName)
        {
            var lnumIndex = aStrStoredFileName.IndexOf('_');

            return lnumIndex > 0 && Guid.TryParse(aStrStoredFileName[..lnumIndex], out _)
                ? aStrStoredFileName[(lnumIndex + 1)..]
                : aStrStoredFileName;
        }
    }
}

