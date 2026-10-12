using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Pennylane.Application.Consts;
using Pennylane.Application.Consts.Enums;
using Pennylane.Application.Extensions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models;
using Pennylane.Application.Models.Pennylane;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pennylane.WebApi.Controllers;
using Pennylane.WebApi.Models;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.WebApi.Tests.Controllers;

public class PennylaneControllerTest
{
    private PennylaneController _controller;
    private readonly Mock<IPennylaneService> _pennylaneService;

    public PennylaneControllerTest()
    {
        _pennylaneService = new Mock<IPennylaneService>(MockBehavior.Loose);
        _controller = new PennylaneController(_pennylaneService.Object);
    }

    [Fact]
    public async Task CreatePennylaneRoleAsync_ShouldReturnCreatedStatusAndMessage_WhenServiceSucceeds()
    {
        // Arrange
        var role = new PnlRole { AccountId = 1, ContactId = 2, Role = "Admin" };
        _pennylaneService
            .Setup(s => s.InsertPennylaneRoleAsync(It.IsAny<PnlRole>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await _controller.CreatePennylaneRoleAsync(role);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        objectResult.Value.Should().Be($"Role assigned to contact {role.ContactId} on account {role.AccountId}.");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task CreatePennylaneRoleAsync_ShouldThrowNotFoundException_WhenAccountNotFound()
    {
        // Arrange
        var role = new PnlRole { AccountId = 1, ContactId = 2, Role = "Admin" };
        var errorMessage = string.Format(Errors.AccountNotFoundMessage, role.AccountId);
        _pennylaneService
            .Setup(s => s.InsertPennylaneRoleAsync(It.IsAny<PnlRole>()))
            .ThrowsAsync(new NotFoundException(Errors.AccountNotFoundCode, errorMessage))
            .Verifiable();

        // Act & Assert
        var act = () => _controller.CreatePennylaneRoleAsync(role);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{errorMessage}*");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task CreatePennylaneRoleAsync_ShouldThrowNotFoundException_WhenContactNotFound()
    {
        // Arrange
        var role = new PnlRole { AccountId = 1, ContactId = 2, Role = "Admin" };
        var errorMessage = string.Format(Errors.ContactNotFoundMessage, role.ContactId);
        _pennylaneService
            .Setup(s => s.InsertPennylaneRoleAsync(It.IsAny<PnlRole>()))
            .ThrowsAsync(new NotFoundException(Errors.ContactNotFoundCode, errorMessage))
            .Verifiable();

        // Act & Assert
        var act = () => _controller.CreatePennylaneRoleAsync(role);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{errorMessage}*");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GetPennylaneRoleAsync_ShouldReturnOkWithRole()
    {
        // Arrange
        var contactId = 1;
        var accountId = 1;
        var role = new PnlRole { AccountId = accountId, ContactId = contactId, Role = "Admin" };

        _pennylaneService
            .Setup(s => s.GetPennylaneRoleAsync(contactId, accountId))
            .ReturnsAsync(role)
            .Verifiable();

        // Act
        var result = await _controller.GetPennylaneRoleAsync(contactId, accountId);

        // Assert
        var okResult = result.Result.As<OkObjectResult>();
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().Be(role);
        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GetRoles_WhenCompanyIdIsNull_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetRoles(null);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetRoles_WhenSupportedSaasPlansIsMissing_ReturnsTheListOfv1FreeSaas()
    {
        // Arrange
        var companyId = "validCompanyId";
        var company = new PennylaneCompany { SaasPlan = "notAValidSaas" };
        _pennylaneService.Setup(s => s.GetCompanyByIdAsync(companyId))
            .ReturnsAsync(company);

        // Act
        var result = await _controller.GetRoles(companyId);

        // Assert
        Assert.Equal(4, PennylaneRoles.RoleSaasDictionary[SaasNames.v1_saas_free.GetDescription()].Count);
        Assert.True(PennylaneRoles.RoleSaasDictionary[SaasNames.v1_saas_free.GetDescription()].All(result.Value!.Contains));
    }

    [Fact]
    public async Task GetRoles_WhenCompanyIdIsValidAndSsasNamesIsValid_ReturnsRolesofTheSelectedSaasName()
    {
        // Arrange
        var companyId = "validCompanyId";
        var company = new PennylaneCompany { SaasPlan = SaasNames.v1_pro_internalized.GetDescription() };
        _pennylaneService.Setup(s => s.GetCompanyByIdAsync(companyId))
            .ReturnsAsync(company);

        // Act
        var result = await _controller.GetRoles(companyId);

        // Assert
        Assert.Equal(6, result.Value!.Count);
        Assert.True(PennylaneRoles.RoleSaasDictionary[SaasNames.v1_pro_internalized.GetDescription()].All(x => result.Value.Contains(x)));
    }

    [Fact]
    public async Task GetRoles_WhenAccountNotFound_ReturnsNotFoundObjectResult()
    {
        var companyId = "validCompanyId";
        _pennylaneService.Setup(s => s.GetCompanyByIdAsync(companyId!))
            .ReturnsAsync((PennylaneCompany)null!);

        // Act
        var result = await _controller.GetRoles(companyId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>().Which.StatusCode!.Value.Equals(404);
    }

    [Fact]
    public async Task GetCompanyEmailsByAccountNumberAsync_WhenCompanyExistsOnPennylane_ReturnsCompanyEmails()
    {
        var accountNumber = "AccountNumber";
        var customerInvoiceTransmissionEmail = "CustomerInvoiceTransmissionEmail";
        var supplierInvoiceTransmissionEmail = "SupplierInvoiceTransmissionEmail";
        var expected = new OkObjectResult(new PennylaneEmails(accountNumber, customerInvoiceTransmissionEmail, supplierInvoiceTransmissionEmail));

        _pennylaneService.Setup(s => s.GetPennylaneEmailsAsync(accountNumber))
            .ReturnsAsync(new PennylaneEmails(accountNumber, customerInvoiceTransmissionEmail, supplierInvoiceTransmissionEmail));

        // Act
        var result = await _controller.GetCompanyEmailsByAccountNumberAsync(accountNumber);

        // Assert
        result.Result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetCompanyEmailsByAccountNumberAsync_WhenCompanyDoesntExistOnPennylane_ReturnsNull()
    {
        var accountNumber = "AccountNumber";
        var expected = new NotFoundObjectResult("Account Not Found");

        _pennylaneService.Setup(s => s.GetPennylaneEmailsAsync(accountNumber))
            .ReturnsAsync((PennylaneEmails?)null);

        // Act
        var result = await _controller.GetCompanyEmailsByAccountNumberAsync(accountNumber);

        // Assert
        result.Result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenCompanyIsNewlyCreated_ReturnsCreatedStatusWithResult()
    {
        // Arrange
        var accountId = 123;
        var contactIds = new List<int> { 1, 2, 3 };
        var request = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = contactIds,
            NotYetRegistered = false,
            RegistrationNumber = "123456789"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            RegNo = "123456789",
            TradeName = "Test Trade",
            Address = "123 Test St",
            City = "Test City",
            PostalCode = "12345",
            CountryAlpha2 = "FR",
            SaasPlan = "v1_saas_free",
            FiscalCategory = "category",
            FiscalRegime = "regime",
            LegalFormCode = "legal",
            VatFrequency = "monthly"
        };

        var userResults = new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "user1@test.com", Success = true },
                new UserAdditionResult { ContactId = 2, Email = "user2@test.com", Success = true },
                new UserAdditionResult { ContactId = 3, Email = "user3@test.com", Success = true }
            };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, contactIds))
            .ReturnsAsync(userResults)
            .Verifiable();

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Created);
        resultValue.Company.Should().BeEquivalentTo(company);
        resultValue.UserResults.Should().HaveCount(3);
        resultValue.UserResults.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenCompanyAlreadyExists_ReturnsOkStatusWithResult()
    {
        // Arrange
        var accountId = 123;
        var contactIds = new List<int> { 1, 2 };
        var request = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = contactIds,
            NotYetRegistered = false,
            RegistrationNumber = "123456789"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Existing Company",
            FirmId = "firm1",
            RegNo = "123456789",
            TradeName = "Existing Trade",
            Address = "123 Existing St",
            City = "Existing City",
            PostalCode = "12345",
            CountryAlpha2 = "FR",
            SaasPlan = "v1_saas_free",
            FiscalCategory = "category",
            FiscalRegime = "regime",
            LegalFormCode = "legal",
            VatFrequency = "monthly"
        };

        var userResults = new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "user1@test.com", Success = true },
                new UserAdditionResult { ContactId = 2, Email = "user2@test.com", Success = true }
            };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, true, false, false, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, contactIds))
            .ReturnsAsync(userResults)
            .Verifiable();

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.AlreadyExists);
        resultValue.Company.Should().BeEquivalentTo(company);
        resultValue.UserResults.Should().HaveCount(2);
        resultValue.UserResults.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenCompanyAlreadyExistsWithoutId_ReturnsOkStatusWithResult()
    {
        // Arrange
        var accountId = 123;
        var contactIds = new List<int> { 1, 2 };
        var request = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = contactIds,
            NotYetRegistered = false,
            RegistrationNumber = "123456789"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync(((PennylaneCompany?)null, true, false, false, false))
            .Verifiable();

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.AlreadyExists);
        resultValue.Company.Should().BeNull();
        resultValue.UserResults.Should().BeNull();

        _pennylaneService.Verify(s => s.AddUsersToCompanyAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>()), Times.Never);
        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenRequiresFirmAssignment_ReturnsConflictWithRequiresFirmAssignmentStatus()
    {
        // Arrange
        var accountId = 123;
        var contactIds = new List<int> { 1, 2, 3 };
        var request = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = contactIds
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync(((PennylaneCompany?)null, false, false, true, false))
            .Verifiable();

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ConflictObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.RequiresFirmAssignment);
        resultValue.Company.Should().BeNull();
        resultValue.UserResults.Should().BeNull();

        _pennylaneService.Verify(s => s.AddUsersToCompanyAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>()), Times.Never);
        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenRequiresFirmTransfer_ReturnsConflictWithRequiresFirmTransferStatus()
    {
        // Arrange
        var accountId = 123;
        var contactIds = new List<int> { 1, 2, 3 };
        var request = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = contactIds
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync(((PennylaneCompany?)null, false, false, false, true))
            .Verifiable();

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ConflictObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.RequiresFirmTransfer);
        resultValue.Company.Should().BeNull();
        resultValue.UserResults.Should().BeNull();

        _pennylaneService.Verify(s => s.AddUsersToCompanyAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>()), Times.Never);
        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenSomeUsersFailToBeAdded_ReturnsMultiStatusWithPartialSuccess()
    {
        // Arrange
        var accountId = 123;
        var contactIds = new List<int> { 1, 2, 3 };
        var request = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = contactIds
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            RegNo = "123456789",
            TradeName = "Test Trade",
            Address = "123 Test St",
            City = "Test City",
            PostalCode = "12345",
            CountryAlpha2 = "FR",
            SaasPlan = "v1_saas_free",
            FiscalCategory = "category",
            FiscalRegime = "regime",
            LegalFormCode = "legal",
            VatFrequency = "monthly"
        };

        var userResults = new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "user1@test.com", Success = true },
                new UserAdditionResult { ContactId = 2, Email = "user2@test.com", Success = false, Error = "Contact not found" },
                new UserAdditionResult { ContactId = 3, Email = "user3@test.com", Success = true }
            };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, contactIds))
            .ReturnsAsync(userResults)
            .Verifiable();

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status207MultiStatus);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.PartialSuccess);
        resultValue.Company.Should().BeEquivalentTo(company);
        resultValue.UserResults.Should().HaveCount(3);
        resultValue.UserResults.Should().Contain(r => !r.Success && r.Error == "Contact not found");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenNoContactsProvided_ReturnsCreatedWithEmptyUserResults()
    {
        // Arrange
        var accountId = 123;
        var request = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = new List<int>()
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            RegNo = "123456789",
            TradeName = "Test Trade",
            Address = "123 Test St",
            City = "Test City",
            PostalCode = "12345",
            CountryAlpha2 = "FR",
            SaasPlan = "v1_saas_free",
            FiscalCategory = "category",
            FiscalRegime = "regime",
            LegalFormCode = "legal",
            VatFrequency = "monthly"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>())
            .Verifiable();

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Created);
        resultValue.Company.Should().BeEquivalentTo(company);
        resultValue.UserResults.Should().BeNull();

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenAllUsersFailToBeAdded_ReturnsMultiStatusWithPartialSuccess()
    {
        // Arrange
        var accountId = 123;
        var contactIds = new List<int> { 1, 2 };

        var request = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = contactIds,
            NotYetRegistered = false,
            RegistrationNumber = "123456789"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            RegNo = "123456789",
            TradeName = "Test Trade",
            Address = "123 Test St",
            City = "Test City",
            PostalCode = "12345",
            CountryAlpha2 = "FR",
            SaasPlan = "v1_saas_free",
            FiscalCategory = "category",
            FiscalRegime = "regime",
            LegalFormCode = "legal",
            VatFrequency = "monthly",
            NotYetRegistered = false,

        };

        var userResults = new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "user1@test.com", Success = false, Error = "Contact not found" },
                new UserAdditionResult { ContactId = 2, Email = "user2@test.com", Success = false, Error = "Invalid email" }
            };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, true, false, false, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, contactIds))
            .ReturnsAsync(userResults)
            .Verifiable();

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status207MultiStatus);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.PartialSuccess);
        resultValue.Company.Should().BeEquivalentTo(company);
        resultValue.UserResults.Should().HaveCount(2);
        resultValue.UserResults.Should().AllSatisfy(r => r.Success.Should().BeFalse());

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenServiceThrowsNotFoundException_PropagatesException()
    {
        // Arrange
        var accountId = 999;
        var request = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = new List<int> { 1 },
            NotYetRegistered = true,
            RegistrationNumber = "987654321"

        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ThrowsAsync(new NotFoundException(Errors.AccountNotFoundCode,
                string.Format(Errors.AccountNotFoundMessage, accountId)))
            .Verifiable();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await _controller.CreateCompanyAsync(request));

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenCompanyCreationFails_ReturnsOkWithToCreateStatus()
    {
        // Arrange
        var accountId = 123;
        var request = new CreateCompanyRequest
        {
            AccountId = accountId,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync(((PennylaneCompany?)null, false, false, false, false))
            .Verifiable();

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.ToCreate);
        resultValue.Company.Should().BeNull();
        resultValue.UserResults.Should().BeNull();

        _pennylaneService.Verify(s => s.AddUsersToCompanyAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>()), Times.Never);
        _pennylaneService.VerifyAll();
    }

    #region CreateCompanyAsync Plan Comparison Tests

    [Fact]
    public async Task CreateCompanyAsync_WhenPlanMatchesRequestedPlan_ReturnsValidatedStatus()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = "COLLABORATIVE"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_saas_free"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Validated);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenPlanIsPremiumInternal_ReturnsValidatedStatus()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = "PREMIUM_INTERNAL"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_pro_internalized"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Validated);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenPlanDoesNotMatch_ReturnsCreatedStatus()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = "COLLABORATIVE"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_basic"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Created);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenRequestedPlanCodeIsNull_ReturnsCreatedStatus()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = null
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_saas_free"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Created);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenCreatedOnDefaultAndPlanMatches_ReturnsValidatedStatus()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = "BASIC"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_basic"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, true, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Validated);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenPartialSuccessAndPlanMatches_DoesNotReturnValidatedStatus()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1, 2 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = "COLLABORATIVE"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_saas_free"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u1@test.com", Success = true },
                new UserAdditionResult { ContactId = 2, Email = "u2@test.com", Success = false, Error = "Failed" }
            });

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.PartialSuccess);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenUnknownPlanCode_ReturnsCreatedStatus()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = "UNKNOWN_PLAN"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_saas_free"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Created);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenAlreadyExistsAndPlanMatches_ReturnsValidatedStatus()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = "COLLABORATIVE"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_saas_free"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, true, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Validated);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenAlreadyExistsAndPlanDoesNotMatch_ReturnsAlreadyExistsStatus()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = "COLLABORATIVE"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_basic"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, true, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        // Act
        var result = await _controller.CreateCompanyAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.AlreadyExists);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenCompanySaasPlanIsNull_ReturnsCreatedStatus()
    {
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = "COLLABORATIVE"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = null!
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        var result = await _controller.CreateCompanyAsync(request);

        var objectResult = result.Result.As<ObjectResult>();
        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Created);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenRequestedPlanCodeIsEmpty_ReturnsCreatedStatus()
    {
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = ""
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_saas_free"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        var result = await _controller.CreateCompanyAsync(request);

        var objectResult = result.Result.As<ObjectResult>();
        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Created);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenCreatedOnDefaultAndPlanDoesNotMatch_ReturnsCreatedOnDefaultStatus()
    {
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = "PREMIUM"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_basic"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, true, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        var result = await _controller.CreateCompanyAsync(request);

        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.CreatedOnDefault);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenPlanMatchesCaseInsensitive_ReturnsValidatedStatus()
    {
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = "COLLABORATIVE"
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "V1_SAAS_FREE"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, false, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        var result = await _controller.CreateCompanyAsync(request);

        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Validated);
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenAlreadyExistsAndRequestedPlanCodeIsNull_ReturnsAlreadyExistsStatus()
    {
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 1 },
            NotYetRegistered = false,
            RegistrationNumber = "123456789",
            RequestedPlanCode = null
        };

        var company = new PennylaneCompany
        {
            Id = "ACC123",
            Name = "Test Company",
            FirmId = "firm1",
            SaasPlan = "v1_saas_free"
        };

        _pennylaneService
            .Setup(s => s.CreateCollaboratorsAndGetPennylaneIdsAsync(It.IsAny<IReadOnlyList<ContactFunctions>?>(), It.IsAny<string?>()))
            .ReturnsAsync((null, null, null));

        _pennylaneService
            .Setup(s => s.CreateCompanyOnlyAsync(request))
            .ReturnsAsync((company, true, false, false, false));

        _pennylaneService
            .Setup(s => s.AddUsersToCompanyAsync(company.Id, It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(new List<UserAdditionResult>
            {
                new UserAdditionResult { ContactId = 1, Email = "u@test.com", Success = true }
            });

        var result = await _controller.CreateCompanyAsync(request);

        var objectResult = result.Result.As<OkObjectResult>();
        var resultValue = objectResult.Value.As<CreateCompanyResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.AlreadyExists);
    }

    #endregion

    #region GrantPennylaneAccessAsync Tests

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenCompanyNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync((PennylaneCompany?)null)
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<NotFoundObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Failed);
        resultValue.Error.Should().Be("Company not found");
        resultValue.ContactId.Should().Be(request.ContactId);
        resultValue.AccountId.Should().Be(request.AccountId);

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenUserAlreadyExistsInCompany_ReturnsOkWithAlreadyHasAccess()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var existingUser = new PennylaneUsers { Id = "user123", Email = "test@example.com" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync((existingUser, true))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.InsertUserPennylaneRole(request, existingUser.Id, company.Id))
            .ReturnsAsync(true)
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.AlreadyHasAccess);
        resultValue.PennylaneUserId.Should().Be(existingUser.Id);
        resultValue.PennylaneCompanyId.Should().Be(company.Id);

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenNewUserCreated_ReturnsCreatedWithCreatedStatus()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var newUserId = "newUser456";

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .SetupSequence(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync(((PennylaneUsers?)null, false))
            .ReturnsAsync((new PennylaneUsers { Id = newUserId }, false));

        _pennylaneService
            .Setup(s => s.CreatePennylaneUserAsync(request.ContactId))
            .ReturnsAsync((newUserId, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.InsertUserPennylaneRole(request, newUserId, company.Id))
            .ReturnsAsync(true)
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Created);
        resultValue.PennylaneUserId.Should().Be(newUserId);
        resultValue.PennylaneCompanyId.Should().Be(company.Id);
        resultValue.Message.Should().Contain("User created successfully");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenUserCreationReturnsNull_ReturnsBadRequest()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync(((PennylaneUsers?)null, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreatePennylaneUserAsync(request.ContactId))
            .ReturnsAsync(((string?)null, false))
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<BadRequestObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Failed);
        resultValue.Error.Should().Be("User creation failed");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenUserCreationReturnsEmptyString_ReturnsBadRequest()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync(((PennylaneUsers?)null, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreatePennylaneUserAsync(request.ContactId))
            .ReturnsAsync((string.Empty, false))
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<BadRequestObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Failed);
        resultValue.Error.Should().Be("User creation failed");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenExistingGlobalUserGrantedAccess_ReturnsCreatedWithExistingUserStatus()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var existingGlobalUserId = "globalUser789";

        // User doesn't exist in company but exists globally
        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .SetupSequence(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync(((PennylaneUsers?)null, false))
            .ReturnsAsync((new PennylaneUsers { Id = existingGlobalUserId }, false));

        // CreatePennylaneUserAsync returns existing user ID (user already exists globally)
        _pennylaneService
            .Setup(s => s.CreatePennylaneUserAsync(request.ContactId))
            .ReturnsAsync((existingGlobalUserId, true))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.InsertUserPennylaneRole(request, existingGlobalUserId, company.Id))
            .ReturnsAsync(true)
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Created);
        resultValue.PennylaneUserId.Should().Be(existingGlobalUserId);
        resultValue.PennylaneCompanyId.Should().Be(company.Id);

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_SetsCorrectRoleInResult()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "internal_accountant" };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var newUserId = "user123";

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .SetupSequence(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync(((PennylaneUsers?)null, false))
            .ReturnsAsync((new PennylaneUsers { Id = newUserId }, false));

        _pennylaneService
            .Setup(s => s.CreatePennylaneUserAsync(request.ContactId))
            .ReturnsAsync((newUserId, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.InsertUserPennylaneRole(request, newUserId, company.Id))
            .ReturnsAsync(true)
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();
        resultValue.Role.Should().Be("internal_accountant");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenUserAlreadyHasAccess_UpdatesRoleMessage()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "executive" };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var existingUser = new PennylaneUsers { Id = "user123", Email = "test@example.com" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync((existingUser, true))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.InsertUserPennylaneRole(request, existingUser.Id, company.Id))
            .ReturnsAsync(true)
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();
        resultValue.Message.Should().Contain("already had access");
        resultValue.Message.Should().Contain("updated");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenKeyNotFoundExceptionThrown_ReturnsNotFound()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ThrowsAsync(new KeyNotFoundException($"Company with ID {request.AccountId} not found."))
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<NotFoundObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Failed);
        resultValue.Error.Should().Be("Company not found");
        resultValue.Steps.Should().NotBeNull();
        resultValue.Steps.Should().Contain(s => s.Contains("Company not found"));

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenUserExistsGloballyButNotInCompany_ReturnsCreatedWithExistingUserStatus()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var globalUser = new PennylaneUsers { Id = "globalUser123", Email = "test@example.com" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        // User exists globally but NOT in this company (alreadyExistsInCompany = false)
        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync((globalUser, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.InsertUserPennylaneRole(request, globalUser.Id, company.Id))
            .ReturnsAsync(true)
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.ExistingUserAccessGranted);
        resultValue.PennylaneUserId.Should().Be(globalUser.Id);
        resultValue.PennylaneCompanyId.Should().Be(company.Id);
        resultValue.Message.Should().Contain("Existing Pennylane user granted access");
        resultValue.Steps.Should().NotBeNull();
        resultValue.Steps.Should().Contain(s => s.Contains("Company found"));
        resultValue.Steps.Should().Contain(s => s.Contains("exists in Pennylane") && s.Contains("but not in this company"));

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenSuccessful_ReturnsStepsInResult()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var newUserId = "newUser456";

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .SetupSequence(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync(((PennylaneUsers?)null, false))
            .ReturnsAsync((new PennylaneUsers { Id = newUserId }, false));

        _pennylaneService
            .Setup(s => s.CreatePennylaneUserAsync(request.ContactId))
            .ReturnsAsync((newUserId, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.InsertUserPennylaneRole(request, newUserId, company.Id))
            .ReturnsAsync(true)
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<ObjectResult>();
        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();

        resultValue.Steps.Should().NotBeNull();
        resultValue.Steps.Should().HaveCount(3);
        resultValue.Steps.Should().Contain(s => s.Contains("Company found"));
        resultValue.Steps.Should().Contain(s => s.Contains("User created"));
        resultValue.Steps.Should().Contain(s => s.Contains("Role") && s.Contains("assigned"));

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenUserAlreadyInCompany_StepsContainAlreadyExists()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var existingUser = new PennylaneUsers { Id = "user123", Email = "test@example.com" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync((existingUser, true))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.InsertUserPennylaneRole(request, existingUser.Id, company.Id))
            .ReturnsAsync(true)
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();

        resultValue.Steps.Should().NotBeNull();
        resultValue.Steps.Should().Contain(s => s.Contains("Company found"));
        resultValue.Steps.Should().Contain(s => s.Contains("User already exists in company"));
        resultValue.Steps.Should().Contain(s => s.Contains("Role") && s.Contains("assigned"));

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenUserCreationFails_StepsContainFailure()
    {
        // Arrange
        var request = new PnlRole { AccountId = 1, ContactId = 2, Role = "employee" };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync(((PennylaneUsers?)null, false))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.CreatePennylaneUserAsync(request.ContactId))
            .ReturnsAsync(((string?)null, false))
            .Verifiable();

        // Act
        var result = await _controller.GrantPennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<BadRequestObjectResult>();
        var resultValue = objectResult.Value.As<GrantPennylaneAccessResult>();

        resultValue.Steps.Should().NotBeNull();
        resultValue.Steps.Should().Contain(s => s.Contains("Company found"));
        resultValue.Steps.Should().Contain(s => s.Contains("Failed to create user"));
        resultValue.Message.Should().Contain("Company found but failed");

        _pennylaneService.VerifyAll();
    }

    #endregion

    #region RevokePennylaneAccessAsync Tests

    [Fact]
    public async Task RevokePennylaneAccessAsync_WhenCompanyNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new RevokeAccessRequest { AccountId = 1, ContactId = 2 };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync((PennylaneCompany?)null)
            .Verifiable();

        // Act
        var result = await _controller.RevokePennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<NotFoundObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var resultValue = objectResult.Value.As<RevokePennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Failed);
        resultValue.Error.Should().Be("Company not found");
        resultValue.ContactId.Should().Be(request.ContactId);
        resultValue.AccountId.Should().Be(request.AccountId);

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_WhenKeyNotFoundExceptionThrown_ReturnsNotFound()
    {
        // Arrange
        var request = new RevokeAccessRequest { AccountId = 1, ContactId = 2 };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ThrowsAsync(new KeyNotFoundException($"Company with ID {request.AccountId} not found."))
            .Verifiable();

        // Act
        var result = await _controller.RevokePennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<NotFoundObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var resultValue = objectResult.Value.As<RevokePennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Failed);
        resultValue.Error.Should().Be("Company not found");
        resultValue.Steps.Should().NotBeNull();
        resultValue.Steps.Should().Contain(s => s.Contains("Company not found"));

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_WhenUserNotFoundInCompany_ReturnsOkWithUserNotFoundStatus()
    {
        // Arrange
        var request = new RevokeAccessRequest { AccountId = 1, ContactId = 2 };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync(((PennylaneUsers?)null, false))
            .Verifiable();

        // Act
        var result = await _controller.RevokePennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var resultValue = objectResult.Value.As<RevokePennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.UserNotFound);
        resultValue.PennylaneCompanyId.Should().Be(company.Id);
        resultValue.Message.Should().Contain("was not found");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_WhenUserExistsButNotInCompany_ReturnsOkWithUserNotFoundStatus()
    {
        // Arrange
        var request = new RevokeAccessRequest { AccountId = 1, ContactId = 2 };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var globalUser = new PennylaneUsers { Id = "globalUser123", Email = "test@example.com" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        // User exists globally but NOT in this company
        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync((globalUser, false))
            .Verifiable();

        // Act
        var result = await _controller.RevokePennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var resultValue = objectResult.Value.As<RevokePennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.UserNotFound);
        resultValue.Message.Should().Contain("was not found");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_WhenUserExistsInCompany_ReturnsOkWithRevokedStatus()
    {
        // Arrange
        var request = new RevokeAccessRequest { AccountId = 1, ContactId = 2 };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var existingUser = new PennylaneUsers { Id = "user123", Email = "test@example.com" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync((existingUser, true))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.RevokeUserAccessAsync(company.Id, existingUser.Id))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.DeletePennylaneRoleAsync(request.AccountId, request.ContactId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await _controller.RevokePennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var resultValue = objectResult.Value.As<RevokePennylaneAccessResult>();
        resultValue.Status.Should().Be(PennylaneStatuses.Revoked);
        resultValue.PennylaneUserId.Should().Be(existingUser.Id);
        resultValue.PennylaneCompanyId.Should().Be(company.Id);
        resultValue.Message.Should().Contain("successfully revoked");

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_WhenSuccessful_ReturnsStepsInResult()
    {
        // Arrange
        var request = new RevokeAccessRequest { AccountId = 1, ContactId = 2 };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var existingUser = new PennylaneUsers { Id = "user123", Email = "test@example.com" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync((existingUser, true))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.RevokeUserAccessAsync(company.Id, existingUser.Id))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.DeletePennylaneRoleAsync(request.AccountId, request.ContactId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await _controller.RevokePennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        var resultValue = objectResult.Value.As<RevokePennylaneAccessResult>();

        resultValue.Steps.Should().NotBeNull();
        resultValue.Steps.Should().HaveCount(4);
        resultValue.Steps.Should().Contain(s => s.Contains("Company found"));
        resultValue.Steps.Should().Contain(s => s.Contains("User found in company"));
        resultValue.Steps.Should().Contain(s => s.Contains("Access revoked"));
        resultValue.Steps.Should().Contain(s => s.Contains("Local role record deleted"));

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_WhenUserNotFound_StepsContainUserNotFound()
    {
        // Arrange
        var request = new RevokeAccessRequest { AccountId = 1, ContactId = 2 };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync(((PennylaneUsers?)null, false))
            .Verifiable();

        // Act
        var result = await _controller.RevokePennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        var resultValue = objectResult.Value.As<RevokePennylaneAccessResult>();

        resultValue.Steps.Should().NotBeNull();
        resultValue.Steps.Should().HaveCount(2);
        resultValue.Steps.Should().Contain(s => s.Contains("Company found"));
        resultValue.Steps.Should().Contain(s => s.Contains("User not found in company"));

        _pennylaneService.VerifyAll();
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_SetsCorrectContactAndAccountInResult()
    {
        // Arrange
        var request = new RevokeAccessRequest { AccountId = 42, ContactId = 99 };
        var company = new PennylaneCompany { Id = "ACC001", Name = "Test Company" };
        var existingUser = new PennylaneUsers { Id = "user123", Email = "test@example.com" };

        _pennylaneService
            .Setup(s => s.GetCompanyByAccountIdAsync(request.AccountId))
            .ReturnsAsync(company)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.GetPennylaneCompanyUser(company.Id, request.ContactId))
            .ReturnsAsync((existingUser, true))
            .Verifiable();

        _pennylaneService
            .Setup(s => s.RevokeUserAccessAsync(company.Id, existingUser.Id))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _pennylaneService
            .Setup(s => s.DeletePennylaneRoleAsync(request.AccountId, request.ContactId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await _controller.RevokePennylaneAccessAsync(request);

        // Assert
        var objectResult = result.Result.As<OkObjectResult>();
        var resultValue = objectResult.Value.As<RevokePennylaneAccessResult>();

        resultValue.ContactId.Should().Be(99);
        resultValue.AccountId.Should().Be(42);

        _pennylaneService.VerifyAll();
    }

    #endregion

}
