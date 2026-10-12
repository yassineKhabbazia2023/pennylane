using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pennylane.Application.Models.Signatory;
using Pennylane.Application.Requests;

namespace Pennylane.Application.Interfaces;

/// <summary>
/// Service métier pour l'import des signataires Pennylane
/// à partir d'un fichier (Excel/CSV).
/// </summary>
public interface ISignatoryAccessesImportService
{
    /// <summary>
    /// Importe les signataires à partir d'un fichier envoyé (Excel/CSV) :
    /// parse, validation et upsert en base.
    /// </summary>
    /// <param name="file">Fichier uploadé.</param>
    /// <returns>Résultat global de l'import (succès/erreurs).</returns>
    Task<SignatoryAccessImportResponse> ImportAsync(IFormFile file);

    Task<SignatoryAccess> CheckPennylaneAccessAsync(string currentUserEmail, string contactType, int? accountId);

    Task<IEnumerable<SignatoryValidationHistory>> ValidatePennylaneTermsAsync(string currentUserEmail, PennylaneSignatoryValidationRequest request);

    Task<FileStreamResult> GetLatestTermsContentAsync(string currentUserEmail);
}
