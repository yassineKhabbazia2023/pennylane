using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Application.Interfaces;

/// <summary>
/// Contrat d'accès aux données pour la table fonctionnelle des signataires Pennylane
/// (amend.SignatoryAccess). Gère l'insertion et la mise à jour des droits d'accès.
/// </summary>
public interface ISignatoryAccessesImportRepository
{
    /// <summary>
    /// Ajoute en mémoire un enregistrement de droits d'accès signataire.
    /// La ligne n'est réellement écrite en base qu'au prochain appel à <see cref="SaveChangesAsync"/>.
    /// </summary>
    /// <param name="entity">Entité représentant les droits d'accès d'un signataire Pennylane.</param>
    Task InsertSignatoryAccessAsync(SignatoryAccessEntity entity);

    /// <summary>
    /// Persiste en base toutes les modifications en attente (insert / update).
    /// </summary>
    Task SaveChangesAsync();

    /// <summary>
    /// Récupère l'ensemble des enregistrements de droits d'accès signataires.
    /// </summary>
    Task<List<SignatoryAccessEntity>> GetAllSignatoryAccessAsync();

    /// <summary>
    /// Marque une entité existante comme modifiée dans le contexte de persistance,
    /// afin que ses nouvelles valeurs soient écrites en base au prochain
    /// appel à <see cref="SaveChangesAsync"/>.
    /// </summary>
    /// <param name="entity">Entité existante à mettre à jour.</param>
    void UpdateSignatoryAccess(SignatoryAccessEntity entity);

    Task<SignatoryAccessEntity> GetSignatoryAccessByAccountNumberAsync(string accountNumber);

    Task<IEnumerable<Tuple<SignatoryAccessEntity, string>>> GetSignatoryAccessesByUserEmailAsync(string userEmail);

    void ValidateTerms(string accountNumber, int termsId, bool status, SignatoryAccessEntity sourceData);

    /// <summary>
    /// Récupère l'identifiant des Terms correspondant au lien de fichier (LinkFr) fourni.
    /// </summary>
    /// <param name="linkFr">Nom/lien du fichier des Terms (LinkFr).</param>
    Task<int> GetTermsIdByLinkFrAsync(string linkFr);

    /// <summary>
    /// Supprime les enregistrements SignatoryAccess par leurs AccountNumbers.
    /// Exécute immédiatement un DELETE SQL batch via ExecuteDeleteAsync.
    /// </summary>
    Task DeleteSignatoryAccessesAsync(IEnumerable<string> accountNumbers);

    Task<IEnumerable<Tuple<SignatoryAccessEntity, string>>> GetSignatoryAccessesByAccountNumbersAsync(IEnumerable<string> accountNumbers);
}
