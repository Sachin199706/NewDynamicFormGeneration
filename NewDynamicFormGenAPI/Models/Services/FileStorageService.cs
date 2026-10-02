using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.Models.Services
{
    /// <summary>
    /// Service for managing file storage operations for form attachments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FileStorageService implements the IFileStorageService abstraction, providing a centralized
    /// interface for file I/O operations. This enables flexible backend implementation without
    /// coupling business logic to specific storage mechanisms.
    /// </para>
    /// <para>
    /// Current implementation stores files in <c>App_Data/Uploads</c> directory on the server.
    /// Files are named using a GUID prefix for uniqueness, preserving the original filename
    /// for user reference and download.
    /// </para>
    /// <para>
    /// File storage interaction:
    /// <list type="bullet">
    /// <item><description>Submission: Files uploaded with forms are saved via SaveFileAsync, which returns the stored filename</description></item>
    /// <item><description>Storage: Filenames (not full paths) are stored in FormSubmission.JsonData</description></item>
    /// <item><description>Serving: Stored filenames are retrieved via GetFilePath and served through FilesController</description></item>
    /// <item><description>Cleanup: Files can be removed via DeleteFile if submission revisions or deletions are implemented</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStorageService"/> class.
        /// </summary>
        /// <param name="env">The ASP.NET Core web host environment for path resolution.</param>
        public FileStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Saves an uploaded file to disk and returns its stored filename.
        /// </summary>
        /// <param name="aObjFile">The uploaded file from multipart form data.</param>
        /// <returns>
        /// The stored filename (not the full path) in format <c>{Guid}_{originalFileName}</c>.
        /// This filename should be stored in submission JSON data for later retrieval.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method:
        /// <list type="number">
        /// <item><description>Creates the App_Data/Uploads directory if it doesn't exist</description></item>
        /// <item><description>Generates a unique filename by prefixing the original with a GUID</description></item>
        /// <item><description>Writes the file stream to disk</description></item>
        /// <item><description>Returns the stored filename (not full path) for JSON storage</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// The GUID prefix ensures filename uniqueness even if multiple users upload files with the same name.
        /// The original filename is preserved (after the GUID) for user-friendly downloads.
        /// </para>
        /// <para>
        /// Callers store the returned filename in submission JSON data (e.g., control values).
        /// The filename is later used by FilesController.GetFile to resolve and serve the file.
        /// </para>
        /// </remarks>
        public async Task<string> SaveFileAsync(IFormFile aObjFile)
        {
            var lstrUploadsRoot = Path.Combine(_env.ContentRootPath, "App_Data", "Uploads");
            Directory.CreateDirectory(lstrUploadsRoot);

            var lstrStoredFileName = $"{Guid.NewGuid()}_{aObjFile.FileName}";
            var lstrFullPath = Path.Combine(lstrUploadsRoot, lstrStoredFileName);

            using (var lobjStream = new FileStream(lstrFullPath, FileMode.Create))
            {
                await aObjFile.CopyToAsync(lobjStream);
            }

            return lstrStoredFileName;
        }

        /// <summary>
        /// Deletes a file from storage by its stored filename.
        /// </summary>
        /// <param name="aStrStoredFileName">The stored filename (not full path) to delete.</param>
        /// <remarks>
        /// <para>
        /// This method safely removes a file from the uploads directory. If the file does not exist,
        /// the operation completes without error (idempotent behavior).
        /// </para>
        /// <para>
        /// Future use case: When submission deletion or revision is implemented, use this method
        /// to clean up associated file attachments.
        /// </para>
        /// </remarks>
        public void DeleteFile(string aStrStoredFileName)
        {
            var lstrUploadsRoot = Path.Combine(_env.ContentRootPath, "App_Data", "Uploads");
            var lstrFullPath = Path.Combine(lstrUploadsRoot, aStrStoredFileName);

            if (File.Exists(lstrFullPath))
            {
                File.Delete(lstrFullPath);
            }
        }

        /// <summary>
        /// Resolves a stored filename to its full file path, with validation.
        /// </summary>
        /// <param name="aStrStoredFileName">The stored filename (not full path) to resolve.</param>
        /// <returns>
        /// The full file path if the file exists and the filename is valid;
        /// otherwise, <c>null</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method provides secure path resolution by:
        /// <list type="bullet">
        /// <item><description>Validating that the filename contains no path traversal attempts (e.g., "../")</description></item>
        /// <item><description>Confirming the file exists on disk before returning the path</description></item>
        /// <item><description>Returning null for invalid or non-existent files</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Used by FilesController.GetFile to safely retrieve files for serving to clients.
        /// The security validation prevents directory traversal attacks by rejecting filenames
        /// that resolve to paths outside the uploads directory.
        /// </para>
        /// </remarks>
        public string? GetFilePath(string aStrStoredFileName)
        {
            if (string.IsNullOrWhiteSpace(aStrStoredFileName)) return null;

            var lstrSafeName = Path.GetFileName(aStrStoredFileName);
            if (!string.Equals(lstrSafeName, aStrStoredFileName, StringComparison.Ordinal)) return null;

            var lstrFullPath = Path.Combine(GetUploadsRoot(), lstrSafeName);
            return File.Exists(lstrFullPath) ? lstrFullPath : null;
        }

        /// <summary>
        /// Gets the root uploads directory path.
        /// </summary>
        /// <returns>The full path to the App_Data/Uploads directory.</returns>
        private string GetUploadsRoot() => Path.Combine(_env.ContentRootPath, "App_Data", "Uploads");
    }
}