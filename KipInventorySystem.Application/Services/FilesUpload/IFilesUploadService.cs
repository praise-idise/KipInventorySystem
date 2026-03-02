namespace KipInventorySystem.Application.Services.FilesUpload;

public interface IFilesUploadService
{
    Task<List<string>> UploadFilesAsync(IEnumerable<(Stream Stream, string filename)> files, CancellationToken cancellationToken);
    Task<List<string>> UploadImagesAsync(IEnumerable<(Stream Stream, string filename)> images, CancellationToken cancellationToken);
    Task<string> UploadImageAsync(Stream stream, string filename, CancellationToken cancellationToken);
    Task<string> UploadFileAsync(Stream stream, string filename, CancellationToken cancellationToken);
}
