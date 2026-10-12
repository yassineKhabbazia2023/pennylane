// <copyright file="CompanyRepositoryTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Exceptions;
using Application.Models.Accounts;
using Microsoft.EntityFrameworkCore;
using Pennylane.Application.Models;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Infrastructure.Repository;
using Pulse.ContactRegistry.Infrastructure.Context;

namespace Pennylane.Infrastructure.Tests.Repository;

public class CompanyRepositoryTests
{
    private PennylaneContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PennylaneContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PennylaneContext(options);
    }

    private PennylaneCompany CreateTestPennylaneCompany(string id, string name = "Test Company")
    {
        return new PennylaneCompany
        {
            Id = id,
            FirmId = "test-firm",
            Name = name,
            RegNo = "123456",
            TradeName = "Test Trade",
            Address = "123 Test St",
            City = "Test City",
            PostalCode = "12345",
            CountryAlpha2 = "FR",
            SaasPlan = "Premium",
            FiscalCategory = "Category",
            FiscalRegime = "Regime",
            LegalFormCode = "SARL",
            VatFrequency = "Monthly"
        };
    }

    [Fact]
    public async Task GetCompanyByIdWithRetentionAsync_ReturnsCompany_WhenLastSyncDateWithinRetention()
    {
        // Arrange
        var context = GetInMemoryContext();
        var companyId = "test-company";
        var now = DateTime.UtcNow;
        context.CompanyEntity.Add(new CompanyEntity
        {
            Id = companyId,
            FirmId = "test-firm",
            Name = "Test",
            LastSyncDate = now,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
        var repo = new CompanyRepository(context);

        // Act
        var result = await repo.GetCompanyByIdWithRetentionAsync(companyId, 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(companyId, result.Id);
    }

    [Fact]
    public async Task GetCompanyByIdWithRetentionAsync_ThrowsException_WhenCompanyNotFound()
    {
        // Arrange
        var context = GetInMemoryContext();
        var repo = new CompanyRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.GetCompanyByIdWithRetentionAsync("missing-company", 1));
    }

    [Fact]
    public async Task GetAllCompaniesAsync_ReturnsEmptyList_WhenNoCompanies()
    {
        // Arrange
        var context = GetInMemoryContext();
        var repo = new CompanyRepository(context);

        // Act
        var result = await repo.GetAllCompaniesAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllCompaniesAsync_ReturnsCompaniesOrderedByName()
    {
        // Arrange
        var context = GetInMemoryContext();
        var now = DateTime.UtcNow;

        var companies = new[]
        {
            new CompanyEntity { Id = "3", FirmId = "firm3", Name = "Charlie", CreatedAt = now, UpdatedAt = now },
            new CompanyEntity { Id = "1", FirmId = "firm1", Name = "Alpha", CreatedAt = now, UpdatedAt = now },
            new CompanyEntity { Id = "2", FirmId = "firm2", Name = "Beta", CreatedAt = now, UpdatedAt = now }
        };

        context.CompanyEntity.AddRange(companies);
        await context.SaveChangesAsync();
        var repo = new CompanyRepository(context);

        // Act
        var result = await repo.GetAllCompaniesAsync();

        // Assert
        var resultList = result.ToList();
        Assert.Equal(3, resultList.Count);
        Assert.Equal("Alpha", resultList[0].Name);
        Assert.Equal("Beta", resultList[1].Name);
        Assert.Equal("Charlie", resultList[2].Name);
    }

    [Fact]
    public async Task GetLastSyncDateAsync_ReturnsNull_WhenCompanyNotFound()
    {
        // Arrange
        var context = GetInMemoryContext();
        var repo = new CompanyRepository(context);

        // Act
        var result = await repo.GetLastSyncDateAsync("missing-company");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLastSyncDateAsync_ReturnsLastSyncDate_WhenCompanyExists()
    {
        // Arrange
        var context = GetInMemoryContext();
        var companyId = "test-company";
        var lastSyncDate = DateTime.UtcNow.AddDays(-5);
        var now = DateTime.UtcNow;

        context.CompanyEntity.Add(new CompanyEntity
        {
            Id = companyId,
            FirmId = "test-firm",
            Name = "Test",
            LastSyncDate = lastSyncDate,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
        var repo = new CompanyRepository(context);

        // Act
        var result = await repo.GetLastSyncDateAsync(companyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(lastSyncDate.Date, result.Value.Date);
    }

    [Fact]
    public async Task GetLastSyncDateAsync_ReturnsNull_WhenLastSyncDateIsNull()
    {
        // Arrange
        var context = GetInMemoryContext();
        var companyId = "test-company";
        var now = DateTime.UtcNow;

        context.CompanyEntity.Add(new CompanyEntity
        {
            Id = companyId,
            FirmId = "test-firm",
            Name = "Test",
            LastSyncDate = null,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
        var repo = new CompanyRepository(context);

        // Act
        var result = await repo.GetLastSyncDateAsync(companyId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCompanyByIdAsync_ReturnsNull_WhenCompanyNotFound()
    {
        // Arrange
        var context = GetInMemoryContext();
        var repo = new CompanyRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.GetCompanyByIdAsync("missing-company"));
    }

    [Fact]
    public async Task GetCompanyByIdAsync_ReturnsCompany_WhenCompanyExists()
    {
        // Arrange
        var context = GetInMemoryContext();
        var companyId = "test-company";
        var now = DateTime.UtcNow;

        context.CompanyEntity.Add(new CompanyEntity
        {
            Id = companyId,
            FirmId = "test-firm",
            Name = "Test Company",
            RegNo = "123456",
            SaasPlan = "Premium",
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
        var repo = new CompanyRepository(context);

        // Act
        var result = await repo.GetCompanyByIdAsync(companyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(companyId, result.Id);
        Assert.Equal("Test Company", result.Name);
        Assert.Equal("123456", result.RegNo);
        Assert.Equal("Premium", result.SaasPlan);
    }

    [Fact]
    public async Task GetPennylaneInternalIdByACompanyIdAsync_ReturnsNull_WhenCompanyNotFound()
    {
        // Arrange
        var context = GetInMemoryContext();
        var repo = new CompanyRepository(context);

        // Act
        var result = await repo.GetPennylaneInternalIdByACompanyIdAsync("missing-company");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPennylaneInternalIdByACompanyIdAsync_ReturnsInternalId_WhenCompanyExists()
    {
        // Arrange
        var context = GetInMemoryContext();
        var companyId = "test-company";
        var internalId = "internal123";
        var now = DateTime.UtcNow;

        context.CompanyEntity.Add(new CompanyEntity
        {
            Id = companyId,
            FirmId = "test-firm",
            Name = "Test",
            InternalId = internalId,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
        var repo = new CompanyRepository(context);

        // Act
        var result = await repo.GetPennylaneInternalIdByACompanyIdAsync(companyId);

        // Assert
        Assert.Equal(internalId, result);
    }

    [Fact]
    public async Task GetPennylaneInternalIdByACompanyIdAsync_ReturnsNull_WhenInternalIdIsNull()
    {
        // Arrange
        var context = GetInMemoryContext();
        var companyId = "test-company";
        var now = DateTime.UtcNow;

        context.CompanyEntity.Add(new CompanyEntity
        {
            Id = companyId,
            FirmId = "test-firm",
            Name = "Test",
            InternalId = null,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
        var repo = new CompanyRepository(context);

        // Act
        var result = await repo.GetPennylaneInternalIdByACompanyIdAsync(companyId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SavePennylaneInternalIdAsync_UpdatesInternalId_WhenCompanyExists()
    {
        // Arrange
        var context = GetInMemoryContext();
        var companyId = "test-company";
        var newInternalId = 456;
        var now = DateTime.UtcNow;

        context.CompanyEntity.Add(new CompanyEntity
        {
            Id = companyId,
            FirmId = "test-firm",
            Name = "Test",
            InternalId = "123",
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
        var repo = new CompanyRepository(context);

        // Act
        await repo.SavePennylaneInternalIdAsync(companyId, newInternalId);

        // Assert
        var company = await context.CompanyEntity.FirstAsync(c => c.Id == companyId);
        Assert.Equal("456", company.InternalId);
        Assert.True(company.UpdatedAt > now);
    }

    [Fact]
    public async Task SavePennylaneInternalIdAsync_DoesNothing_WhenCompanyNotFound()
    {
        // Arrange
        var context = GetInMemoryContext();
        var repo = new CompanyRepository(context);

        // Act
        await repo.SavePennylaneInternalIdAsync("missing-company", 123);

        // Assert
        var companiesCount = await context.CompanyEntity.CountAsync();
        Assert.Equal(0, companiesCount);
    }

    [Fact]
    public async Task SaveCompaniesAsync_AddsNewCompany_WhenCompanyDoesNotExist()
    {
        // Arrange
        var context = GetInMemoryContext();
        var repo = new CompanyRepository(context);
        var newCompany = CreateTestPennylaneCompany("new-company", "New Company");
        newCompany.SupplierInvoiceTransmissionEmails = new[] { "supplier@test.com" };
        newCompany.CustomerInvoiceTransmissionEmails = new[] { "customer@test.com" };

        // Act
        await repo.SaveCompaniesAsync(new List<PennylaneCompany> { newCompany });

        // Assert
        var company = await context.CompanyEntity.FirstAsync(c => c.Id == "new-company");
        Assert.Equal("New Company", company.Name);
        Assert.Equal("123456", company.RegNo);
        Assert.Equal("Premium", company.SaasPlan);
        Assert.NotNull(company.LastSyncDate);
        Assert.True(company.LastSyncDate > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task SaveCompaniesAsync_UpdatesExistingCompany_WhenCompanyExists()
    {
        // Arrange
        var context = GetInMemoryContext();
        var companyId = "existing-company";
        var originalDate = DateTime.UtcNow.AddDays(-1);

        context.CompanyEntity.Add(new CompanyEntity
        {
            Id = companyId,
            FirmId = "firm1",
            Name = "Old Name",
            RegNo = "old-reg",
            SaasPlan = "Basic",
            LastSyncDate = originalDate,
            CreatedAt = originalDate,
            UpdatedAt = originalDate
        });
        await context.SaveChangesAsync();

        var repo = new CompanyRepository(context);
        var updatedCompany = CreateTestPennylaneCompany(companyId, "Updated Name");
        updatedCompany.RegNo = "new-reg";
        updatedCompany.SupplierInvoiceTransmissionEmails = new[] { "supplier@updated.com" };
        updatedCompany.CustomerInvoiceTransmissionEmails = new[] { "customer@updated.com" };

        // Act
        await repo.SaveCompaniesAsync(new List<PennylaneCompany> { updatedCompany });

        // Assert
        var company = await context.CompanyEntity.FirstAsync(c => c.Id == companyId);
        Assert.Equal("Updated Name", company.Name);
        Assert.Equal("new-reg", company.RegNo);
        Assert.Equal("Premium", company.SaasPlan);
        Assert.Equal("supplier@updated.com", company.SupplierInvoiceTransmissionEmail);
        Assert.Equal("customer@updated.com", company.CustomerInvoiceTransmissionEmail);
        Assert.True(company.LastSyncDate > originalDate);
        Assert.True(company.UpdatedAt > originalDate);
    }

    [Fact]
    public async Task SaveCompaniesAsync_HandlesMultipleCompanies_WithMixedOperations()
    {
        // Arrange
        var context = GetInMemoryContext();
        var existingCompanyId = "existing-company";
        var originalDate = DateTime.UtcNow.AddDays(-1);

        context.CompanyEntity.Add(new CompanyEntity
        {
            Id = existingCompanyId,
            FirmId = "firm1",
            Name = "Existing Company",
            CreatedAt = originalDate,
            UpdatedAt = originalDate
        });
        await context.SaveChangesAsync();

        var repo = new CompanyRepository(context);
        var companies = new List<PennylaneCompany>
        {
            CreateTestPennylaneCompany(existingCompanyId, "Updated Existing Company"),
            CreateTestPennylaneCompany("new-company", "Brand New Company")
        };

        // Act
        await repo.SaveCompaniesAsync(companies);

        // Assert
        var allCompanies = await context.CompanyEntity.ToListAsync();
        Assert.Equal(2, allCompanies.Count);

        var existingCompany = allCompanies.First(c => c.Id == existingCompanyId);
        Assert.Equal("Updated Existing Company", existingCompany.Name);
        Assert.True(existingCompany.UpdatedAt > originalDate);

        var newCompany = allCompanies.First(c => c.Id == "new-company");
        Assert.Equal("Brand New Company", newCompany.Name);
    }

    [Fact]
    public async Task SaveCompaniesAsync_HandlesEmptyList()
    {
        // Arrange
        var context = GetInMemoryContext();
        var repo = new CompanyRepository(context);

        // Act
        await repo.SaveCompaniesAsync(new List<PennylaneCompany>());

        // Assert
        var companiesCount = await context.CompanyEntity.CountAsync();
        Assert.Equal(0, companiesCount);
    }

    [Fact]
    public async Task SaveCompaniesAsync_ShouldUpdateFirmId_WhenCompanyAlreadyExists()
    {
        // Arrange
        var context = GetInMemoryContext();
        var companyId = "existing-company";
        var originalDate = DateTime.UtcNow.AddDays(-1);

        context.CompanyEntity.Add(new CompanyEntity
        {
            Id = companyId,
            FirmId = "OLD",
            Name = "Company",
            CreatedAt = originalDate,
            UpdatedAt = originalDate
        });
        await context.SaveChangesAsync();

        var repo = new CompanyRepository(context);
        var updatedCompany = CreateTestPennylaneCompany(companyId, "Company");
        updatedCompany.FirmId = "NEW";

        // Act
        await repo.SaveCompaniesAsync(new List<PennylaneCompany> { updatedCompany });

        // Assert
        var company = await context.CompanyEntity.FirstAsync(c => c.Id == companyId);
        Assert.Equal("NEW", company.FirmId);
    }

    [Fact]
    public async Task SaveCompaniesAsync_HandlesNullEmailLists()
    {
        // Arrange
        var context = GetInMemoryContext();
        var repo = new CompanyRepository(context);
        var newCompany = CreateTestPennylaneCompany("test-company", "Test Company");
        newCompany.SupplierInvoiceTransmissionEmails = null;
        newCompany.CustomerInvoiceTransmissionEmails = null;

        // Act
        await repo.SaveCompaniesAsync(new List<PennylaneCompany> { newCompany });

        // Assert
        var company = await context.CompanyEntity.FirstAsync(c => c.Id == "test-company");
        Assert.Null(company.SupplierInvoiceTransmissionEmail);
        Assert.Null(company.CustomerInvoiceTransmissionEmail);
    }
}