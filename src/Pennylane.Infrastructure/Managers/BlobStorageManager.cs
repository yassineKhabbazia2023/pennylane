using Azure;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pennylane.Application.Consts;
using Pennylane.Application.Interfaces;
using Pennylane.Infrastructure.Options;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.Infrastructure.Managers;

public class BlobStorageManager(BlobServiceClient blobServiceClient, IOptions<BlobStorageOptions> options, ILogger<BlobStorageManager> logger) : IBlobStorageManager
{
    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
    private readonly BlobStorageOptions _options = options.Value;
    private readonly ILogger<BlobStorageManager> _logger = logger;

    public async Task<FileStreamResult?> GetFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new NullArgumentException(Errors.NullFileNameCode, string.Format(Errors.NullFileNameMessage, nameof(fileName)));
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);
            _logger.LogInformation("ContainerClient {ContainerName} récupéré", containerClient.Name);

            var blobClient = containerClient.GetBlobClient(fileName);

            var contentStream = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Lecture du fichier {FileName}", fileName);

            return new FileStreamResult(contentStream.Value.Content, "application/pdf");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new NotFoundException(Errors.NotFoundFileNameCode, string.Format(Errors.NotFoundFileNameMessage, fileName));
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex.InnerException, ex.Message);

            return null;
        }
    }
}
