namespace Pennylane.Infrastructure.Options;

public class BlobStorageOptions
{
    public string ContainerName { get; set; } = default!;

    public string BlobUri { get; set; } = default!;
}
