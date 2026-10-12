using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.Pagers;
using Pennylane.WebApi.Models;
using Pulse.ExceptionMiddleware.Exceptions;
using System.ComponentModel.DataAnnotations;

namespace Pennylane.WebApi.Controllers;

/// <summary>
/// Document Controller.
/// </summary>
[Route("api/documents")]
[ApiController]
public class DocumentController(IDocumentService documentService, ISyncService syncService) : ControllerBase
{
    // Single source of truth for the cap: declared on the request model so the contract is
    // discoverable by the front (and enforced by model validation) as well as here.
    private const int MaxDocumentsPerArchive = DownloadDocumentsRequest.MaxDocumentsPerArchive;

    private const string RydgeConseilArchiveNameFormat = "documents-rydge-conseil_{0}.zip";

    /// <summary>
    /// Get documents.
    /// </summary>
    /// <param name="accountNumber"></param>
    /// <param name="search"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    [HttpGet]
    [ProducesResponseType<DocumentsPagination>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDocumentsAsync(
        [FromQuery, Required(AllowEmptyStrings = false)] string accountNumber,
        [FromQuery] string? search = null,
        [FromQuery, Range(1, 10_000)] int page = 1,
        [FromQuery, Range(1, 500)] int pageSize = 4)
    {
        var accountDocumentsPage = await documentService.GetDocumentsAsync(accountNumber, page, pageSize, search);

        //Vérification non bloquante de la date de dernière synchronisation
        await syncService.CheckAndPushSyncMessageAsync(accountNumber);

        return Ok(DocumentsPagination.Create(accountDocumentsPage));
    }

    /// <summary>
    /// Get document content by id.
    /// </summary>
    /// <param name="accountNumber">The account number.</param>
    /// <param name="id">The document id.</param>
    /// <returns>The document content as a stream.</returns>
    [HttpGet("{id}/content")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentContentAsync(
        [FromQuery, Required(AllowEmptyStrings = false)] string accountNumber,
        long id)
    {
        try
        {
            // Call the service method to get the document content as a stream
            var fileStream = await documentService.GetDocumentContentStreamAsync(accountNumber, id);
    
            if (fileStream == null)
            {
                return NotFound("Failed to retrieve the document content.");
            }
    
            // Return the file content as a FileStreamResult
            return new FileStreamResult(fileStream, "application/octet-stream")
            {
                FileDownloadName = id.ToString() // Optionally set the file name for download
            };
        }
        catch (BadRequestException)
        {
            return NotFound();
        }
        catch (RequestHttpException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }


    /// <summary>
    /// Purge (hard delete) all documents for an account.
    /// </summary>
    /// <param name="accountNumber">The account number to purge.</param>
    /// <returns>Number of deleted documents.</returns>
    [HttpDelete("purge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PurgeDocumentsAsync([FromQuery, Required(AllowEmptyStrings = false)] string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            return BadRequest("accountNumber is required.");
        }

        try
        {
            const int pageSize = 100;
            var page = 1;
            var allIds = new List<long>();

            while (true)
            {
                var pageResult = await documentService.GetDocumentsAsync(accountNumber, page, pageSize, applyExtensionFilter: false);
                if (pageResult?.Documents is not { Count: > 0 })
                {
                    break;
                }

                allIds.AddRange(pageResult.Documents.Select(d => d.Id));

                if (pageResult.DocumentsCount <= page * pageSize)
                {
                    break;
                }

                page++;
            }

            if (allIds.Count == 0)
            {
                return Ok(new { deleted = 0 });
            }

            var deleted = await documentService.HardDeleteDocumentsAsync(accountNumber, allIds);
            return Ok(new { deleted });
        }
        catch (Exception ex)
        {
            // Let service layer log more details; surface a 500 while keeping controller simple.
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    /// <summary>
    /// Download multiple documents as a single zip archive.
    /// </summary>
    /// <param name="accountNumber">The account number.</param>
    /// <param name="request">The identifiers of the documents to download.</param>
    /// <returns>A zip archive containing the requested documents.</returns>
    [HttpPost("archive")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocumentsArchiveAsync(
        [FromQuery, Required(AllowEmptyStrings = false)] string accountNumber,
        [FromBody] DownloadDocumentsRequest request)
    {
        if (request?.Ids == null || request.Ids.Count == 0)
        {
            return Problem(detail: "Documents identifiers are empty or not valid.", statusCode: StatusCodes.Status400BadRequest, title: "InvalidParameter");
        }

        var distinctIds = request.Ids.Distinct().ToList();

        if (distinctIds.Count > MaxDocumentsPerArchive)
        {
            return Problem(detail: $"A maximum of {MaxDocumentsPerArchive} documents can be downloaded per archive.", statusCode: StatusCodes.Status400BadRequest, title: "TooManyDocuments");
        }

        var archiveStream = await documentService.GetDocumentsArchiveStreamAsync(accountNumber, distinctIds);

        // No requested id matched a document: the service returns Stream.Null. Returning it as a
        // file would hand the user a 0-byte ".zip" no tool can open, so answer 404 instead.
        if (archiveStream is null || ReferenceEquals(archiveStream, Stream.Null))
        {
            return Problem(detail: "No document found for the requested identifiers.", statusCode: StatusCodes.Status404NotFound, title: "DocumentsNotFound");
        }

        return new FileStreamResult(archiveStream, "application/x-zip-compressed")
        {
            FileDownloadName = string.Format(RydgeConseilArchiveNameFormat, DateTime.UtcNow.ToString("dd-MM-yyyy")),
        };
    }
}
