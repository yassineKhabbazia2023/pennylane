using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Exceptions;
using Pulse.ExceptionMiddleware.Exceptions;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Models.Pagers;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pennylane.Application.Extensions;
using InvalidOperationException = System.InvalidOperationException;

namespace Pennylane.Application.services;

public class PennylaneApiFirmService : IPennylaneApiFirmService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PennylaneApiFirmService> _logger;
    private readonly PennylaneFirmTokenOptions _firmTokenOptions;
    private const string code = "PNL0500";

    public PennylaneApiFirmService(HttpClient httpClient, ILogger<PennylaneApiFirmService> logger, IOptions<PennylaneFirmTokenOptions> firmTokenOptions)
    {
        _httpClient = httpClient;
        _logger = logger;
        _firmTokenOptions = firmTokenOptions.Value;
    }

    public async Task<List<PennylaneDmsFile>> GetCompanyDmsFilesAsync(string firmId, int pennylaneInternalCompanyId, string? fileId = null)
    {
        var token = GetTokenForFirm(firmId);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("No token found for firm {FirmId}.", firmId);
            throw new BadRequestException(code, $"No token found for firm {firmId}.");
        }
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        if (!string.IsNullOrEmpty(fileId))
        {
            return await GetSingleFileAsync(pennylaneInternalCompanyId, fileId);
        }
        else
        {
            return await GetAllFilesAsync(pennylaneInternalCompanyId);
        }
    }

    private async Task<List<PennylaneDmsFile>> GetSingleFileAsync(int pennylaneInternalCompanyId, string fileId)
    {
        var baseUrl = $"companies/{pennylaneInternalCompanyId}/dms/files";
        var filter = $"?filter=[{{ \"field\": \"id\", \"operator\": \"eq\", \"value\": \"{fileId}\" }}]";
        var url = baseUrl + filter;

        var (result, _) = await FetchFilesAsync(url);
        if (result == null || !result.Items.Any())
        {
            throw new BadRequestException(code, $"Failed to retrieve DMS file {fileId} for company {pennylaneInternalCompanyId}.");
        }

        return new List<PennylaneDmsFile> { result.Items.First() };
    }

    private async Task<List<PennylaneDmsFile>> GetAllFilesAsync(int pennylaneInternalCompanyId)
    {
        var allItems = new List<PennylaneDmsFile>();
        var baseUrl = $"companies/{pennylaneInternalCompanyId}/dms/files";
        string? nextCursor = null;
        do
        {
            var url = $"{baseUrl}?limit=100";
            if (!string.IsNullOrEmpty(nextCursor))
            {
                url += $"&cursor={Uri.EscapeDataString(nextCursor)}";
            }

            var (result, _) = await FetchFilesAsync(url);
            if (result == null)
            {
                throw new BadRequestException(code, $"Failed to retrieve DMS files for company {pennylaneInternalCompanyId}.");
            }

            allItems.AddRange(result.Items);
            nextCursor = result.HasMore ? result.NextCursor : null;
        } while (!string.IsNullOrEmpty(nextCursor));

        return allItems;
    }

    private async Task<(PennylaneDmsFilesResponse, HttpResponseMessage)> FetchFilesAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new RequestHttpException(response.StatusCode,
                    $"API request failed with status code: {response.StatusCode}");

            throw new BadRequestException(code, $"API request failed with status code: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<PennylaneDmsFilesResponse>();
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize the response.");
        }

        return (result, response);
    }

    public async Task<int?> GetPennylaneInternalIdByAccountNumberAsync(string firmId, string accountNumber)
    {
        var token = GetTokenForFirm(firmId);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("No token found for firm {FirmId}.", firmId);
            throw new BadRequestException(code, $"No token found for firm {firmId}.");
        }
        int currentPage = 1;
        int totalPages = 1;
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        do
        {
            var url = $"companies?per_page=1000&page={currentPage}";
            var (result, _) = await _httpClient.GetAndParseAsync<PennylaneCompanyFirmPager>(url);
            if (result == null || result.Items == null)
                return null;
            var match = result.Items.FirstOrDefault(x => x.ClientCode == accountNumber);
            if (match != null)
                return match.Id;
            totalPages = result.TotalPages;
            currentPage++;
        } while (currentPage <= totalPages);
        return null;
    }

    private string? GetTokenForFirm(string firmId)
    {
        return _firmTokenOptions.FirstOrDefault(f => f.Id == firmId)?.Tokens.FirstOrDefault();
    }
}
