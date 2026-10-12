using System.ComponentModel.DataAnnotations.Schema;
using CsvHelper.Configuration.Attributes;

namespace Pennylane.Application.Models.Signatory;

/// <summary>
/// Ligne d'import de signataire Pennylane issue du fichier Excel.
/// </summary>
public class SignatoryAccessImportRow
{
    /// <summary>
    /// Code dossier.
    /// </summary>
    [Name("Code dossier")]
    public string AccountNumber { get; set; } = null!; 

    /// <summary>
    /// Email du signataire.
    /// </summary>
    [Name("Email signataire")]
    public string? Email { get; set; }

    /// <summary>
    /// Nom du signataire.
    /// </summary>
    [Name("Nom signataire")]
    public string? LastName { get; set; }

    /// <summary>
    /// Prénom du signataire.
    /// </summary>
    [Name("Prénom signataire")]
    public string? FirstName { get; set; }

    /// <summary>
    /// Nom du plan Pennylane.
    /// </summary>
    [Name("Nom du plan Pennylane")]
    public string? PlanName { get; set; }

    /// <summary>
    /// Nombre d'utilisateurs Pennylane.
    /// </summary>
    [Name("Nombre d'utilisateurs Pennylane")]
    public int? UserCount { get; set; }

    /// <summary>
    /// Prix HT du plan Pennylane.
    /// </summary>
    [Name("Prix HT du plan Pennylane")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal? PlanPriceExclTax { get; set; }

    /// <summary>
    /// Prix HT de la mise en conformité.
    /// </summary>
    [Name("Prix HT de la mise en conformité")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal? CompliancePriceExclTax { get; set; }

    /// <summary>
    /// Date de fin de gratuité.
    /// </summary>
    [Name("Date de fin de gratuité")]
    public string? FreeTrialEndDate { get; set; }

    /// <summary>
    /// Vague : type de dossier (vide = habituel, Restauration = dossier foodservice).
    /// Valeurs acceptées : null, vide ou "Restauration" (case-insensitive).
    /// </summary>
    [Name("Vague")]
    public string? Wave { get; set; }
}
