namespace NewDynamicFormGenAPI.Models.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile aObjFile);
    void DeleteFile(string aStrStoredFileName);
}
