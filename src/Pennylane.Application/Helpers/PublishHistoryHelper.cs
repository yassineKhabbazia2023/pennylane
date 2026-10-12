using Pennylane.Application.Consts;
using Pennylane.Application.Models.Signatory;
using Pennylane.Domain.Entities.Pennylane;
using System.Globalization;
using System.Text;

namespace Pennylane.Application.Helpers;

public static class PublishHistoryHelper
{
    public static string BuildAmendmentBaseDetails(SignatoryAccessImportRow row)
    {
        return $"Nom du plan Pennylane : {FormatValue("Nom du plan Pennylane", row.PlanName)}\r\n" +
            $"Nombre d'utilisateurs : {FormatValue("Nombre d'utilisateurs", row.UserCount)}\r\n" +
            $"Prix HT du plan Pennylane : {FormatValue("Prix HT du plan Pennylane", row.PlanPriceExclTax)} EUR\r\n" +
            $"Prix de la mise en conformité : {FormatValue("Prix de la mise en conformité", row.CompliancePriceExclTax)} EUR\r\n" +
            $"Date de fin de gratuité : {FormatValue("Date de fin de gratuité", string.IsNullOrWhiteSpace(row.FreeTrialEndDate) ? null : row.FreeTrialEndDate)}\r\n" +
            $"Vague : {FormatValue("Vague", string.IsNullOrWhiteSpace(row.Wave) ? null : row.Wave)}\r\n";
    }

    public static string BuildAmendmentUpdatedDetails(SignatoryAccessEntity oldRow, SignatoryAccessImportRow newRow)
    {
        var details = new StringBuilder();

        details.AppendIfChanged("Email signataire", oldRow.Email, newRow.Email);
        details.AppendIfChanged("Nom signataire", oldRow.LastName, newRow.LastName);
        details.AppendIfChanged("Prénom signataire", oldRow.FirstName, newRow.FirstName);
        details.AppendIfChanged("Nom du plan Pennylane", oldRow.PlanName, newRow.PlanName);
        details.AppendIfChanged("Nombre d'utilisateurs", oldRow.UserCount, newRow.UserCount);
        details.AppendIfChanged("Prix HT du plan Pennylane", oldRow.PlanPriceExclTax, newRow.PlanPriceExclTax);
        details.AppendIfChanged("Prix de la mise en conformité", oldRow.CompliancePriceExclTax, newRow.CompliancePriceExclTax);
        details.AppendIfChanged("Date de fin de gratuité", oldRow.FreeTrialEndDate.HasValue ? DateOnly.FromDateTime(oldRow.FreeTrialEndDate.Value) : null,
            string.IsNullOrWhiteSpace(newRow.FreeTrialEndDate) ? null : DateOnly.ParseExact(newRow.FreeTrialEndDate, GlobalConstants.DateFormat, CultureInfo.InvariantCulture));
        details.AppendIfChanged("Vague", oldRow.Wave, newRow.Wave);

        return details.ToString().TrimEnd();
    }

    private static readonly HashSet<string> FeminineFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Date de fin de gratuité",
        "Vague"
    };

    private static void AppendIfChanged(this StringBuilder currentDetails, string fieldName, object? oldValue, object? newValue)
    {
        if (!Equals(oldValue, newValue))
        {
            var oldDisplay = FormatValue(fieldName, oldValue);
            var newDisplay = FormatValue(fieldName, newValue);
            currentDetails.AppendLine($"{fieldName} : {oldDisplay} a été remplacé par {newDisplay}");
        }
    }

    private static string FormatValue(string fieldName, object? value)
    {
        if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
        {
            var article = FeminineFieldNames.Contains(fieldName) ? "Aucune" : "Aucun";
            var fieldNameLowerFirst = char.ToLower(fieldName[0]) + fieldName[1..];
            return $"{article} {fieldNameLowerFirst}";
        }

        return value.ToString() ?? string.Empty;
    }
}
