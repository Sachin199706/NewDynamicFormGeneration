using NewDynamicFormGenAPI.Models.Interfaces;

namespace NewDynamicFormGenAPI.Models.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;

        public FileStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Saves the file to disk and returns the stored filename (not the full path) —
        /// callers store this name in JsonData; it's what gets served back later
        /// via static file hosting at /uploads/{fileName}.
        /// </summary>
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

        public void DeleteFile(string aStrStoredFileName)
        {
            var lstrUploadsRoot = Path.Combine(_env.ContentRootPath, "App_Data", "Uploads");
            var lstrFullPath = Path.Combine(lstrUploadsRoot, aStrStoredFileName);

            if (File.Exists(lstrFullPath))
            {
                File.Delete(lstrFullPath);
            }
        }
    }
}