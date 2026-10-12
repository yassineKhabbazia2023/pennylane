using Pennylane.Application.Consts;
using Pennylane.Application.Helpers;
using Pennylane.Application.Models.Signatory;
using Pennylane.Domain.Entities.Pennylane;
using System.Globalization;

namespace Pennylane.Application.Mappers;

/// <summary>
/// Mapper utilitaire pour projeter les DTO d'import de signataires
/// vers l'entité de persistance SignatoryAccessEntity.
/// Gère à la fois la création et la mise à jour lors des imports successifs.
/// </summary>
public static class SignatoryAccessImportEntityMapper
{
    /// <summary>
    /// Crée une nouvelle entité SignatoryAccessEntity à partir
    /// d'une ligne d'import et des informations techniques d'import (batch, horodatage).
    /// </summary>
    public static SignatoryAccessEntity ToEntity(SignatoryAccessImportRow row, int termsId)
    {
        return new SignatoryAccessEntity
        {
            AccountNumber = row.AccountNumber,
            Email = row.Email,
            LastName = row.LastName,
            FirstName = row.FirstName,
            PlanName = row.PlanName,
            UserCount = row.UserCount,
            PlanPriceExclTax = row.PlanPriceExclTax,
            CompliancePriceExclTax = row.CompliancePriceExclTax,
            UpdatedDate = null,
            FreeTrialEndDate = string.IsNullOrWhiteSpace(row.FreeTrialEndDate) ? null : DateTime.ParseExact(row.FreeTrialEndDate, GlobalConstants.DateFormat, CultureInfo.InvariantCulture),
            TermsId = termsId,
            Wave = string.IsNullOrWhiteSpace(row.Wave) ? null : row.Wave,
        };
    }

    public static SignatoryAccessImportRow ToImportRow(this SignatoryAccessEntity entity)
    {
        return new SignatoryAccessImportRow
        {
            AccountNumber = entity.AccountNumber,
            Email = entity.Email,
            LastName = entity.LastName,
            FirstName = entity.FirstName,
            PlanName = entity.PlanName,
            UserCount = entity.UserCount,
            PlanPriceExclTax = entity.PlanPriceExclTax,
            CompliancePriceExclTax = entity.CompliancePriceExclTax,
            FreeTrialEndDate = entity.FreeTrialEndDate?.ToString(GlobalConstants.DateFormat, CultureInfo.InvariantCulture),
            Wave = entity.Wave,
        };
    }

    /// <summary>
    /// Applique les nouvelles valeurs issues d'une ligne d'import
    /// sur une entité existante, dans le cadre d'un comportement d'upsert.
    /// </summary>
    public static void ApplyUpdate(SignatoryAccessEntity existing, SignatoryAccessImportRow row, DateTime updatedDateUtc, int termsId)
    {
        existing.Email = row.Email;
        existing.LastName = row.LastName;
        existing.FirstName = row.FirstName;
        existing.PlanName = row.PlanName;
        existing.UserCount = row.UserCount;
        existing.PlanPriceExclTax = row.PlanPriceExclTax;
        existing.CompliancePriceExclTax = row.CompliancePriceExclTax;
        existing.UpdatedDate = updatedDateUtc;
        existing.FreeTrialEndDate = string.IsNullOrWhiteSpace(row.FreeTrialEndDate) ? null : DateTime.ParseExact(row.FreeTrialEndDate, GlobalConstants.DateFormat, CultureInfo.InvariantCulture);
        existing.TermsId = termsId;
        existing.Wave = string.IsNullOrWhiteSpace(row.Wave) ? null : row.Wave;
    }

    public static SignatoryAccess MapToSignatoryAccessModel(
        SignatoryAccessEntity currentEntity,
        IEnumerable<Tuple<SignatoryAccessEntity,string>> userEntities,
        string currentUserEmail,
        bool getDetails = false)
    {
        var isCurrentUserWithoutAccess = currentEntity.CanAccess != true && string.Equals(currentEntity.Email, currentUserEmail, StringComparison.OrdinalIgnoreCase);
        var wave = currentEntity.Wave;

        return new SignatoryAccess
        {
            CanAccess = currentEntity.CanAccess ?? false,
            IsPennylaneSignatory = SetPennylaneSignatory(currentEntity, currentUserEmail),
            Details = isCurrentUserWithoutAccess || getDetails ? new SignatoryDetails
            {
                Terms = MapToTermsModel(currentEntity.Terms),
                Accounts = MapToAccountsModel(userEntities),
            } : null,
            FreeTrialEndDate = string.IsNullOrWhiteSpace(wave) ? currentEntity.FreeTrialEndDate : null,
            BillingMonth = isCurrentUserWithoutAccess && string.IsNullOrWhiteSpace(wave) ? GetBillingMonth(currentEntity.FreeTrialEndDate) : null,
            Wave = wave,
        };
    }

    private static string? GetBillingMonth(DateTime? freeTrialEndDate)
    {
        if (!freeTrialEndDate.HasValue)
        {
            return null;
        }

        var nextMonth = freeTrialEndDate.Value.AddMonths(1);
        return nextMonth.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"));
    }

    private static bool? SetPennylaneSignatory(SignatoryAccessEntity currentEntity, string currentUserEmail)
    {
        if (!currentEntity.Email.Equals(currentUserEmail, StringComparison.OrdinalIgnoreCase))
        {
            return currentEntity.CanAccess == true ? null : false;
        }
        
        return true;
    }

    public static Terms MapToTermsModel(TermsEntity currentEntityTerms)
    {
        return new Terms
        {
            Id = currentEntityTerms.Id,
            LinkFr = currentEntityTerms.LinkFr,
        };
    }

    public static IEnumerable<SignatoryAccount> MapToAccountsModel(IEnumerable<Tuple<SignatoryAccessEntity, string>> userEntities)
    {
        return userEntities.Select(e => new SignatoryAccount
        {
            AccountNumber = e.Item1.AccountNumber,
            LegalName = e.Item2,
            PlanName = e.Item1.PlanName,
            PlanPriceExclTax = e.Item1.PlanPriceExclTax,
            CompliancePriceExclTax = e.Item1.CompliancePriceExclTax,
        });
    }
}
