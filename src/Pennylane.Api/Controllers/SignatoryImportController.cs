using Microsoft.AspNetCore.Mvc;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.Signatory;
using Pennylane.Application.Requests;
using Pulse.ExceptionMiddleware.Model;

namespace Pennylane.WebApi.Controllers;

/// <summary>
/// Contrôleur pour l'import des signataires Pennylane.
/// </summary>
/// <remarks>
/// Initialise une nouvelle instance de <see cref="SignatoryImportController"/>.
/// </remarks>
/// <param name="signatairesImportService">Service d'import des signataires.</param>
/// <param name="logger"></param>
[ApiController]
[Route("api/pennylane")]
public class SignatoryImportController(ISignatoryAccessesImportService signatairesImportService,
    ILogger<SignatoryImportController> logger) : ControllerBase
{
    private readonly ISignatoryAccessesImportService _signatoryImportService = signatairesImportService;
    private readonly ILogger<SignatoryImportController> _logger = logger;

    /// <summary>
    /// Importe un fichier CSV de signataires Pennylane.
    /// </summary>
    /// <param name="file">Fichier CSV contenant les signataires.</param>
    /// <returns>Résultat de l'import avec détails des succès et erreurs.</returns>
    [HttpPost("signatories/import")]
    [ProducesResponseType(typeof(SignatoryAccessImportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportSignatoriesAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            _logger.LogWarning("[SignatoryImportController] Tentative d'upload sans fichier");
            return BadRequest("Le fichier est obligatoire.");
        }

        _logger.LogInformation("[SignatoryImportController] Réception fichier {FileName} ({Size} bytes)", file.FileName, file.Length);

        var result = await _signatoryImportService.ImportAsync(file);

        return Ok(result);
    }

    /// <summary>
    /// Vérifie si l'utilisateur connecté a accès à Pennylane sur le dossier en cours.
    /// </summary>
    /// <param name="currentUserEmail">L'email de l'utilisateur connecté.</param>
    /// <param name="contactType">Le type de l'utilisateur connecté.</param>
    /// <param name="accountId">L'identifiant technique du dossier.</param>
    /// <returns>L'autorisation ou non d'accéder à Pennylane.</returns>
    [HttpGet("accounts/{accountId}/access")]
    [ProducesResponseType(typeof(SignatoryAccess), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SignatoryAccess>> CheckPennylaneAccessAsync([FromHeader(Name = "ContactEmail")] string currentUserEmail, [FromHeader(Name = "ContactType")] string contactType, int accountId)
    {
        var result = await _signatoryImportService.CheckPennylaneAccessAsync(currentUserEmail, contactType, accountId);
        return Ok(result);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="currentUserEmail">L'email de l'utilisateur connecté.</param>
    /// <param name="contactType">Le type de l'utilisateur connecté.</param>
    /// <returns>L'autorisation ou non d'accéder à Pennylane.</returns>
    [HttpGet("access")]
    [ProducesResponseType(typeof(SignatoryAccess), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SignatoryAccess>> CheckPennylaneAccessAsync([FromHeader(Name = "ContactEmail")] string currentUserEmail, [FromHeader(Name = "ContactType")] string contactType)
    {
        var result = await _signatoryImportService.CheckPennylaneAccessAsync(currentUserEmail, contactType, null);
        return Ok(result);
    }

    /// <summary>
    /// Accepter ou refuser les CGU Pennylane pour un ou plusieurs dossiers.
    /// </summary>
    /// <param name="currentUserEmail">L'email de l'utilisateur connecté.</param>
    /// <param name="request">Le détails de la requête.</param>
    /// <returns>Le résultat de la demande.</returns>
    [HttpPost("validation")]
    [ProducesResponseType(typeof(IEnumerable<SignatoryValidationHistory>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<SignatoryValidationHistory>>> ValidatePennylaneTermsAsync([FromHeader(Name = "ContactEmail")] string currentUserEmail, [FromBody] PennylaneSignatoryValidationRequest request)
    {
        var result = await _signatoryImportService.ValidatePennylaneTermsAsync(currentUserEmail, request);
        return Ok(result);
    }

    /// <summary>
    /// Récupère le contenu des CGU Pennylane pour le dossier concerné, au format PDF.
    /// </summary>
    /// <param name="currentUserEmail">L'email de l'utilisateur connecté.</param>
    /// <returns>Le contenu des CGU Pennylane.</returns>
    [HttpGet("terms/content")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<FileStreamResult> GetLastestTermsContentAsync([FromHeader(Name = "ContactEmail")] string currentUserEmail)
    {
        var result =  await _signatoryImportService.GetLatestTermsContentAsync(currentUserEmail);

        return File(result.FileStream, "application/pdf", true);
    }
}
