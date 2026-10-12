namespace Pennylane.Application.Models.Signatory;

public class SignatoryAccess
{
    public bool CanAccess { get; set; }

    public bool? IsPennylaneSignatory { get; set; }

    public SignatoryDetails? Details { get; set; }

    public DateTime? FreeTrialEndDate { get; set; }

    public string? BillingMonth { get; set; }

    public bool? IsFreeTrialExpired { get; set; }

    /// <summary>
    /// Vague : classification du dossier (null = habituel, "Restauration" = foodservice).
    /// Retourne la valeur brute de la colonne "Vague" du CSV.
    /// </summary>
    public string? Wave { get; set; }
}
