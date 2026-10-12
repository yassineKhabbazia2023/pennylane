// pyright file="SignatoryAccessImportMapTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using CsvHelper;
using CsvHelper.Configuration;
using Pennylane.Application.Mappers;
using Pennylane.Application.Models.Signatory;
using System.Globalization;
using System.IO;
using Xunit;

namespace Pennylane.Application.Tests.Mappers;

/// <summary>
/// Tests unitaires pour la classe SignatoryAccessImportMap.
/// Vérifie le mapping correct des en-têtes et le bon fonctionnement des convertisseurs de type.
/// </summary>
public class SignatoryAccessImportMapTests
{
    private readonly CsvConfiguration _config;

    public SignatoryAccessImportMapTests()
    {
        _config = new CsvConfiguration(new CultureInfo("fr-FR"))
        {
            Delimiter = ";",
            HasHeaderRecord = true
        };
    }

    [Fact]
    public void SignatoryAccessImportMap_ShouldMapAllFieldsCorrectly()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix HT de la mise en conformité;Date de fin de gratuité\r\n" +
                         "ACC001;john.doe@test.com;Doe;John;Enterprise;10;150,55;75,00;31/10/2026";

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, _config);
        csv.Context.RegisterClassMap<SignatoryAccessImportMap>();

        // Act
        csv.Read();
        csv.ReadHeader();
        csv.Read();
        var record = csv.GetRecord<SignatoryAccessImportRow>();

        // Assert
        Assert.NotNull(record);
        Assert.Equal("ACC001", record.AccountNumber);
        Assert.Equal("john.doe@test.com", record.Email);
        Assert.Equal("Doe", record.LastName);
        Assert.Equal("John", record.FirstName);
        Assert.Equal("Enterprise", record.PlanName);
        Assert.Equal(10, record.UserCount);
        Assert.Equal(150.55m, record.PlanPriceExclTax);
        Assert.Equal(75.00m, record.CompliancePriceExclTax);
        Assert.Equal("31/10/2026", record.FreeTrialEndDate);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData(" ", 0)]
    [InlineData("NULL", 0)]
    [InlineData("null", 0)]
    public void SignatoryAccessImportMap_ShouldConvertEmptyOrNullNumericToZero(string input, int expected)
    {
        // Arrange
        // On teste via le mapping complet pour s'assurer que les TypeConverters sont bien branchés
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix HT de la mise en conformité;Date de fin de gratuité\r\n" +
                         $"ACC001;email@test.com;Nom;Prenom;Plan;{input};{input};{input};31/10/2026";

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, _config);
        csv.Context.RegisterClassMap<SignatoryAccessImportMap>();

        // Act
        csv.Read();
        csv.ReadHeader();
        csv.Read();
        var record = csv.GetRecord<SignatoryAccessImportRow>();

        // Assert
        Assert.Equal(expected, record.UserCount);
        Assert.Equal((decimal)expected, record.PlanPriceExclTax);
        Assert.Equal((decimal)expected, record.CompliancePriceExclTax);
    }

    [Fact]
    public void SignatoryAccessImportMap_ShouldHandleLargeNumericValues()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix HT de la mise en conformité;Date de fin de gratuité\r\n" +
                         "ACC999;test@test.com;Smith;Will;Gold;9999;1234567,89;987654,32;31/10/2026";

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, _config);
        csv.Context.RegisterClassMap<SignatoryAccessImportMap>();

        // Act
        csv.Read();
        csv.ReadHeader();
        csv.Read();
        var record = csv.GetRecord<SignatoryAccessImportRow>();

        // Assert
        Assert.Equal(9999, record.UserCount);
        Assert.Equal(1234567.89m, record.PlanPriceExclTax);
        Assert.Equal(987654.32m, record.CompliancePriceExclTax);
    }
}
