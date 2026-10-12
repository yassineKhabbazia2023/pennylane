namespace Pennylane.Application.Interfaces;

public interface IDocumentStreamService
{
    Task<Stream> GetStreamFromUrlAsync(string url);
}