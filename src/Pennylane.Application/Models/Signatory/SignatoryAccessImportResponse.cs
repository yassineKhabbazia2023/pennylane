// <copyright file="SignatoryAccessImportResponse.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Models.Signatory;

/// <summary>
/// Résultat global d'un import de signataires Pennylane.
/// Contient les statistiques de traitement et, si nécessaire,
/// le détail des erreurs rencontrées ligne par ligne.
/// </summary>
public class SignatoryAccessImportResponse
{
    /// <summary>
    /// Nombre total de lignes lues dans le fichier (hors en-tête).
    /// </summary>
    public int TotalRows { get; set; }

    /// <summary>
    /// Nombre de lignes traitées avec succès (insert ou update).
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Nombre de lignes supprimées de la base (présentes en base mais absentes du fichier
    /// et sans historique de validation).
    /// </summary>
    public int DeletedCount { get; set; }

    /// <summary>
    /// Nombre de lignes en erreur (validation ou exception technique).
    /// </summary>
    public int ErrorCount => Errors.Count;

    /// <summary>
    /// Détail des erreurs par ligne, lorsque celles-ci sont remontées.
    /// La taille effective de cette liste peut être limitée par la logique
    /// d'import pour éviter des réponses JSON trop volumineuses.
    /// </summary>
    public List<SignatoryAccessImportErrorDto> Errors { get; set; } = new();
}
