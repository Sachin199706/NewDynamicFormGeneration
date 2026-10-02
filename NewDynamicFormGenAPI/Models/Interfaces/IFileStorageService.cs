namespace NewDynamicFormGenAPI.Models.Interfaces;

/// <summary>
/// Defines operations for managing form file uploads and attachments.
/// </summary>
/// <remarks>
/// <para>
/// IFileStorageService abstracts file storage operations, enabling implementations to use
/// different storage backends (local file system, Azure Blob Storage, AWS S3, etc.)
/// without affecting form submission logic.
/// </para>
/// <para>
/// Files are stored using storage-generated names (typically GUIDs) while the original
/// filenames are preserved in form submission metadata for display purposes.
/// </para>
/// </remarks>
public interface IFileStorageService
{
    /// <summary>
    /// Saves an uploaded file to storage and returns a reference identifier.
    /// </summary>
    /// <param name="aObjFile">The uploaded file to store.</param>
    /// <returns>A stored file name or reference identifier for later retrieval.</returns>
    /// <remarks>
    /// <para>
    /// This method:
    /// <list type="bullet">
    ///   <item><description>Validates the file (size, type, etc.)</description></item>
    ///   <item><description>Generates a unique storage identifier (typically a GUID)</description></item>
    ///   <item><description>Saves the file to the configured storage backend</description></item>
    ///   <item><description>Returns the storage identifier for later reference</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The original filename is not used for storage to prevent filename collisions and security issues.
    /// The returned identifier is stored in submission metadata and used for retrieval.
    /// </para>
    /// </remarks>
    Task<string> SaveFileAsync(IFormFile aObjFile);

    /// <summary>
    /// Deletes a previously stored file from storage.
    /// </summary>
    /// <param name="aStrStoredFileName">The storage reference identifier returned by <see cref="SaveFileAsync"/>.</param>
    /// <remarks>
    /// <para>
    /// This method removes the file from storage. It is typically called when:
    /// <list type="bullet">
    ///   <item><description>A submission is deleted and its attachments are no longer needed</description></item>
    ///   <item><description>File retention policies require cleanup of old submissions</description></item>
    ///   <item><description>User data privacy requirements mandate file deletion</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// If the file does not exist, this method should gracefully handle the condition
    /// without throwing an exception (file already deleted).
    /// </para>
    /// </remarks>
    void DeleteFile(string aStrStoredFileName);

    /// <summary>
    /// Retrieves the file path or URI for a stored file, enabling download or display.
    /// </summary>
    /// <param name="aStrStoredFileName">The storage reference identifier returned by <see cref="SaveFileAsync"/>.</param>
    /// <returns>A file path or URI for the stored file, or <c>null</c> if the file does not exist.</returns>
    /// <remarks>
    /// <para>
    /// The returned path/URI format depends on the storage backend:
    /// <list type="bullet">
    ///   <item><description>Local file system: file system path or download URL</description></item>
    ///   <item><description>Cloud storage: HTTP URI to the object</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The returned URI should be suitable for download links or preview display in the form review interface.
    /// </para>
    /// </remarks>
    string? GetFilePath(string aStrStoredFileName);
}
