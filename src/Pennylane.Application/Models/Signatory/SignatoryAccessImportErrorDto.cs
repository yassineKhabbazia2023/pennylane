namespace Pennylane.Application.Models.Signatory;

/// <summary>
/// Détail d'une erreur sur une ligne d'import de signataire.
/// </summary>
public class SignatoryAccessImportErrorDto
{
    /// <summary>
    /// Numéro de ligne dans le fichier (1‑based, en incluant ou non le header selon ton choix).
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Nom de la colonne concernée par l'erreur.
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Message d'erreur fonctionnelle ou technique.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Valeurs de la ligne concernée (optionnel, pratique pour debug / restitution).
    /// </summary>
    public SignatoryAccessImportRow? RowData { get; set; }
}