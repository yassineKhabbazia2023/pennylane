// pyright file="SignatoryAccessImportHelper.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Http;
using Pennylane.Application.Configuration;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Consts;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Models.Signatory;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Domain.Enums;
using System.Globalization;

namespace Pennylane.Application.Helpers;

/// <summary>
/// Helper pour l'import des signataires depuis un fichier CSV.
/// </summary>
public static class SignatoryAccessImportHelper
{
    /// <summary>
    /// Parse et valide un fichier CSV.
    /// </summary>
    public static async Task<List<SignatoryAccessImportRow>> ParseCsvFile(IFormFile file)
    {
        List<(SignatoryAccessImportRow, int, string[])> csvData;

        using var stream = file.OpenReadStream();

        try
        {
            csvData = CsvFileReader.ReadStreamAsync<SignatoryAccessImportRow>(stream, delimeter: ";").ToList();
        }
        catch (InvalidDataException ex)
        {
            throw new HeaderValidationException($"CSV invalide (virgule obligatoire) : {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new HeaderValidationException("Erreur lors de la lecture du CSV: " + ex.Message, ex);
        }

        // Validation stricte du format
        if (!SignatoryAccessCsvConfig.IsValidCsvFormat(
            csvData,
            typeof(SignatoryAccessImportRow),
            out var messageError))
        {
            throw new HeaderValidationException(messageError);
        }

        // Retourner UNIQUEMENT les lignes de données (exclure le header)
        // La première ligne (index 0) est le header avec Item1 = null
        return await Task.FromResult(
            csvData
                .Skip(1)  //   Ignorer la première ligne (header)
                .Where(d => d.Item1 != null)  //   Sécurité : filtrer les nulls
                .Select(d => d.Item1)
                .ToList()
        );
    }

    /// <summary>
    /// Valide une ligne de données et retourne une liste d'erreurs.
    /// </summary>
    public static List<SignatoryAccessImportErrorDto> ValidateRow(SignatoryAccessImportRow row, int rowNumber, IEnumerable<DateOnly> allowedDates)
    {
        var errors = new List<SignatoryAccessImportErrorDto>();

        // AccountNumber obligatoire
        if (string.IsNullOrWhiteSpace(row.AccountNumber))
        {
            errors.Add(new SignatoryAccessImportErrorDto { RowNumber = rowNumber, ColumnName = "Code dossier", ErrorMessage = "Le Code dossier est une colonne obligatoire.", RowData = row });
        }
        else if (row.AccountNumber.Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new SignatoryAccessImportErrorDto { RowNumber = rowNumber, ColumnName = "Code dossier", ErrorMessage = "Le Code dossier ne peut pas être 'NULL'.", RowData = row });
        }

        // Validation Email si présent
        if (!string.IsNullOrWhiteSpace(row.Email) && !row.Email.Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsValidEmail(row.Email))
            {
                errors.Add(new SignatoryAccessImportErrorDto { RowNumber = rowNumber, ColumnName = "Email signataire", ErrorMessage = "Email invalide.", RowData = row });
            }
        }

        // Validation des prix (doivent être positifs si renseignés)
        if (row.PlanPriceExclTax.HasValue && row.PlanPriceExclTax < 0)
        {
            errors.Add(new SignatoryAccessImportErrorDto { RowNumber = rowNumber, ColumnName = "Prix HT du plan Pennylane", ErrorMessage = "Le prix HT du plan ne peut pas être négatif.", RowData = row });
        }

        if (row.CompliancePriceExclTax.HasValue && row.CompliancePriceExclTax < 0)
        {
            errors.Add(new SignatoryAccessImportErrorDto { RowNumber = rowNumber, ColumnName = "Prix HT de la mise en conformité", ErrorMessage = "Le prix HT de conformité ne peut pas être négatif.", RowData = row });
        }

        // Validation du nombre d'utilisateurs
        if (row.UserCount.HasValue && row.UserCount < 0)
        {
            errors.Add(new SignatoryAccessImportErrorDto { RowNumber = rowNumber, ColumnName = "Nombre d'utilisateurs Pennylane", ErrorMessage = "Le nombre d'utilisateurs ne peut pas être négatif.", RowData = row });
        }

