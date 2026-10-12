using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Notifications.Commons.WebApi;
using Notifications.Commons.WebApi.QueryParams;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Models.Signatory;
using Pennylane.Application.Services;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Domain.Enums;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Application.Tests.Services;

public class ComplianceEmailServiceTests
{
    private readonly Mock<INotificationManager> _notificationManagerMock;
    private readonly Mock<ISignatoryAccessesImportRepository> _importRepositoryMock;
    private readonly Mock<IBlobStorageManager> _blobStorageManagerMock;
    private readonly Mock<IOptions<ComplianceEmailOptions>> _optionsMock;
    private readonly Mock<ILogger<ComplianceEmailService>> _loggerMock;
    private readonly ComplianceEmailService _sut;
    private readonly ComplianceEmailOptions _options;
    private readonly Fixture _fixture;

    public ComplianceEmailServiceTests()
    {
        _notificationManagerMock = new Mock<INotificationManager>();
        _importRepositoryMock = new Mock<ISignatoryAccessesImportRepository>();
        _blobStorageManagerMock = new Mock<IBlobStorageManager>();
        _optionsMock = new Mock<IOptions<ComplianceEmailOptions>>();
        _loggerMock = new Mock<ILogger<ComplianceEmailService>>();

        _options = new ComplianceEmailOptions
        {
            AcceptanceTemplateName = "Pennylane compliance acceptance",
            RefusalTemplateName = "Pennylane compliance refusal",
            SenderEmail = "noreply@pulse.fr",
            LoginPage = "https://app.rydge.fr/pennylane/restore",
            ServiceBusTopic = "feedcenter",
            CustomerRelationRefusalRecipientEmail = "Relation-Client@rydge.fr",
            CustomerRelationRefusalTemplateName = "Pennylane compliance refusal customer relation",
            BccEmail =  "copie@email.fr"
        };
        _optionsMock.Setup(x => x.Value).Returns(_options);

        _sut = new ComplianceEmailService(
            _notificationManagerMock.Object,
            _importRepositoryMock.Object,
            _blobStorageManagerMock.Object,
            _optionsMock.Object,
            _loggerMock.Object);

        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [Fact]
    public async Task SendAcceptanceEmailAsync_ShouldPublishEmailEvent()
    {
        // Arrange
        var context = CreateContext();
        EmailOnlySenderEvent? capturedEvent = null;

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedEvent = evt as EmailOnlySenderEvent)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        _notificationManagerMock.Verify(
            x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), _options.ServiceBusTopic),
            Times.Once);
        capturedEvent.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAcceptanceEmailAsync_ShouldBuildCorrectPayload()
    {
        // Arrange
        var context = CreateContext();
        EmailRequest? capturedRequest = null;

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.From.Should().Be(_options.SenderEmail);
        capturedRequest.To.Should().ContainSingle().Which.Should().Be(context.RecipientEmail);
        capturedRequest.TemplateName.Should().Be(_options.AcceptanceTemplateName);
        capturedRequest.Variables.Should().ContainKey("userName");
        capturedRequest.Variables.Should().ContainKey("acceptanceDate");
        capturedRequest.Variables.Should().ContainKey("freeTrialEndDate");
        capturedRequest.Variables.Should().ContainKey("companies");
        capturedRequest.Variables.Should().ContainKey("isNewComplianceFlow");
        capturedRequest.Variables.Should().ContainKey("isFreeTrialExpired");
        capturedRequest.Variables["freeTrialEndDate"].Should().Be(context.FreeTrialEndDate);
        capturedRequest.Variables["isNewComplianceFlow"].Should().Be(context.IsNewComplianceFlow);
        capturedRequest.Variables["isFreeTrialExpired"].Should().Be(context.IsFreeTrialExpired);
        capturedRequest.Bcc.Should().Contain(_options.BccEmail);
    }

    [Fact]
    public async Task SendAcceptanceEmailAsync_WhenNoFirstName_ShouldUseDefaultUserName()
    {
        // Arrange
        var context = new ComplianceEmailContext
        {
            RecipientEmail = "test@example.com",
            UserFirstName = "",
            UserLastName = "",
            FreeTrialEndDate = "30 juin 2026",
            IsNewComplianceFlow = false,
            IsFreeTrialExpired = false,
            Companies = new List<ComplianceCompanyInfo>
            {
                new()
                {
                    AccountNumber = "ACC001",
                    Name = "Test Company",
                    CompliancePriceExclTax = 150m,
                    SubscriptionPriceExclTax = 49m
                }
            }
        };

        EmailRequest? capturedRequest = null;

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Variables["userName"].Should().Be("Client");
    }

    [Fact]
    public async Task SendAcceptanceEmailAsync_ShouldFormatPricesCorrectly()
    {
        // Arrange
        var context = new ComplianceEmailContext
        {
            RecipientEmail = "test@example.com",
            UserFirstName = "Jean",
            UserLastName = "Dupont",
            FreeTrialEndDate = "30 juin 2026",
            IsNewComplianceFlow = false,
            IsFreeTrialExpired = false,
            Companies = new List<ComplianceCompanyInfo>
            {
                new()
                {
                    AccountNumber = "ACC001",
                    Name = "Entreprise A",
                    CompliancePriceExclTax = 150m,
                    SubscriptionPriceExclTax = 49.50m
                },
                new()
                {
                    AccountNumber = "ACC002",
                    Name = "Entreprise B",
                    CompliancePriceExclTax = null,
                    SubscriptionPriceExclTax = null
                }
            }
        };

        EmailRequest? capturedRequest = null;

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        var companies = capturedRequest!.Variables["companies"] as IEnumerable<object>;
        companies.Should().NotBeNull();
        companies.Should().HaveCount(2);

        var companiesList = companies!.ToList();
        var company1 = companiesList[0];
        var company2 = companiesList[1];

        company1.GetType().GetProperty("compliancePrice")!.GetValue(company1).Should().Be("150€ HT");
        company1.GetType().GetProperty("subscriptionPrice")!.GetValue(company1).Should().Be("49,50€ HT / mois");
        company1.GetType().GetProperty("complianceText")!.GetValue(company1).Should().Be("- Accompagnement RFE :");
        company2.GetType().GetProperty("compliancePrice")!.GetValue(company2).Should().Be(string.Empty);
        company2.GetType().GetProperty("subscriptionPrice")!.GetValue(company2).Should().Be("Offert");
        company2.GetType().GetProperty("complianceText")!.GetValue(company2).Should().Be(string.Empty);
    }

    [Fact]
    public async Task SendAcceptanceEmailAsync_ShouldBuildUserNameCorrectly()
    {
        // Arrange
        var context = new ComplianceEmailContext
        {
            RecipientEmail = "test@example.com",
            UserFirstName = "  Jean  ",
            UserLastName = "  Dupont  ",
            FreeTrialEndDate = "30 juin 2026",
            IsNewComplianceFlow = false,
            IsFreeTrialExpired = false,
            Companies = new List<ComplianceCompanyInfo>
            {
                new()
                {
                    AccountNumber = "ACC001",
                    Name = "Test Company",
                    CompliancePriceExclTax = 150m,
                    SubscriptionPriceExclTax = 49m
                }
            }
        };

        EmailRequest? capturedRequest = null;

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Variables["userName"].Should().Be("Jean Dupont");
    }

    [Theory]
    [InlineData("JEAN", "DUPONT", "Jean Dupont")]
    [InlineData("jean", "dupont", "Jean Dupont")]
    [InlineData("jEaN", "dUpOnT", "Jean Dupont")]
    [InlineData("MARIE-CLAIRE", "DE LA FONTAINE", "Marie-Claire De La Fontaine")]
    public async Task SendAcceptanceEmailAsync_ShouldCapitalizeUserName(string firstName, string lastName, string expectedUserName)
    {
        // Arrange
        var context = new ComplianceEmailContext
        {
            RecipientEmail = "test@example.com",
            UserFirstName = firstName,
            UserLastName = lastName,
            FreeTrialEndDate = "30 juin 2026",
            IsNewComplianceFlow = false,
            IsFreeTrialExpired = false,
            Companies = new List<ComplianceCompanyInfo>
            {
                new()
                {
                    AccountNumber = "ACC001",
                    Name = "Test Company",
                    CompliancePriceExclTax = 150m,
                    SubscriptionPriceExclTax = 49m
                }
            }
        };

        EmailRequest? capturedRequest = null;

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Variables["userName"].Should().Be(expectedUserName);
    }

    [Theory]
    [InlineData("JEAN", "DUPONT", "Jean Dupont")]
    [InlineData("jean", "dupont", "Jean Dupont")]
    [InlineData("jEaN", "dUpOnT", "Jean Dupont")]
    public async Task SendRefusalEmailAsync_ShouldCapitalizeUserName(string firstName, string lastName, string expectedUserName)
    {
        // Arrange
        var context = new ComplianceRefusalContext
        {
            RecipientEmail = "test@example.com",
            UserFirstName = firstName,
            UserLastName = lastName,
            FreeTrialEndDate = "30 juin 2026",
            IsNewComplianceFlow = false,
            IsFreeTrialExpired = false
        };

        EmailRequest? capturedRequest = null;

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendRefusalEmailAsync(context);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Variables["userName"].Should().Be(expectedUserName);
    }

    [Theory]
    [InlineData("JOHN", "DOE", "John Doe")]
    [InlineData("john", "doe", "John Doe")]
    [InlineData("jOhN", "dOe", "John Doe")]
    public async Task SendCustomerRelationRefusalEmailAsync_ShouldCapitalizeClientFullName(string firstName, string lastName, string expectedFullName)
    {
        // Arrange
        var context = new CustomerRelationRefusalContext
        {
            UserFirstName = firstName,
            UserLastName = lastName,
            UserEmail = "john.doe@test.com",
            AccountNumbers = ["ACC1"]
        };

        EmailRequest? capturedRequest = null;

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendCustomerRelationRefusalEmailAsync(context);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Variables["clientFullName"].Should().Be(expectedFullName);
    }

    [Fact]
    public async Task SendAcceptanceEmailAsync_WhenTermsExist_ShouldAttachPdf()
    {
        // Arrange
        var context = CreateContext();
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var terms = new TermsEntity { Id = 1, LinkFr = "cgu/cgu-pennylane-2025.pdf", CreationDate = DateTime.UtcNow };

        var memoryStream = new MemoryStream(pdfContent);
        var fileStreamResult = new FileStreamResult(memoryStream, "application/pdf") { FileDownloadName = "cgu-pennylane-2025.pdf" };

        _blobStorageManagerMock
            .Setup(x => x.GetFileAsync(terms.LinkFr, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileStreamResult);

        EmailRequest? capturedRequest = null;
        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, terms);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Attachments.Should().HaveCount(1);
        capturedRequest.Attachments[0].FileName.Should().Be("cgu-pennylane-2025.pdf");
        capturedRequest.Attachments[0].Type.Should().Be(MailAttachmentType.Pdf);
        capturedRequest.Attachments[0].Content.Should().Be(Convert.ToBase64String(pdfContent));
    }

    [Fact]
    public async Task SendAcceptanceEmailAsync_WhenNoTerms_ShouldNotAttachPdf()
    {
        // Arrange
        var context = CreateContext();

        EmailRequest? capturedRequest = null;
        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAcceptanceEmailAsync_WhenBlobNotFound_ShouldNotAttachPdf()
    {
        // Arrange
        var context = CreateContext();
        var terms = new TermsEntity { Id = 1, LinkFr = "cgu/cgu-pennylane-2025.pdf", CreationDate = DateTime.UtcNow };

        _blobStorageManagerMock
            .Setup(x => x.GetFileAsync(terms.LinkFr, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileStreamResult)null!);

        EmailRequest? capturedRequest = null;
        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAcceptanceEmailAsync_WhenBlobThrowsException_ShouldNotAttachPdfButStillSendEmail()
    {
        // Arrange
        var context = CreateContext();
        var terms = new TermsEntity { Id = 1, LinkFr = "cgu/cgu-pennylane-2025.pdf", CreationDate = DateTime.UtcNow };

        _blobStorageManagerMock
            .Setup(x => x.GetFileAsync(terms.LinkFr, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Blob storage error"));

        EmailRequest? capturedRequest = null;
        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Attachments.Should().BeEmpty();
        _notificationManagerMock.Verify(
            x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), _options.ServiceBusTopic),
            Times.Once);
    }

    [Fact]
    public async Task SendRefusalEmailAsync_ShouldPublishEmailEvent()
    {
        // Arrange
        var context = CreateRefusalContext();

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendRefusalEmailAsync(context);

        // Assert
        _notificationManagerMock.Verify(
            x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), _options.ServiceBusTopic),
            Times.Once);
    }

    [Fact]
    public async Task SendRefusalEmailAsync_ShouldBuildCorrectPayload()
    {
        // Arrange
        var context = CreateRefusalContext();
        EmailRequest? capturedRequest = null;

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendRefusalEmailAsync(context);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.From.Should().Be(_options.SenderEmail);
        capturedRequest.To.Should().ContainSingle().Which.Should().Be(context.RecipientEmail);
        capturedRequest.TemplateName.Should().Be(_options.RefusalTemplateName);
        capturedRequest.Variables.Should().ContainKey("userName");
        capturedRequest.Variables.Should().ContainKey("loginPage");
        capturedRequest.Variables.Should().ContainKey("freeTrialEndDate");
        capturedRequest.Variables.Should().ContainKey("isNewComplianceFlow");
        capturedRequest.Variables.Should().ContainKey("isFreeTrialExpired");
        capturedRequest.Variables["userName"].Should().Be("Jean Dupont");
        capturedRequest.Variables["loginPage"].Should().Be(_options.LoginPage);
        capturedRequest.Variables["freeTrialEndDate"].Should().Be(context.FreeTrialEndDate);
        capturedRequest.Variables["isNewComplianceFlow"].Should().Be(context.IsNewComplianceFlow);
        capturedRequest.Variables["isFreeTrialExpired"].Should().Be(context.IsFreeTrialExpired);
        capturedRequest.Bcc.Should().Contain(_options.BccEmail);
    }

    [Fact]
    public async Task SendAcceptanceEmailAsync_WhenBccConfigured_ShouldIncludeBcc()
    {
        // Arrange
        _options.BccEmail = "test@bcc.hubspot.com";
        var context = CreateContext();
        EmailRequest? capturedRequest = null;

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Bcc.Should().ContainSingle().Which.Should().Be("test@bcc.hubspot.com");
    }

    [Fact]
    public async Task SendRefusalEmailAsync_WhenBccConfigured_ShouldIncludeBcc()
    {
        // Arrange
        _options.BccEmail = "test@bcc.hubspot.com";
        var context = CreateRefusalContext();
        EmailRequest? capturedRequest = null;

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendRefusalEmailAsync(context);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Bcc.Should().ContainSingle().Which.Should().Be("test@bcc.hubspot.com");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAcceptanceEmailAsync_ShouldForwardIsNewComplianceFlag(bool isNewComplianceFlow)
    {
        // Arrange
        var context = new ComplianceEmailContext
        {
            RecipientEmail = "test@example.com",
            UserFirstName = "Jean",
            UserLastName = "Dupont",
            FreeTrialEndDate = "31 octobre 2026",
            IsNewComplianceFlow = isNewComplianceFlow,
            IsFreeTrialExpired = false,
            Companies = new List<ComplianceCompanyInfo>
            {
                new()
                {
                    AccountNumber = "ACC001",
                    Name = "Test Company",
                    CompliancePriceExclTax = 150m,
                    SubscriptionPriceExclTax = 49m
                }
            }
        };

        EmailRequest? capturedRequest = null;
        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Variables["isNewComplianceFlow"].Should().Be(isNewComplianceFlow);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendRefusalEmailAsync_ShouldForwardIsNewComplianceFlag(bool isNewComplianceFlow)
    {
        // Arrange
        var context = new ComplianceRefusalContext
        {
            RecipientEmail = "test@example.com",
            UserFirstName = "Jean",
            UserLastName = "Dupont",
            FreeTrialEndDate = "31 octobre 2026",
            IsNewComplianceFlow = isNewComplianceFlow,
            IsFreeTrialExpired = false
        };

        EmailRequest? capturedRequest = null;
        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendRefusalEmailAsync(context);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Variables["isNewComplianceFlow"].Should().Be(isNewComplianceFlow);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAcceptanceEmailAsync_ShouldForwardIsFreeTrialExpiredFlag(bool isFreeTrialExpired)
    {
        // Arrange
        var context = new ComplianceEmailContext
        {
            RecipientEmail = "test@example.com",
            UserFirstName = "Jean",
            UserLastName = "Dupont",
            FreeTrialEndDate = "31 octobre 2026",
            IsNewComplianceFlow = false,
            IsFreeTrialExpired = isFreeTrialExpired,
            Companies = new List<ComplianceCompanyInfo>
            {
                new()
                {
                    AccountNumber = "ACC001",
                    Name = "Test Company",
                    CompliancePriceExclTax = 150m,
                    SubscriptionPriceExclTax = 49m
                }
            }
        };

        EmailRequest? capturedRequest = null;
        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Variables["isFreeTrialExpired"].Should().Be(isFreeTrialExpired);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendRefusalEmailAsync_ShouldForwardIsFreeTrialExpiredFlag(bool isFreeTrialExpired)
    {
        // Arrange
        var context = new ComplianceRefusalContext
        {
            RecipientEmail = "test@example.com",
            UserFirstName = "Jean",
            UserLastName = "Dupont",
            FreeTrialEndDate = "31 octobre 2026",
            IsNewComplianceFlow = false,
            IsFreeTrialExpired = isFreeTrialExpired
        };

        EmailRequest? capturedRequest = null;
        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendRefusalEmailAsync(context);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Variables["isFreeTrialExpired"].Should().Be(isFreeTrialExpired);
    }

    [Theory]
    [InlineData("Restauration", "Restauration")]
    [InlineData(null, "")]
    public async Task SendAcceptanceEmailAsync_ShouldForwardWave(string? wave, string expectedWave)
    {
        // Arrange
        var context = new ComplianceEmailContext
        {
            RecipientEmail = "test@example.com",
            UserFirstName = "Jean",
            UserLastName = "Dupont",
            FreeTrialEndDate = "31 octobre 2026",
            IsNewComplianceFlow = false,
            IsFreeTrialExpired = false,
            Wave = wave,
            Companies = new List<ComplianceCompanyInfo>
            {
                new()
                {
                    AccountNumber = "ACC001",
                    Name = "Test Company",
                    CompliancePriceExclTax = 150m,
                    SubscriptionPriceExclTax = 49m
                }
            }
        };

        EmailRequest? capturedRequest = null;
        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendAcceptanceEmailAsync(context, _fixture.Build<TermsEntity>().Without(t => t.SignatoryAccessEntity).Without(t => t.ValidationHistoryEntity).Create());

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Variables["wave"].Should().Be(expectedWave);
    }

    [Theory]
    [InlineData("Restauration", "Restauration")]
    [InlineData(null, "")]
    public async Task SendRefusalEmailAsync_ShouldForwardWave(string? wave, string expectedWave)
    {
        // Arrange
        var context = new ComplianceRefusalContext
        {
            RecipientEmail = "test@example.com",
            UserFirstName = "Jean",
            UserLastName = "Dupont",
            FreeTrialEndDate = "31 octobre 2026",
            IsNewComplianceFlow = false,
            IsFreeTrialExpired = false,
            Wave = wave
        };

        EmailRequest? capturedRequest = null;
        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendRefusalEmailAsync(context);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Variables["wave"].Should().Be(expectedWave);
    }

    private static ComplianceEmailContext CreateContext()
    {
        return new ComplianceEmailContext
        {
            RecipientEmail = "jean.dupont@example.com",
            UserFirstName = "Jean",
            UserLastName = "Dupont",
            FreeTrialEndDate = "30 juin 2026",
            IsNewComplianceFlow = false,
            IsFreeTrialExpired = false,
            Companies = new List<ComplianceCompanyInfo>
            {
                new()
                {
                    AccountNumber = "ACC001",
                    Name = "Entreprise A",
                    CompliancePriceExclTax = 150m,
                    SubscriptionPriceExclTax = 49m
                },
                new()
                {
                    AccountNumber = "ACC002",
                    Name = "Entreprise B",
                    CompliancePriceExclTax = 200m,
                    SubscriptionPriceExclTax = 59m
                }
            }
        };
    }

    private static ComplianceRefusalContext CreateRefusalContext()
    {
        return new ComplianceRefusalContext
        {
            RecipientEmail = "jean.dupont@example.com",
            UserFirstName = "Jean",
            UserLastName = "Dupont",
            FreeTrialEndDate = "30 juin 2026",
            IsNewComplianceFlow = false,
            IsFreeTrialExpired = false
        };
    }

    [Fact]
    public async Task SendCustomerRelationRefusalEmailAsync_Nominal()
    {
        EmailRequest? capturedRequest = null;
        var context = new CustomerRelationRefusalContext
        {
            UserFirstName = "John",
            UserLastName = "Doe",
            UserEmail = "john.doe@test.com",
            AccountNumbers = ["ACC1", "ACC2"]
        };

        _notificationManagerMock
            .Setup(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), It.IsAny<string?>()))
            .Callback<BaseEvent<EmailRequest>, string?>((evt, _) => capturedRequest = evt.Data)
            .Returns(Task.CompletedTask);

        await _sut.SendCustomerRelationRefusalEmailAsync(context);

        _notificationManagerMock.Verify(x => x.PublishAsync(It.IsAny<EmailOnlySenderEvent>(), _options.ServiceBusTopic), Times.Once);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.From.Should().Be(_options.SenderEmail);
        capturedRequest.To.Should().ContainSingle().Which.Should().Be(_options.CustomerRelationRefusalRecipientEmail);
        capturedRequest.TemplateName.Should().Be(_options.CustomerRelationRefusalTemplateName);
        capturedRequest.Variables.Should().ContainKey("clientFullName");
        capturedRequest.Variables.Should().ContainKey("refusalDate");
        capturedRequest.Variables.Should().ContainKey("folderCodes");
        capturedRequest.Variables.Should().ContainKey("clientEmail");
        capturedRequest.Variables["clientFullName"].Should().Be("John Doe");
        capturedRequest.Variables["refusalDate"].Should().Be(DateTime.UtcNow.ToString("dd/MM/yyyy"));
        capturedRequest.Variables["folderCodes"].Should().Be("ACC1, ACC2");
        capturedRequest.Variables["clientEmail"].Should().Be(context.UserEmail);
        capturedRequest.Bcc.Should().BeNull();
    }
}
