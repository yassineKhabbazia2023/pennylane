// <copyright file="EmailRecapOptionsTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Pennylane.Application.Configurations.Options;

namespace Pennylane.Application.Tests.Configurations;

public class EmailRecapOptionsTests
{
    [Fact]
    public void Validation_WithValidConfig_Passes()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeTrue();
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void Validation_WithMissingSenderEmail_Fails()
    {
        // Arrange
        var options = CreateValidOptions();
        options.SenderEmail = null!;

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Validation_WithInvalidSenderEmail_Fails()
    {
        // Arrange
        var options = CreateValidOptions();
        options.SenderEmail = "not-an-email";

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Validation_WithMissingLoginUrl_Fails()
    {
        // Arrange
        var options = CreateValidOptions();
        options.LoginUrl = null!;

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Validation_WithInvalidLoginUrl_Fails()
    {
        // Arrange
        var options = CreateValidOptions();
        options.LoginUrl = "not-a-url";

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(169)]
    public void Validation_WithInvalidLookbackHours_Fails(int hours)
    {
        // Arrange
        var options = CreateValidOptions();
        options.LookbackHours = hours;

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(168)]
    public void Validation_WithValidLookbackHours_Passes(int hours)
    {
        // Arrange
        var options = CreateValidOptions();
        options.LookbackHours = hours;

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Validation_WithInvalidMaxParallelEmailSends_Fails(int sends)
    {
        // Arrange
        var options = CreateValidOptions();
        options.MaxParallelEmailSends = sends;

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void Validation_WithInvalidReferenceHour_Fails(int hour)
    {
        // Arrange
        var options = CreateValidOptions();
        options.ReferenceHour = hour;

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(23)]
    public void Validation_WithValidReferenceHour_Passes(int hour)
    {
        // Arrange
        var options = CreateValidOptions();
        options.ReferenceHour = hour;

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(9)]
    [InlineData(501)]
    public void Validation_WithInvalidBatchSize_Fails(int size)
    {
        // Arrange
        var options = CreateValidOptions();
        options.BatchSize = size;

        // Act
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        var isValid = Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new EmailRecapOptions
        {
            SenderEmail = "test@test.com",
            LoginUrl = "https://test.com"
        };

        // Assert
        options.Enabled.Should().BeTrue();
        options.ReferenceHour.Should().Be(9);
        options.TemplateName.Should().Be("Document deposit pennylane");
        options.LookbackHours.Should().Be(24);
        options.MaxParallelEmailSends.Should().Be(20);
        options.BatchSize.Should().Be(100);
        options.GenerationCron.Should().Be("0 8 * * *");
        options.SendCron.Should().Be("0 9 * * *");
    }

    [Fact]
    public void ConstantName_IsCorrect()
    {
        // Assert
        EmailRecapOptions.EmailRecapSettings.Should().Be("EmailRecapSettings");
    }

    private static EmailRecapOptions CreateValidOptions()
    {
        return new EmailRecapOptions
        {
            TemplateName = "test-template",
            LookbackHours = 24,
            SenderEmail = "sender@test.com",
            LoginUrl = "https://app.test.com/login",
            MaxParallelEmailSends = 20,
            BatchSize = 100,
            GenerationCron = "0 8 * * *",
            SendCron = "0 9 * * *"
        };
    }
}
