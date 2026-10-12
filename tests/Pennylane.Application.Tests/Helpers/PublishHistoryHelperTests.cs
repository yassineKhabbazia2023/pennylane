// pyright file="PublishHistoryHelperTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Helpers;
using Pennylane.Application.Models.Signatory;
using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Application.Tests.Helpers;

public class PublishHistoryHelperTests
{
    public PublishHistoryHelperTests()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo("fr-FR");
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = new System.Globalization.CultureInfo("fr-FR");
    }

    [Fact]
    public void BuildAmendmentActivatedDetails_ShouldReturnExpectedString()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            PlanName = "PlanA",
            UserCount = 5,
            PlanPriceExclTax = 100.5m,
            CompliancePriceExclTax = 20.0m,
            FreeTrialEndDate = "31/10/2026",
            Wave = "Restauration"
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentBaseDetails(row);

        // Assert
        var expected = "Nom du plan Pennylane : PlanA\r\n" +
                       "Nombre d'utilisateurs : 5\r\n" +
                       "Prix HT du plan Pennylane : 100,5 EUR\r\n" +
                       "Prix de la mise en conformité : 20,0 EUR\r\n" +
                       "Date de fin de gratuité : 31/10/2026\r\n" +
                       "Vague : Restauration\r\n";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildAmendmentBaseDetails_ShouldReturnAucun_ForNullMasculineFields()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            PlanName = null,
            UserCount = null,
            PlanPriceExclTax = null,
            CompliancePriceExclTax = null,
            FreeTrialEndDate = "31/10/2026",
            Wave = "Restauration"
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentBaseDetails(row);

        // Assert
        Assert.Contains("Nom du plan Pennylane : Aucun nom du plan Pennylane", result);
        Assert.Contains("Nombre d'utilisateurs : Aucun nombre d'utilisateurs", result);
        Assert.Contains("Prix HT du plan Pennylane : Aucun prix HT du plan Pennylane EUR", result);
        Assert.Contains("Prix de la mise en conformité : Aucun prix de la mise en conformité EUR", result);
    }

    [Fact]
    public void BuildAmendmentBaseDetails_ShouldReturnAucune_ForNullFeminineFields()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            PlanName = "PlanA",
            UserCount = 5,
            PlanPriceExclTax = 100.0m,
            CompliancePriceExclTax = 20.0m,
            FreeTrialEndDate = null,
            Wave = null
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentBaseDetails(row);

        // Assert
        Assert.Contains("Date de fin de gratuité : Aucune date de fin de gratuité", result);
        Assert.Contains("Vague : Aucune vague", result);
    }

    [Fact]
    public void BuildAmendmentBaseDetails_ShouldReturnAucun_ForEmptyStringFields()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            PlanName = "",
            UserCount = 5,
            PlanPriceExclTax = 100.0m,
            CompliancePriceExclTax = 20.0m,
            FreeTrialEndDate = "   ",
            Wave = ""
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentBaseDetails(row);

        // Assert
        Assert.Contains("Nom du plan Pennylane : Aucun nom du plan Pennylane", result);
        Assert.Contains("Date de fin de gratuité : Aucune date de fin de gratuité", result);
        Assert.Contains("Vague : Aucune vague", result);
    }

    [Fact]
    public void BuildAmendmentBaseDetails_ShouldReturnAllAucun_WhenAllFieldsAreNull()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            PlanName = null,
            UserCount = null,
            PlanPriceExclTax = null,
            CompliancePriceExclTax = null,
            FreeTrialEndDate = null,
            Wave = null
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentBaseDetails(row);

        // Assert
        var expected = "Nom du plan Pennylane : Aucun nom du plan Pennylane\r\n" +
                       "Nombre d'utilisateurs : Aucun nombre d'utilisateurs\r\n" +
                       "Prix HT du plan Pennylane : Aucun prix HT du plan Pennylane EUR\r\n" +
                       "Prix de la mise en conformité : Aucun prix de la mise en conformité EUR\r\n" +
                       "Date de fin de gratuité : Aucune date de fin de gratuité\r\n" +
                       "Vague : Aucune vague\r\n";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildAmendmentUpdatedDetails_ShouldReturnChangedFields()
    {
        // Arrange
        var oldEntity = new SignatoryAccessEntity
        {
            Email = "old@email.com",
            LastName = "OldLast",
            FirstName = "OldFirst",
            PlanName = "OldPlan",
            UserCount = 1,
            PlanPriceExclTax = 10.0m,
            CompliancePriceExclTax = 2.0m,
            FreeTrialEndDate = new DateTime(2024, 1, 1),
            Wave = "Restauration"
        };
        var newRow = new SignatoryAccessImportRow
        {
            Email = "new@email.com",
            LastName = "NewLast",
            FirstName = "NewFirst",
            PlanName = "NewPlan",
            UserCount = 2,
            PlanPriceExclTax = 20.0m,
            CompliancePriceExclTax = 3.0m,
            FreeTrialEndDate = "31/10/2026",
            Wave = null
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentUpdatedDetails(oldEntity, newRow);

        // Assert
        Assert.Contains("Email signataire : old@email.com a été remplacé par new@email.com", result);
        Assert.Contains("Nom signataire : OldLast a été remplacé par NewLast", result);
        Assert.Contains("Prénom signataire : OldFirst a été remplacé par NewFirst", result);
        Assert.Contains("Nom du plan Pennylane : OldPlan a été remplacé par NewPlan", result);
        Assert.Contains("Nombre d'utilisateurs : 1 a été remplacé par 2", result);
        Assert.Contains("Prix HT du plan Pennylane : 10,0 a été remplacé par 20,0", result);
        Assert.Contains("Prix de la mise en conformité : 2,0 a été remplacé par 3,0", result);
        Assert.Contains("Date de fin de gratuité : 01/01/2024 a été remplacé par 31/10/2026", result);
        Assert.Contains("Vague : Restauration a été remplacé par", result);
    }

    [Fact]
    public void BuildAmendmentUpdatedDetails_ShouldReturnEmptyString_WhenNoChanges()
    {
        // Arrange
        var oldEntity = new SignatoryAccessEntity
        {
            Email = "same@email.com",
            LastName = "SameLast",
            FirstName = "SameFirst",
            PlanName = "SamePlan",
            UserCount = 3,
            PlanPriceExclTax = 30.0m,
            CompliancePriceExclTax = 4.0m,
            FreeTrialEndDate = new DateTime(2026, 10, 31)
        };
        var newRow = new SignatoryAccessImportRow
        {
            Email = "same@email.com",
            LastName = "SameLast",
            FirstName = "SameFirst",
            PlanName = "SamePlan",
            UserCount = 3,
            PlanPriceExclTax = 30.0m,
            CompliancePriceExclTax = 4.0m,
            FreeTrialEndDate = "31/10/2026"
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentUpdatedDetails(oldEntity, newRow);

        // Assert
        Assert.True(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void BuildAmendmentUpdatedDetails_ShouldDisplayAucune_ForFeminineFields_WhenOldValueIsNull()
    {
        // Arrange
        var oldEntity = new SignatoryAccessEntity
        {
            Email = "test@email.com",
            FreeTrialEndDate = null,
            Wave = null
        };
        var newRow = new SignatoryAccessImportRow
        {
            Email = "test@email.com",
            FreeTrialEndDate = "31/10/2026",
            Wave = "Restauration"
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentUpdatedDetails(oldEntity, newRow);

        // Assert
        Assert.Contains("Date de fin de gratuité : Aucune date de fin de gratuité a été remplacé par 31/10/2026", result);
        Assert.Contains("Vague : Aucune vague a été remplacé par Restauration", result);
    }

    [Fact]
    public void BuildAmendmentUpdatedDetails_ShouldDisplayAucune_ForFeminineFields_WhenNewValueIsNull()
    {
        // Arrange
        var oldEntity = new SignatoryAccessEntity
        {
            Email = "test@email.com",
            FreeTrialEndDate = new DateTime(2026, 10, 31),
            Wave = "Restauration"
        };
        var newRow = new SignatoryAccessImportRow
        {
            Email = "test@email.com",
            FreeTrialEndDate = null,
            Wave = null
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentUpdatedDetails(oldEntity, newRow);

        // Assert
        Assert.Contains("Date de fin de gratuité : 31/10/2026 a été remplacé par Aucune date de fin de gratuité", result);
        Assert.Contains("Vague : Restauration a été remplacé par Aucune vague", result);
    }

    [Fact]
    public void BuildAmendmentUpdatedDetails_ShouldDisplayAucun_ForMasculineFields_WhenOldValueIsNull()
    {
        // Arrange
        var oldEntity = new SignatoryAccessEntity
        {
            Email = "test@email.com",
            PlanName = null,
            UserCount = null,
            PlanPriceExclTax = null,
            CompliancePriceExclTax = null
        };
        var newRow = new SignatoryAccessImportRow
        {
            Email = "test@email.com",
            PlanName = "NewPlan",
            UserCount = 5,
            PlanPriceExclTax = 100.0m,
            CompliancePriceExclTax = 20.0m
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentUpdatedDetails(oldEntity, newRow);

        // Assert
        Assert.Contains("Nom du plan Pennylane : Aucun nom du plan Pennylane a été remplacé par NewPlan", result);
        Assert.Contains("Nombre d'utilisateurs : Aucun nombre d'utilisateurs a été remplacé par 5", result);
        Assert.Contains("Prix HT du plan Pennylane : Aucun prix HT du plan Pennylane a été remplacé par 100,0", result);
        Assert.Contains("Prix de la mise en conformité : Aucun prix de la mise en conformité a été remplacé par 20,0", result);
    }

    [Fact]
    public void BuildAmendmentUpdatedDetails_ShouldDisplayAucun_ForMasculineFields_WhenNewValueIsNull()
    {
        // Arrange
        var oldEntity = new SignatoryAccessEntity
        {
            Email = "test@email.com",
            PlanName = "OldPlan",
            UserCount = 5,
            PlanPriceExclTax = 100.0m,
            CompliancePriceExclTax = 20.0m
        };
        var newRow = new SignatoryAccessImportRow
        {
            Email = "test@email.com",
            PlanName = null,
            UserCount = null,
            PlanPriceExclTax = null,
            CompliancePriceExclTax = null
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentUpdatedDetails(oldEntity, newRow);

        // Assert
        Assert.Contains("Nom du plan Pennylane : OldPlan a été remplacé par Aucun nom du plan Pennylane", result);
        Assert.Contains("Nombre d'utilisateurs : 5 a été remplacé par Aucun nombre d'utilisateurs", result);
        Assert.Contains("Prix HT du plan Pennylane : 100,0 a été remplacé par Aucun prix HT du plan Pennylane", result);
        Assert.Contains("Prix de la mise en conformité : 20,0 a été remplacé par Aucun prix de la mise en conformité", result);
    }

    [Fact]
    public void BuildAmendmentUpdatedDetails_ShouldDisplayAucun_ForStringFields_WhenOldValueIsEmpty()
    {
        // Arrange
        var oldEntity = new SignatoryAccessEntity
        {
            Email = "test@email.com",
            LastName = "",
            FirstName = "   "
        };
        var newRow = new SignatoryAccessImportRow
        {
            Email = "test@email.com",
            LastName = "NewLast",
            FirstName = "NewFirst"
        };

        // Act
        var result = PublishHistoryHelper.BuildAmendmentUpdatedDetails(oldEntity, newRow);

        // Assert
        Assert.Contains("Nom signataire : Aucun nom signataire a été remplacé par NewLast", result);
        Assert.Contains("Prénom signataire : Aucun prénom signataire a été remplacé par NewFirst", result);
    }
}