        var hasWave = !string.IsNullOrWhiteSpace(row.Wave);
        var hasFreeTrialEndDate = !string.IsNullOrWhiteSpace(row.FreeTrialEndDate);

        // Validation : FreeTrialEndDate et Wave ne peuvent pas être tous les deux vides
        if (!hasFreeTrialEndDate && !hasWave)
        {
            errors.Add(new SignatoryAccessImportErrorDto
            {
                RowNumber = rowNumber,
                ColumnName = "Date de fin de gratuité/Vague",
                ErrorMessage = "La date de fin de gratuité ou la vague doit être renseignée.",
                RowData = row
            });
        }

        // Validation de la date de fin de gratuité (si renseignée, doit être dans allowedDates)
        if (hasFreeTrialEndDate && !IsAllowDate(allowedDates, row.FreeTrialEndDate!))
        {
            errors.Add(new SignatoryAccessImportErrorDto
            {
                RowNumber = rowNumber,
                ColumnName = "Date de fin de gratuité",
                ErrorMessage = "Date de fin de gratuité non correcte.",
                RowData = row
            });
        }

        // Validation de la colonne Wave (si renseignée, doit correspondre à une valeur de WaveType)
        if (hasWave && !IsValideWave(row.Wave!))
        {
            errors.Add(new SignatoryAccessImportErrorDto
            {
                RowNumber = rowNumber,
                ColumnName = "Vague",
                ErrorMessage = $"La valeur de la colonne Vague est invalide. Valeurs autorisées : vide ou {nameof(Domain.Enums.WaveType.Restauration)}.",
                RowData = row
            });
        }

