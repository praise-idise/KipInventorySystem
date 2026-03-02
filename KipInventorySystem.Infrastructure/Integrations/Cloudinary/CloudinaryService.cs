using KipInventorySystem.Application.Services.FilesUpload;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using static KipInventorySystem.Shared.Models.AppSettings;

namespace KipInventorySystem.Infrastructure.Integrations.Cloudinary;

internal class CloudinaryService(IOptions<CloudinarySettings> cloudinarySettings) : IFilesUploadService
{
    private readonly CloudinarySettings _settings = cloudinarySettings.Value;
    private readonly CloudinaryDotNet.Cloudinary _cloudinary =
        new(new Account(cloudinarySettings.Value.CloudName, cloudinarySettings.Value.ApiKey, cloudinarySettings.Value.ApiSecret))
        {
            Api = { Secure = true }
        };

    public async Task<List<string>> UploadFilesAsync(IEnumerable<(Stream Stream, string filename)> files, CancellationToken cancellationToken)
    {
        var uploadTasks = files.Select(file =>
        {
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(file.filename, file.Stream),
                Folder = $"{_settings.FolderPrefix}/Files",
                Overwrite = true,
                UseFilename = true,
            };

            return _cloudinary.UploadAsync(uploadParams);
        });

        var results = await Task.WhenAll(uploadTasks);

        return [.. results.Select(r => r.SecureUrl?.AbsoluteUri!)];
    }

    public async Task<List<string>> UploadImagesAsync(IEnumerable<(Stream Stream, string filename)> images, CancellationToken cancellationToken)
    {
        var uploadTasks = images.Select(img =>
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(img.filename, img.Stream),
                Folder = $"{_settings.FolderPrefix}/Images",
                Overwrite = true,
                UseFilename = true,
            };

            return _cloudinary.UploadAsync(uploadParams, cancellationToken);
        });

        var results = await Task.WhenAll(uploadTasks);

        return results.Select(r => r.SecureUrl?.AbsoluteUri!).ToList();
    }

    public async Task<string> UploadImageAsync(Stream stream, string filename, CancellationToken cancellationToken)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(filename, stream),
            Folder = $"{_settings.FolderPrefix}/Images",
            Overwrite = true,
            UseFilename = true,
        };

        var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);

        return result.SecureUrl?.AbsoluteUri!;
    }

    public async Task<string> UploadFileAsync(Stream stream, string filename, CancellationToken cancellationToken)
    {
        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(filename, stream),
            Folder = $"{_settings.FolderPrefix}/Files",
            Overwrite = true,
            UseFilename = true,
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        return result.SecureUrl?.AbsoluteUri!;
    }
}
