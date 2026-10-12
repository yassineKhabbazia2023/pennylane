// <copyright file="DocumentSyncOptionsTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Configurations.Options;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Pennylane.Application.Tests.Configurations;

public class DocumentSyncOptionsTests
{
    [Fact]
    public void GetExpandedPaths_WithSimplePaths_ReturnsPathsAsIs()
    {
        // Arrange
        var options = new DocumentSyncOptions
        {
            SyncPaths = new List<string>
            {
                "/Dossier permanent/00 - Publications",
                "/Dossier permanent/01 - Contrats"
            }
        };

        // Act
        var expanded = options.GetExpandedPaths();

        // Assert
        Assert.Equal(2, expanded.Count);
        Assert.Contains("/Dossier permanent/00 - Publications", expanded);
        Assert.Contains("/Dossier permanent/01 - Contrats", expanded);
    }

    [Fact]
    public void GetExpandedPaths_WithWildcardPaths_ReturnsPathsAsIs()
    {
        // Arrange
        var options = new DocumentSyncOptions
        {
            SyncPaths = new List<string>
            {
                "/Dossier annuel/Exercice */FEC",
                "/Dossier annuel/Exercice */Fiscal"
            }
        };

        // Act
        var expanded = options.GetExpandedPaths();

        // Assert
        Assert.Equal(2, expanded.Count);
        Assert.Contains("/Dossier annuel/Exercice */FEC", expanded);
        Assert.Contains("/Dossier annuel/Exercice */Fiscal", expanded);
    }

    [Fact]
    public void GetExpandedPaths_WithMixedPaths_ReturnsAllPathsAsIs()
    {
        // Arrange
        var options = new DocumentSyncOptions
        {
            SyncPaths = new List<string>
            {
                "/Dossier permanent/00 - Publications",
                "/Dossier annuel/Exercice */FEC",
                "/Dossier annuel/Exercice */Fiscal"
            }
        };

        // Act
        var expanded = options.GetExpandedPaths();

        // Assert
        Assert.Equal(3, expanded.Count);
        Assert.Contains("/Dossier permanent/00 - Publications", expanded);
        Assert.Contains("/Dossier annuel/Exercice */FEC", expanded);
        Assert.Contains("/Dossier annuel/Exercice */Fiscal", expanded);
    }

    [Fact]
    public void GetExpandedPaths_WithEmptyPaths_ReturnsEmptyList()
    {
        // Arrange
        var options = new DocumentSyncOptions
        {
            SyncPaths = new List<string>()
        };

        // Act
        var expanded = options.GetExpandedPaths();

        // Assert
        Assert.Empty(expanded);
    }

    [Fact]
    public void GetExpandedPaths_WithMultipleWildcards_ReturnsPathsAsIs()
    {
        // Arrange
        var options = new DocumentSyncOptions
        {
            SyncPaths = new List<string>
            {
                "/Dossier annuel/Exercice */*/FEC",
                "/Dossier */Exercice */Fiscal"
            }
        };

        // Act
        var expanded = options.GetExpandedPaths();

        // Assert
        Assert.Equal(2, expanded.Count);
        Assert.Contains("/Dossier annuel/Exercice */*/FEC", expanded);
        Assert.Contains("/Dossier */Exercice */Fiscal", expanded);
    }

    [Fact]
    public void Validation_WithEmptySyncPaths_ShouldFail()
    {
        // Arrange
        var options = new DocumentSyncOptions
        {
            SyncPaths = new List<string>()
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Single(validationResults);
        Assert.Contains("must contain at least one path", validationResults[0].ErrorMessage);
    }

    [Fact]
    public void Validation_WithAtLeastOnePath_ShouldPass()
    {
        // Arrange
        var options = new DocumentSyncOptions
        {
            SyncPaths = new List<string>
            {
                "/Dossier permanent/00 - Publications"
            }
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }
}
