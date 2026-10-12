using Pennylane.Application.Interfaces;

namespace Pennylane.Infrastructure.ExternalServices;

public class DocumentStreamService : IDocumentStreamService
{
    private readonly HttpClient _httpClient;

    public DocumentStreamService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Stream> GetStreamFromUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));
        }

        try
        {
            // Fetch the content from the URL
            var response = await _httpClient.GetAsync(url);

            // Ensure the request was successful
            response.EnsureSuccessStatusCode();

            // Return the content as a stream
            return await response.Content.ReadAsStreamAsync();
        }
        catch (HttpRequestException ex)
        {
            // Log the exception details here if needed
            Console.WriteLine($"Error fetching stream from URL: {ex.Message}");
            throw; // Re-throw the exception to handle it in the calling method
        }
    }
}