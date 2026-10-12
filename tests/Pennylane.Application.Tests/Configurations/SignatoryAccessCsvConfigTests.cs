// pyright file="SignatoryAccessCsvConfigTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Configuration;
using Pennylane.Application.Models.Signatory;

namespace Pennylane.Application.Tests.Configurations;

public class SignatoryAccessCsvConfigTests
{
    [Fact]
    public void IsValidCsvFormat_WhenValidData_ShouldReturnTrue()
    {
        // Arrange
        var csvData = new List<(SignatoryAccessImportRow, int, string[])>
        {
            (
                new SignatoryAccessImportRow { AccountNumber = "ACC001" },
                2,
                new[] { 
                    "Code dossier", 
                    "Email signataire", 
                    "Nom signataire", 
                    "Prénom signataire", 
                    "Nom du plan Pennylane",                    
                    "Nombre d'utilisateurs Pennylane", 
                    "Prix HT du plan Pennylane",
                    "Prix HT de la mise en conformité",
                    "Date de fin de gratuité",
                    "Vague",
                }
            )
        };

        // Act
        var result = SignatoryAccessCsvConfig.IsValidCsvFormat(
            csvData, 
            typeof(SignatoryAccessImportRow), 
            out var error);

        // Assert
        Assert.True(result, $"Validation échouée: {error}");
        Assert.Empty(error);
    }


    [Fact]
    public void IsValidCsvFormat_WhenEmptyData_ShouldReturnFalse()
    {
        // Arrange
        var csvData = new List<(SignatoryAccessImportRow, int, string[])>();

        // Act
        var result = SignatoryAccessCsvConfig.IsValidCsvFormat(
            csvData, 
            typeof(SignatoryAccessImportRow), 
            out var error);

        // Assert
        Assert.False(result);
        Assert.Equal("Le fichier CSV ne contient aucune ligne de données.", error);
    }

    [Fact]
    public void IsValidCsvFormat_WhenNullData_ShouldReturnFalse()
    {
        // Arrange
        List<(SignatoryAccessImportRow, int, string[])>? csvData = null;

        // Act
        var result = SignatoryAccessCsvConfig.IsValidCsvFormat(
            csvData!, 
            typeof(SignatoryAccessImportRow), 
            out var error);

        // Assert
        Assert.False(result);
        Assert.Equal("Le fichier CSV ne contient aucune ligne de données.", error);
    }

    [Fact]
    public void IsValidCsvFormat_WhenMissingHeader_ShouldReturnFalse()
    {
        // Arrange
        var csvData = new List<(SignatoryAccessImportRow, int, string[])>
        {
            (
                new SignatoryAccessImportRow { AccountNumber = "ACC001" },
                2,
                Array.Empty<string>()
            )
        };

        // Act
        var result = SignatoryAccessCsvConfig.IsValidCsvFormat(
            csvData, 
            typeof(SignatoryAccessImportRow), 
            out var error);

        // Assert
        Assert.False(result);
        Assert.Equal("Le fichier CSV ne contient pas de ligne d'en-tête.", error);
    }

    [Fact]
    public void IsValidCsvFormat_WhenInsufficientColumns_ShouldReturnFalse()
    {
        // Arrange
        var csvData = new List<(SignatoryAccessImportRow, int, string[])>
        {
            (
                new SignatoryAccessImportRow { AccountNumber = "ACC001" },
                2,
                new[] { "Code dossier", "Email signataire", "Nom signataire" } // Seulement 3 colonnes
            )
        };

        // Act
        var result = SignatoryAccessCsvConfig.IsValidCsvFormat(
            csvData, 
            typeof(SignatoryAccessImportRow), 
            out var error);

        // Assert
        Assert.False(result);
        Assert.Contains("Header invalide : Nombre de colonnes invalide", error);
    }

    [Fact]
    public void IsValidCsvFormat_WhenInvalidColumnName_ShouldReturnFalse()
    {
        // Arrange
        var csvData = new List<(SignatoryAccessImportRow, int, string[])>
        {
            (
                new SignatoryAccessImportRow { AccountNumber = "ACC001" },
                2,
                new[] { "WRONG_COLUMN", "Email signataire", "Nom signataire", "Prénom signataire", 
                        "Nom du plan Pennylane", "Nombre d'utilisateurs Pennylane", 
                        "Prix HT du plan Pennylane", "Prix HT de la mise en conformité", "Date de fin de gratuité", "Vague" }
            )
        };

        // Act
        var result = SignatoryAccessCsvConfig.IsValidCsvFormat(
            csvData, 
            typeof(SignatoryAccessImportRow), 
            out var error);

        // Assert
        Assert.False(result);
        Assert.Contains("Header invalide", error);
        Assert.Contains("Colonne 1", error);
        Assert.Contains("WRONG_COLUMN", error);
    }

    [Fact]
    public void IsValidCsvFormat_WhenDataRowHasWrongColumnCount_ShouldReturnFalse()
    {
        // Arrange
        var csvData = new List<(SignatoryAccessImportRow, int, string[])>
        {
            (
                null!, // Header row DTO is null
                1,
                new[] { "Code dossier", "Email signataire", "Nom signataire", "Prénom signataire", 
                        "Nom du plan Pennylane", "Nombre d'utilisateurs Pennylane", 
                        "Prix HT du plan Pennylane", "Prix HT de la mise en conformité", "Date de fin de gratuité", "Vague" }
            ),
            (
                new SignatoryAccessImportRow { AccountNumber = "ACC001" },
                5,
                new[] { "ACC001", "test@test.com" } // Ligne 5 avec seulement 2 colonnes
            )
        };

        // Act
        var result = SignatoryAccessCsvConfig.IsValidCsvFormat(
            csvData, 
            typeof(SignatoryAccessImportRow), 
            out var error);

        // Assert
        Assert.False(result);
        Assert.Contains("Nombre de colonnes invalide", error);
        Assert.Contains("Ligne 5 a 2 colonnes au lieu de 10", error);
    }
    [Fact]
    public void IsValidCsvFormat_WhenColumnNameCaseInsensitive_ShouldReturnTrue()
    {
        // Arrange
        var csvData = new List<(SignatoryAccessImportRow, int, string[])>
        {
            (
                new SignatoryAccessImportRow { AccountNumber = "ACC001" },
                2,
                new[] { 
                    "code DOSSIER",                          // Variation de casse
                    "EMAIL signataire",                      // Variation de casse
                    "nom SIGNATAIRE",                        // Variation de casse
                    "prénom Signataire",                     // Variation de casse
                    "NOM du plan PENNYLANE",                // Variation de casse
                    "NOMBRE d'utilisateurs Pennylane",       // Variation de casse
                    "prix HT du PLAN Pennylane",            // Variation de casse
                    "Prix ht DE la mise EN conformité",     // Variation de casse
                    "Date de Fin DE graTuité",
                    "VAGUE",                                 // Nouvelle colonne avec variation de casse
                }
            )
        };

        // Act
        var result = SignatoryAccessCsvConfig.IsValidCsvFormat(
            csvData, 
            typeof(SignatoryAccessImportRow), 
            out var error);

        // Assert
        Assert.True(result, $"Validation échouée: {error}");
        Assert.Empty(error);
    }
}
