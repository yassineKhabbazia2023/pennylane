using Microsoft.AspNetCore.Mvc;

namespace Pennylane.Application.Interfaces;

public interface IBlobStorageManager
{
    Task<FileStreamResult?> GetFileAsync(string fileName, CancellationToken cancellationToken = default);
}