        return errors;
    }

    public static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email && email.Contains('.');
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ajoute une erreur au résultat d'import.
    /// </summary>
    public static void AddError(
        this SignatoryAccessImportResponse result,
        int rowNumber,
        string errorMessage,
        string? columnName,
        SignatoryAccessImportRow? rowData)
    {
        result.Errors.Add(new SignatoryAccessImportErrorDto
        {
            RowNumber = rowNumber,
            ColumnName = columnName ?? string.Empty,
            ErrorMessage = errorMessage,
            RowData = rowData
        });
    }

    /// <summary>
    /// Vérifie si une entité a des changements par rapport au DTO.
    /// </summary>
    public static bool HasChanges(
         SignatoryAccessEntity existing,
         SignatoryAccessImportRow dto)
    {
        DateTime? dtoFreeTrialEndDate = DateTime.TryParseExact(dto.FreeTrialEndDate, GlobalConstants.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
            ? parsedDate
            : null;

        return !AreEqual(existing.Email, dto.Email)
               || !AreEqual(existing.LastName, dto.LastName)
               || !AreEqual(existing.FirstName, dto.FirstName)
               || !AreEqual(existing.PlanName, dto.PlanName)
               || existing.UserCount != dto.UserCount
               || existing.PlanPriceExclTax != dto.PlanPriceExclTax
               || existing.CompliancePriceExclTax != dto.CompliancePriceExclTax
               || existing.FreeTrialEndDate != dtoFreeTrialEndDate
               || !AreEqual(existing.Wave, dto.Wave);
    }

    /// <summary>
    /// Compare deux strings en gérant correctement les null, les chaînes vides et "NULL".
    /// Les chaînes vides, nulles et "NULL" sont considérées comme équivalentes.
    /// </summary>
    private static bool AreEqual(string? a, string? b)
    {
        // Normaliser les valeurs vides : traiter "", null et "NULL" comme équivalents
        var normalizedA = string.IsNullOrEmpty(a) || string.Equals(a, "NULL", StringComparison.OrdinalIgnoreCase) ? null : a;
        var normalizedB = string.IsNullOrEmpty(b) || string.Equals(b, "NULL", StringComparison.OrdinalIgnoreCase) ? null : b;

        if (normalizedA is null && normalizedB is null) return true;
        if (normalizedA is null || normalizedB is null) return false;
        return normalizedA == normalizedB;
    }

    private static bool IsAllowDate(IEnumerable<DateOnly> allowedDates, string freeTrialEndDate)
    {
        if (!allowedDates.Any())
        {
            return false;
        }

        if (!DateOnly.TryParseExact(freeTrialEndDate, GlobalConstants.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return false;
        }

        return allowedDates.Contains(parsedDate);
    }

    public static bool IsValideWave(string wave)
    {
        var waveTypeList = Enum.GetValues<WaveType>().Select(x => x.ToString()).ToList();

        return waveTypeList.Contains(wave);
    }

    public static void ValidateEmailFreeTrialConsistency(IEnumerable<SignatoryAccessImportRow> rows, SignatoryAccessImportResponse result)
    {
        var emailGroups = rows
        .Select((row, index) => new { Row = row, LineNumber = index + 2 })
        .Where(x => !string.IsNullOrWhiteSpace(x.Row.Email) &&
                    !x.Row.Email.Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase))
        .GroupBy(x => x.Row.Email!.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in emailGroups)
        {
            var distinctDates = group
                .Select(x => DateOnly.TryParseExact(x.Row.FreeTrialEndDate, GlobalConstants.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate) ? parsedDate : (DateOnly?)null)
                .Distinct()
                .ToList();

            if (distinctDates.Count > 1)
            {
                foreach (var item in group)
                {
                    result.AddError(
                        item.LineNumber,
                        "Plusieurs lignes avec le même email doivent avoir la même date de fin de gratuité.",
                        "Date de fin de gratuité",
                        item.Row);
                }
            }
        }
    }

    public static bool? IsFreeTrialExpired(string? freeTrialEndDate, bool? canAccess, FreeTrialInfoOptions freeTrialOptions, string? wave)
    {
        if (canAccess == true || !string.IsNullOrWhiteSpace(wave))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(freeTrialEndDate))
        {
            return false;
        }

        return IsFreeTrialExpiredByDate(freeTrialEndDate, freeTrialOptions);
    }

    /// <summary>
    /// Indique si la période de gratuité est expirée pour une date donnée, indépendamment de l'accès du signataire.
    /// </summary>
    /// <param name="freeTrialEndDate">Date au format DD/MM/YYYY (obligatoire).</param>
    /// <param name="freeTrialOptions">Options de configuration des périodes de gratuité.</param>
    /// <returns>true si la période de gratuité est expirée; false sinon.</returns>
    /// <exception cref="ArgumentException">Si freeTrialEndDate est null ou vide.</exception>
    public static bool? IsFreeTrialExpiredByDate(string? freeTrialEndDate, FreeTrialInfoOptions freeTrialOptions)
    {
        if (freeTrialEndDate == null)
        {
            return null;
        }

        var parsedDate = freeTrialEndDate.Replace("/", string.Empty);

        return freeTrialOptions.TermsOverridesAfterDate.TryGetValue(parsedDate, out var termsOverride)
            && DateTime.UtcNow >= DateTime.ParseExact(termsOverride.EffectiveFromUtc, GlobalConstants.DateFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Valide que toutes les lignes d'un même client (email) ont la même vague.
    /// Ajoute une erreur au résultat si les vagues sont incohérentes.
    /// </summary>
    public static void ValidateWaveConsistency(IEnumerable<SignatoryAccessImportRow> rows, SignatoryAccessImportResponse result)
    {
        var rowsByEmail = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Email))
            .GroupBy(r => r.Email, StringComparer.OrdinalIgnoreCase);

        foreach (var group in rowsByEmail)
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            var distinctWaves = group
                .Select(r => r.Wave)
                .Distinct()
                .ToList();

            if (distinctWaves.Count > 1)
            {
                var waveDescriptions = distinctWaves
                    .Select(w => w == null ? "habituel (null)" : "Restauration")
                    .Distinct()
                    .ToList();

                result.AddError(
                    0,
                    string.Format(Errors.InconsistentWavesMessage, string.Join(", ", waveDescriptions)),
                    "Vague",
                    null);
            }
        }
    }
}
