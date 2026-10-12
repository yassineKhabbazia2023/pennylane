namespace Pennylane.Application.Configurations.Options;

public class FreeTrialInfoOptions
{
    public const string SectionName = "FreeTrialInfo";

    public List<string> AllowedFreeTrialEndDates { get; set; } = [];

    public Dictionary<string, string> TermsByDate { get; set; } = new();

    /// <summary>
    /// Override des Terms par date de fin de gratuité (clé identique à <see cref="TermsByDate"/>, ex: "30062026").
    /// Si le formulaire n'a pas été accepté (CanAccess != true) et que la date d'effet (UTC) est dépassée,
    /// les Terms résolus basculent vers <see cref="TermsOverrideOptions.LinkFr"/>.
    /// </summary>
    public Dictionary<string, TermsOverrideOptions> TermsOverridesAfterDate { get; set; } = new();

    public Dictionary<string, string> TermsByWave { get; set; } = new();
}

public class TermsOverrideOptions
{
    /// <summary>
    /// Date et heure (UTC) à partir de laquelle l'override s'applique, au format "dd/MM/yyyy HH:mm".
    /// </summary>
    public string EffectiveFromUtc { get; set; } = null!;

    /// <summary>
    /// Nom (LinkFr) des Terms à utiliser une fois la date d'effet atteinte.
    /// </summary>
    public string LinkFr { get; set; } = null!;
}
