using Application.Models.Contacts;
using AutoFixture;
using Pennylane.Application.Mappers;
using Pennylane.Application.Models;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;

namespace Pennylane.Application.Tests.Mappers;

public class MapPennylaneTests
{
    private readonly Fixture _fixture;

    public MapPennylaneTests()
    {
        _fixture = new Fixture();
    }

    #region ToPennylaneContact Tests

    [Fact]
    public void ToPennylaneContact_WhenContactIsValid_ShouldMapCorrectly()
    {
        // Arrange
        var contact = _fixture.Build<Contact>()
            .With(c => c.FirstName, "John")
            .With(c => c.LastName, "Doe")
            .With(c => c.Email, "john.doe@example.com")
            .Create();
        var authenticationMethod = "password";

        // Act
        var result = contact.ToPennylaneContact(authenticationMethod);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(contact.FirstName, result.FirstName);
        Assert.Equal(contact.LastName, result.LastName);
        Assert.Equal(contact.Email, result.Email);
        Assert.Equal(authenticationMethod, result.AuthenticationMethod);
        Assert.True(result.SkipWelcomeEmail);
    }

    [Fact]
    public void ToPennylaneContact_WhenContactHasEmptyValues_ShouldMapCorrectly()
    {
        // Arrange
        var contact = _fixture.Build<Contact>()
            .With(c => c.FirstName, string.Empty)
            .With(c => c.LastName, string.Empty)
            .With(c => c.Email, "test@example.com")
            .Create();
        var authenticationMethod = "sso";

        // Act
        var result = contact.ToPennylaneContact(authenticationMethod);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.FirstName);
        Assert.Equal(string.Empty, result.LastName);
        Assert.Equal(contact.Email, result.Email);
        Assert.Equal(authenticationMethod, result.AuthenticationMethod);
        Assert.True(result.SkipWelcomeEmail);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("sso")]
    [InlineData("oauth")]
    public void ToPennylaneContact_WithDifferentAuthenticationMethods_ShouldMapCorrectly(string authMethod)
    {
        // Arrange
        var contact = _fixture.Create<Contact>();

        // Act
        var result = contact.ToPennylaneContact(authMethod);

        // Assert
        Assert.Equal(authMethod, result.AuthenticationMethod);
        Assert.True(result.SkipWelcomeEmail);
    }

    #endregion

    #region ToPennylaneCompany Tests

    [Fact]
    public void ToPennylaneCompany_WhenCompanyEntityIsValid_ShouldMapCorrectly()
    {
        // Arrange
        var companyEntity = _fixture.Build<CompanyEntity>()
            .With(c => c.Id, "company-123")
            .With(c => c.FirmId, "firm-456")
            .With(c => c.RegNo, "REG123456")
            .With(c => c.Name, "Test Company")
            .With(c => c.SaasPlan, "Premium")
            .With(c => c.SupplierInvoiceTransmissionEmail, "supplier@test.com")
            .With(c => c.CustomerInvoiceTransmissionEmail, "customer@test.com")
            .Create();

        // Act
        var result = companyEntity.ToPennylaneCompany();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(companyEntity.Id, result.Id);
        Assert.Equal(companyEntity.FirmId, result.FirmId);
        Assert.Equal(companyEntity.RegNo, result.RegNo);
        Assert.Equal(companyEntity.Name, result.Name);
        Assert.Equal(companyEntity.SaasPlan, result.SaasPlan);
        Assert.Single(result.SupplierInvoiceTransmissionEmails);
        Assert.Equal(companyEntity.SupplierInvoiceTransmissionEmail, result.SupplierInvoiceTransmissionEmails[0]);
        Assert.Single(result.CustomerInvoiceTransmissionEmails);
        Assert.Equal(companyEntity.CustomerInvoiceTransmissionEmail, result.CustomerInvoiceTransmissionEmails[0]);
    }

    [Fact]
    public void ToPennylaneCompany_WhenEmailsAreNull_ShouldCreateArrayWithNull()
    {
        // Arrange
        var companyEntity = _fixture.Build<CompanyEntity>()
            .With(c => c.SupplierInvoiceTransmissionEmail, (string?)null)
            .With(c => c.CustomerInvoiceTransmissionEmail, (string?)null)
            .Create();

        // Act
        var result = companyEntity.ToPennylaneCompany();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.SupplierInvoiceTransmissionEmails);
        Assert.Null(result.SupplierInvoiceTransmissionEmails[0]);
        Assert.Single(result.CustomerInvoiceTransmissionEmails);
        Assert.Null(result.CustomerInvoiceTransmissionEmails[0]);
    }

    [Fact]
    public void ToPennylaneCompany_WhenRequiredFieldsAreSet_ShouldMapCorrectly()
    {
        // Arrange
        var companyEntity = _fixture.Build<CompanyEntity>()
            .With(c => c.RegNo, (string?)null)
            .With(c => c.SaasPlan, (string?)null)
            .Create();

        // Act & Assert - Should not throw exception despite null values
        var result = companyEntity.ToPennylaneCompany();
        Assert.NotNull(result);
    }

    #endregion

    #region ToCompanyEntity Tests

    [Fact]
    public void ToCompanyEntity_WhenPennylaneCompanyIsValid_ShouldMapCorrectly()
    {
        // Arrange
        var beforeMapping = DateTime.UtcNow;
        var pennylaneCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.Id, "company-789")
            .With(c => c.FirmId, "firm-012")
            .With(c => c.RegNo, "REG789012")
            .With(c => c.Name, "Pennylane Test Company")
            .With(c => c.SaasPlan, "Basic")
            .With(c => c.SupplierInvoiceTransmissionEmails, new[] { "supplier@pennylane.com" })
            .With(c => c.CustomerInvoiceTransmissionEmails, new[] { "customer@pennylane.com" })
            .Create();

        // Act
        var result = pennylaneCompany.ToCompanyEntity();
        var afterMapping = DateTime.UtcNow;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(pennylaneCompany.Id, result.Id);
        Assert.Equal(pennylaneCompany.FirmId, result.FirmId);
        Assert.Equal(pennylaneCompany.RegNo, result.RegNo);
        Assert.Equal(pennylaneCompany.Name, result.Name);
        Assert.Equal(pennylaneCompany.SaasPlan, result.SaasPlan);
        Assert.Equal(pennylaneCompany.SupplierInvoiceTransmissionEmails[0], result.SupplierInvoiceTransmissionEmail);
        Assert.Equal(pennylaneCompany.CustomerInvoiceTransmissionEmails[0], result.CustomerInvoiceTransmissionEmail);
        
        // Verify dates are set to current UTC time
        Assert.True(result.LastSyncDate >= beforeMapping && result.LastSyncDate <= afterMapping);
        Assert.True(result.CreatedAt >= beforeMapping && result.CreatedAt <= afterMapping);
    }

    [Fact]
    public void ToCompanyEntity_WhenEmailArraysAreNull_ShouldSetEmailsToNull()
    {
        // Arrange
        var pennylaneCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.SupplierInvoiceTransmissionEmails, (string[]?)null)
            .With(c => c.CustomerInvoiceTransmissionEmails, (string[]?)null)
            .Create();

        // Act
        var result = pennylaneCompany.ToCompanyEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.SupplierInvoiceTransmissionEmail);
        Assert.Null(result.CustomerInvoiceTransmissionEmail);
    }

    [Fact]
    public void ToCompanyEntity_WhenEmailArraysAreEmpty_ShouldSetEmailsToNull()
    {
        // Arrange
        var pennylaneCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.SupplierInvoiceTransmissionEmails, Array.Empty<string>())
            .With(c => c.CustomerInvoiceTransmissionEmails, Array.Empty<string>())
            .Create();

        // Act
        var result = pennylaneCompany.ToCompanyEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.SupplierInvoiceTransmissionEmail);
        Assert.Null(result.CustomerInvoiceTransmissionEmail);
    }

    [Fact]
    public void ToCompanyEntity_WhenEmailArraysHaveMultipleValues_ShouldTakeFirstValue()
    {
        // Arrange
        var pennylaneCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.SupplierInvoiceTransmissionEmails, new[] { "supplier1@test.com", "supplier2@test.com", "supplier3@test.com" })
            .With(c => c.CustomerInvoiceTransmissionEmails, new[] { "customer1@test.com", "customer2@test.com" })
            .Create();

        // Act
        var result = pennylaneCompany.ToCompanyEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("supplier1@test.com", result.SupplierInvoiceTransmissionEmail);
        Assert.Equal("customer1@test.com", result.CustomerInvoiceTransmissionEmail);
    }

    [Fact]
    public void ToCompanyEntity_WhenEmailArraysContainNullValues_ShouldHandleGracefully()
    {
        // Arrange
        var pennylaneCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.SupplierInvoiceTransmissionEmails, new string?[] { null, "supplier@test.com" })
            .With(c => c.CustomerInvoiceTransmissionEmails, new string?[] { "customer@test.com", null })
            .Create();

        // Act
        var result = pennylaneCompany.ToCompanyEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.SupplierInvoiceTransmissionEmail); // FirstOrDefault() of null
        Assert.Equal("customer@test.com", result.CustomerInvoiceTransmissionEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("REG123456")]
    public void ToCompanyEntity_WithDifferentRegNoValues_ShouldMapCorrectly(string? regNo)
    {
        // Arrange
        var pennylaneCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.RegNo, regNo!)
            .Create();

        // Act
        var result = pennylaneCompany.ToCompanyEntity();

        // Assert
        Assert.Equal(regNo, result.RegNo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Premium")]
    [InlineData("Basic")]
    public void ToCompanyEntity_WithDifferentSaasPlanValues_ShouldMapCorrectly(string? saasPlan)
    {
        // Arrange
        var pennylaneCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.SaasPlan, saasPlan!)
            .Create();

        // Act
        var result = pennylaneCompany.ToCompanyEntity();

        // Assert
        Assert.Equal(saasPlan, result.SaasPlan);
    }

    [Fact]
    public void ToCompanyEntity_ShouldAlwaysSetCurrentUtcTime()
    {
        // Arrange
        var pennylaneCompany = _fixture.Create<PennylaneCompany>();
        var beforeMapping = DateTime.UtcNow;

        // Act
        var result = pennylaneCompany.ToCompanyEntity();
        var afterMapping = DateTime.UtcNow;

        // Assert
        Assert.True(result.LastSyncDate.HasValue);
        Assert.True(result.LastSyncDate >= beforeMapping && result.LastSyncDate <= afterMapping);
        Assert.True(result.CreatedAt >= beforeMapping && result.CreatedAt <= afterMapping);
        Assert.Equal(DateTimeKind.Utc, result.LastSyncDate.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, result.CreatedAt.Kind);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void MapPennylane_RoundTripMapping_ShouldPreserveMainData()
    {
        // Arrange
        var originalCompanyEntity = _fixture.Build<CompanyEntity>()
            .With(c => c.SupplierInvoiceTransmissionEmail, "supplier@original.com")
            .With(c => c.CustomerInvoiceTransmissionEmail, "customer@original.com")
            .Create();

        // Act - Convert to PennylaneCompany and back
        var pennylaneCompany = originalCompanyEntity.ToPennylaneCompany();
        var roundTripCompanyEntity = pennylaneCompany.ToCompanyEntity();

        // Assert - Main data should be preserved
        Assert.Equal(originalCompanyEntity.Id, roundTripCompanyEntity.Id);
        Assert.Equal(originalCompanyEntity.FirmId, roundTripCompanyEntity.FirmId);
        Assert.Equal(originalCompanyEntity.RegNo, roundTripCompanyEntity.RegNo);
        Assert.Equal(originalCompanyEntity.Name, roundTripCompanyEntity.Name);
        Assert.Equal(originalCompanyEntity.SaasPlan, roundTripCompanyEntity.SaasPlan);
        Assert.Equal(originalCompanyEntity.SupplierInvoiceTransmissionEmail, roundTripCompanyEntity.SupplierInvoiceTransmissionEmail);
        Assert.Equal(originalCompanyEntity.CustomerInvoiceTransmissionEmail, roundTripCompanyEntity.CustomerInvoiceTransmissionEmail);

        // Note: Dates will be different as ToCompanyEntity sets new timestamps
        Assert.NotEqual(originalCompanyEntity.LastSyncDate, roundTripCompanyEntity.LastSyncDate);
        Assert.NotEqual(originalCompanyEntity.CreatedAt, roundTripCompanyEntity.CreatedAt);
    }

    [Fact]
    public void MapPennylane_ContactToContactMapping_ShouldPreserveAllData()
    {
        // Arrange
        var originalContact = _fixture.Create<Contact>();
        var authMethod = "test-auth";

        // Act
        var pennylaneContact = originalContact.ToPennylaneContact(authMethod);

        // Assert
        Assert.Equal(originalContact.FirstName, pennylaneContact.FirstName);
        Assert.Equal(originalContact.LastName, pennylaneContact.LastName);
        Assert.Equal(originalContact.Email, pennylaneContact.Email);
        Assert.Equal(authMethod, pennylaneContact.AuthenticationMethod);
        Assert.True(pennylaneContact.SkipWelcomeEmail);
    }

    #endregion
}
