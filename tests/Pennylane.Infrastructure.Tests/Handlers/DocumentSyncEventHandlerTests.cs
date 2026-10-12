using System.Net;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models;
using Pennylane.Application.StaticValues;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pennylane.Infrastructure.Handlers;
using Pulse.Back.Events.IntegrationEvents;
using Pulse.Back.Events.IntegrationEvents.EventsData;
using Pennylane.Domain;
using Pennylane.Application.Managers;
using System.Reflection;
using Pulse.Back.Events.Abstractions;

namespace Pennylane.Infrastructure.Tests.Handlers;

public class DocumentSyncEventHandlerTests

{
    private readonly Mock<ILogger<DocumentSyncEventHandler>> _loggerMock;
    private readonly Mock<IOptions<DocumentSyncOptions>> _syncOptionsMock;
    private readonly Mock<IPennylaneApiGroupService> _groupServiceMock;
    private readonly Mock<IPennylaneApiFirmService> _firmServiceMock;
    private readonly Mock<IDocumentService> _documentServiceMock;
    private readonly Mock<IPennylaneService> _mockPennylaneService;
    private readonly Mock<INotificationManager> _mockNotificationManager;
    private readonly DocumentSyncEventHandler _handler;
    private readonly Fixture _fixture;

    public DocumentSyncEventHandlerTests()
    {
        _fixture = new Fixture();
        _loggerMock = new Mock<ILogger<DocumentSyncEventHandler>>();

        // Setup default DocumentSyncOptions
        var defaultOptions = new DocumentSyncOptions
        {
            SyncPaths = new List<string> { Constant.PermanentPublications }
        };
        _syncOptionsMock = new Mock<IOptions<DocumentSyncOptions>>();
        _syncOptionsMock.Setup(x => x.Value).Returns(defaultOptions);

        _groupServiceMock = new Mock<IPennylaneApiGroupService>();
        _firmServiceMock = new Mock<IPennylaneApiFirmService>();
        _documentServiceMock = new Mock<IDocumentService>();
        _mockPennylaneService = new Mock<IPennylaneService>();
        _mockNotificationManager = new Mock<INotificationManager>();
        _handler = new DocumentSyncEventHandler(
            _loggerMock.Object,
            _syncOptionsMock.Object,
            _groupServiceMock.Object,
            _firmServiceMock.Object,
            _documentServiceMock.Object,
            _mockPennylaneService.Object,
            _mockNotificationManager.Object);
    }

    private PennylaneDmsFile CreateDms(long id, string path = Constant.PermanentPublications)
    => _fixture.Build<PennylaneDmsFile>()
               .With(x => x.Id, id)
               .With(x => x.Path, path)
               .With(x => x.Name, $"dms-{id}")
               .With(x => x.Url, $"https://remote/{id}")
               .Create();

    private Document CreateLocal(long id, string path = Constant.PermanentPublications)
        => _fixture.Build<Document>()
                   .With(d => d.Id, id)
                   .With(d => d.Path, path)
                   .With(d => d.Name, $"local-{id}")
                   .With(d => d.Url, $"https://local/{id}")
                   .With(d => d.CreatedAt, DateTime.UtcNow.AddDays(-1))
                   .With(d => d.UpdatedAt, DateTime.UtcNow)
                   .Create();

    private PennylaneDocumentSyncEvent CreateValidPayload(string accountNumber = "ACC123", int accountId = 42)
    {
        var data = _fixture.Build<PennylaneDocumentSyncEventData>()
                           .With(x => x.AccountNumber, accountNumber)
                           .With(x => x.AccountId, accountId)
                           .Create();
        return new PennylaneDocumentSyncEvent(data, "1.0");
    }

    private Mock<IOptions<DocumentSyncOptions>> CreateDefaultSyncOptionsMock()
    {
        var mock = new Mock<IOptions<DocumentSyncOptions>>();
        mock.Setup(x => x.Value).Returns(new DocumentSyncOptions
        {
            SyncPaths = new List<string> { Constant.PermanentPublications }
        });
        return mock;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_MessageIsNullOrWhitespace_NoServiceCalled(string message)
    {
        await _handler.HandleAsync(message);
        _mockPennylaneService.Verify(x => x.GetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
        _mockPennylaneService.Verify(x => x.GetPennylaneInternalIdByAccountNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _firmServiceMock.Verify(x => x.GetCompanyDmsFilesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _documentServiceMock.Verify(x => x.InsertPennylaneDocumentsAsync(It.IsAny<string>(), It.IsAny<List<PennylaneDmsFile>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InvalidPayload_NoServiceCalled()
    {
        var invalidPayload = JsonSerializer.Serialize(new { foo = "bar" });
        await _handler.HandleAsync(invalidPayload);
        _mockPennylaneService.Verify(x => x.GetCompanyByIdAsync(It.IsAny<string>()), Times.Never);
        _mockPennylaneService.Verify(x => x.GetPennylaneInternalIdByAccountNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _firmServiceMock.Verify(x => x.GetCompanyDmsFilesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _documentServiceMock.Verify(x => x.InsertPennylaneDocumentsAsync(It.IsAny<string>(), It.IsAny<List<PennylaneDmsFile>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CompanyNotFound_NoFurtherServiceCalled()
    {
        var payload = _fixture.Build<BasePennylaneDocumentEventData>()
            .With(x => x.AccountNumber, "ACC123")
            .With(x => x.AccountId, 42)
            .Create();
        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(payload.AccountNumber)).ReturnsAsync((PennylaneCompany)null);
        await _handler.HandleAsync(JsonSerializer.Serialize(payload));
        _mockPennylaneService.Verify(x => x.GetPennylaneInternalIdByAccountNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _firmServiceMock.Verify(x => x.GetCompanyDmsFilesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _documentServiceMock.Verify(x => x.InsertPennylaneDocumentsAsync(It.IsAny<string>(), It.IsAny<List<PennylaneDmsFile>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InternalIdNotFound_NoFurtherServiceCalled()
    {
        var payload = _fixture.Build<BasePennylaneDocumentEventData>()
            .With(x => x.AccountNumber, "ACC123")
            .With(x => x.AccountId, 42)
            .Create();
        var company = new PennylaneCompany { FirmId = "FIRM1" };
        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(payload.AccountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, payload.AccountNumber, It.IsAny<bool>())).ReturnsAsync((int?)null);
        await _handler.HandleAsync(JsonSerializer.Serialize(payload));
        _firmServiceMock.Verify(x => x.GetCompanyDmsFilesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _documentServiceMock.Verify(x => x.InsertPennylaneDocumentsAsync(It.IsAny<string>(), It.IsAny<List<PennylaneDmsFile>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DocumentsInserted_ServiceCalledOnce()
    {
        // Arrange
        var payloadData = _fixture.Build<PennylaneDocumentSyncEventData>()
            .With(x => x.AccountNumber, "ACC123")
            .With(x => x.AccountId, 42)
            .Create();

        var payload = new PennylaneDocumentSyncEvent(payloadData, "1.0");

        var company = new PennylaneCompany { FirmId = "FIRM1" };
        var internalId = 99;
        var dmsFiles = _fixture.CreateMany<PennylaneDmsFile>(3).ToList();
        var filteredFiles = dmsFiles.Where(f => f.Path == Constant.PermanentPublications).ToList();

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(payloadData.AccountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, payloadData.AccountNumber, It.IsAny<bool>())).ReturnsAsync(internalId);
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, string.Empty)).ReturnsAsync(dmsFiles);

        _documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(payloadData.AccountNumber, It.IsAny<List<PennylaneDmsFile>>()))
            .ReturnsAsync(2).Verifiable();

        // Act
        await _handler.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert
        _documentServiceMock.Verify(x =>
                x.InsertPennylaneDocumentsAsync(
                    payloadData.AccountNumber,
                    It.Is<List<PennylaneDmsFile>>(l => l.Count == filteredFiles.Count)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RemoteFilteredListEmpty_ShouldInsertEmptyAndSkipDeletion()
    {
        // Arrange
        var payload = CreateValidPayload();
        var accountNumber = payload.Data.AccountNumber!;
        var company = _fixture.Build<PennylaneCompany>().With(c => c.FirmId, "FIRM1").Create();
        var internalId = 99;

        // Remote files but none under PermanentPublications → filtered list empty
        var dmsFiles = new List<PennylaneDmsFile>
    {
        CreateDms(1, path: "/some/other/path"),
        CreateDms(2, path: "/elsewhere")
    };

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(accountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                             .ReturnsAsync(internalId);
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>()))
                        .ReturnsAsync(dmsFiles);

        _documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(accountNumber, It.IsAny<List<PennylaneDmsFile>>()))
                            .ReturnsAsync(2)
                            .Verifiable();

        // Act
        await _handler.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert: insert got an empty filtered list; deletion skipped
        _documentServiceMock.Verify(x =>
            x.InsertPennylaneDocumentsAsync(
                accountNumber,
                It.Is<List<PennylaneDmsFile>>(l => l.Count == 0)), Times.Once);

        _documentServiceMock.Verify(x =>
            x.HardDeleteDocumentsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<long>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DeltaExists_ShouldHardDeleteOnlyMissingIds()
    {
        // Arrange
        var payload = CreateValidPayload();
        var accountNumber = payload.Data.AccountNumber!;
        var company = _fixture.Build<PennylaneCompany>().With(c => c.FirmId, "FIRM1").Create();
        var internalId = 99;

        // Remote filtered: {1,3}
        var dmsFiles = new List<PennylaneDmsFile>
    {
        CreateDms(1), CreateDms(3),
        CreateDms(999, path: "/ignored") // different path -> ignored by filter
    };

        // Local: {1,2,3,4} ⇒ delta = {2,4}
        var localDocs = new List<Document> { CreateLocal(1), CreateLocal(2), CreateLocal(3), CreateLocal(4) };

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(accountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                             .ReturnsAsync(internalId);
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>()))
                        .ReturnsAsync(dmsFiles);

        _documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(accountNumber, It.IsAny<List<PennylaneDmsFile>>()))
                            .ReturnsAsync(2);

        // Mock GetAllDocumentsByPathAsync with the expected prefix (full path without wildcard)
        _documentServiceMock.Setup(x => x.GetAllDocumentsByPathAsync(accountNumber, Constant.PermanentPublications))
                            .ReturnsAsync(localDocs);

        var deletedIds = new List<long>();
        _documentServiceMock
            .Setup(x => x.HardDeleteDocumentsAsync(accountNumber, It.IsAny<IEnumerable<long>>()))
            .Callback<string, IEnumerable<long>>((_, ids) => deletedIds.AddRange(ids))
            .ReturnsAsync((string _, IEnumerable<long> ids) => ids.Count());

        // Act
        await _handler.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert: exactly {2,4}
        deletedIds.Sort();
        Assert.Equal(new[] { 2L, 4L }, deletedIds.ToArray());
    }

    [Fact]
    public async Task HandleAsync_NoDelta_ShouldNotHardDelete()
    {
        // Arrange
        var payload = CreateValidPayload();
        var accountNumber = payload.Data.AccountNumber!;
        var company = _fixture.Build<PennylaneCompany>().With(c => c.FirmId, "FIRM1").Create();
        var internalId = 99;

        // Remote filtered: {10,11}
        var dmsFiles = new List<PennylaneDmsFile> { CreateDms(10), CreateDms(11) };

        // Local exactly matches: {10,11}
        var localDocs = new List<Document> { CreateLocal(10), CreateLocal(11) };

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(accountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                             .ReturnsAsync(internalId);
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>()))
                        .ReturnsAsync(dmsFiles);

        _documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(accountNumber, It.IsAny<List<PennylaneDmsFile>>()))
                            .ReturnsAsync(2);

        // Mock GetAllDocumentsByPathAsync with the expected prefix
        _documentServiceMock.Setup(x => x.GetAllDocumentsByPathAsync(accountNumber, Constant.PermanentPublications))
                            .ReturnsAsync(localDocs);

        // Act
        await _handler.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert
        _documentServiceMock.Verify(x =>
            x.HardDeleteDocumentsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<long>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_FetchDmsFails_ShouldNotInsertOrDelete()
    {
        // Arrange
        var payload = CreateValidPayload();
        var accountNumber = payload.Data.AccountNumber!;
        var company = _fixture.Build<PennylaneCompany>().With(c => c.FirmId, "FIRM1").Create();
        var internalId = 99;

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(accountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                             .ReturnsAsync(internalId);

        _firmServiceMock
            .Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));

        // Act + Assert (exception propagates)
        await Assert.ThrowsAsync<Exception>(() =>
            _handler.HandleAsync(JsonSerializer.Serialize(payload)));

        // Assert: no insert, no delete after the thrown exception
        _documentServiceMock.Verify(x =>
            x.InsertPennylaneDocumentsAsync(It.IsAny<string>(), It.IsAny<List<PennylaneDmsFile>>()), Times.Never);

        _documentServiceMock.Verify(x =>
            x.HardDeleteDocumentsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<long>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_LargeDelta_ShouldCallHardDeleteMultipleTimesAndDeleteAllDeltaIds()
    {
        // Arrange
        var payload = CreateValidPayload();
        var accountNumber = payload.Data.AccountNumber!;
        var company = _fixture.Build<PennylaneCompany>().With(c => c.FirmId, "FIRM1").Create();
        var internalId = 99;

        // Remote filtered: odd ids 1..999
        var remoteIds = Enumerable.Range(1, 999).Where(i => i % 2 == 1).ToArray();
        var dmsFiles = remoteIds
            .Select(id => CreateDms(id))
            .ToList();

        // Local: 1..1500 → expected delta = local - remote
        var localDocs = Enumerable.Range(1, 1500)
            .Select(id => CreateLocal(id))
            .ToList();

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(accountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                             .ReturnsAsync(internalId);
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>()))
                        .ReturnsAsync(dmsFiles);

        _documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(accountNumber, It.IsAny<List<PennylaneDmsFile>>()))
                            .ReturnsAsync(2);

        // Mock GetAllDocumentsByPathAsync with the expected prefix
        _documentServiceMock.Setup(x => x.GetAllDocumentsByPathAsync(accountNumber, Constant.PermanentPublications))
                            .ReturnsAsync(localDocs);

        var batches = new List<long[]>();
        _documentServiceMock
            .Setup(x => x.HardDeleteDocumentsAsync(accountNumber, It.IsAny<IEnumerable<long>>()))
            .Callback<string, IEnumerable<long>>((_, ids) => batches.Add(ids.ToArray()))
            .ReturnsAsync((string _, IEnumerable<long> ids) => ids.Count());

        // Act
        await _handler.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert: union of all deleted equals expected delta (local - remote)
        var allDeleted = batches.SelectMany(b => b).Distinct().OrderBy(i => i).ToArray();
        var expected = Enumerable.Range(1, 1500).Select(i => (long)i).Except(remoteIds.Select(i => (long)i)).OrderBy(i => i).ToArray();

        Assert.Equal(expected, allDeleted);

        // Expect multiple calls (given chunking in handler)
        _documentServiceMock.Verify(x =>
            x.HardDeleteDocumentsAsync(accountNumber, It.IsAny<IEnumerable<long>>()), Times.AtLeast(2));
    }

    [Fact]
    public void FilterPublicationFiles_ReturnsOnlyPublicationFilesAndSubfolders()
    {
        // Arrange
        var files = new List<PennylaneDmsFile>
        {
            CreateDms(1, Constant.PermanentPublications), // exact
            CreateDms(2, Constant.PermanentPublications + "/subfolder"), // sous-dossier
            CreateDms(3, Constant.PermanentPublications + "XYZ"), // commence par
            CreateDms(4, "/Dossier permanent/00 - Publication"), // autre dossier
            CreateDms(5, "/dossier permanent/00 - Publications"), // casse différente
            CreateDms(6, "/Dossier permanent/00 -"), // autre dossier
            CreateDms(7, ""), // vide
        };

        var allowedPaths = new List<string> { Constant.PermanentPublications };

        // Act
        var result = typeof(DocumentSyncEventHandler)
            .GetMethod("FilterPublicationFiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { files, allowedPaths }) as List<PennylaneDmsFile>;

        // Assert
        var expectedIds = new List<long> { 1, 2, 3, 5 };
        Assert.Equal(expectedIds, result.Select(f => f.Id));
    }

    [Fact]
    public async Task PublishSyncCompletedEventAsync_WithValidData_PublishesEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DocumentSyncEventHandler>>();
        var syncOptionsMock = new Mock<IOptions<DocumentSyncOptions>>();
        syncOptionsMock.Setup(x => x.Value).Returns(new DocumentSyncOptions
        {
            SyncPaths = new List<string> { Constant.PermanentPublications }
        });
        var groupServiceMock = new Mock<IPennylaneApiGroupService>();
        var firmServiceMock = new Mock<IPennylaneApiFirmService>();
        var documentServiceMock = new Mock<IDocumentService>();
        var pennylaneServiceMock = new Mock<IPennylaneService>();
        var notificationManagerMock = new Mock<INotificationManager>();

        // Utilisation des mêmes mocks que dans votre code existant
        var handler = new DocumentSyncEventHandler(
            loggerMock.Object,
            syncOptionsMock.Object,
            groupServiceMock.Object,
            firmServiceMock.Object,
            documentServiceMock.Object,
            pennylaneServiceMock.Object,
            notificationManagerMock.Object);

        // Paramètres pour l'appel à PublishSyncCompletedEventAsync
        var accountNumber = "ACC123";
        var accountId = 42;
        var documentsAdded = 5;
        var documentsRemoved = 2;

        // Utiliser la réflexion pour appeler la méthode privée
        var methodInfo = typeof(DocumentSyncEventHandler).GetMethod(
            "PublishSyncCompletedEventAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        await (Task)methodInfo.Invoke(handler, new object[] { accountNumber, accountId, documentsAdded, documentsRemoved });

        // Assert
        notificationManagerMock.Verify(
            m => m.PublishAsync(
                It.Is<PennylaneDocumentSyncCompletedEvent>(e =>
                    e.Data.AccountNumber == accountNumber &&
                    e.Data.AccountId == accountId &&
                    e.Data.DocumentsAdded == documentsAdded &&
                    e.Data.DocumentsRemoved == documentsRemoved &&
                    e.Version == "1.0" &&
                    e.EventType == nameof(PennylaneDocumentSyncCompletedEvent)
                ),
                It.IsAny<string>()
            ),
            Times.Once);

        // Vérifier que le log de debug est appelé
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Événement PennylaneDocumentSyncCompletedEvent publié")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)!),
            Times.Once);
    }

    [Fact]
    public async Task PublishSyncCompletedEventAsync_WithException_LogsError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DocumentSyncEventHandler>>();
        var syncOptionsMock = CreateDefaultSyncOptionsMock();
        var groupServiceMock = new Mock<IPennylaneApiGroupService>();
        var firmServiceMock = new Mock<IPennylaneApiFirmService>();
        var documentServiceMock = new Mock<IDocumentService>();
        var pennylaneServiceMock = new Mock<IPennylaneService>();
        var notificationManagerMock = new Mock<INotificationManager>();

        // Configurer le mock pour lever une exception
        notificationManagerMock
            .Setup(m => m.PublishAsync(It.IsAny<PennylaneDocumentSyncCompletedEvent>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Publication failed"));

        var handler = new DocumentSyncEventHandler(
            loggerMock.Object,
            syncOptionsMock.Object,
            groupServiceMock.Object,
            firmServiceMock.Object,
            documentServiceMock.Object,
            pennylaneServiceMock.Object,
            notificationManagerMock.Object);

        // Utiliser la réflexion pour appeler la méthode privée
        var methodInfo = typeof(DocumentSyncEventHandler).GetMethod(
            "PublishSyncCompletedEventAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        await (Task)methodInfo.Invoke(handler, new object[] { "ACC123", 42, 5, 2 });

        // Assert : Vérifier que le log d'erreur est appelé
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Échec de la publication de l'événement")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)!),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithValidPayload_ExecutesFullFlow()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DocumentSyncEventHandler>>();
        var syncOptionsMock = CreateDefaultSyncOptionsMock();
        var groupServiceMock = new Mock<IPennylaneApiGroupService>();
        var firmServiceMock = new Mock<IPennylaneApiFirmService>();
        var documentServiceMock = new Mock<IDocumentService>();
        var pennylaneServiceMock = new Mock<IPennylaneService>();
        var notificationManagerMock = new Mock<INotificationManager>();

        var handler = new DocumentSyncEventHandler(
            loggerMock.Object,
            syncOptionsMock.Object,
            groupServiceMock.Object,
            firmServiceMock.Object,
            documentServiceMock.Object,
            pennylaneServiceMock.Object,
            notificationManagerMock.Object);

        var payloadData = new PennylaneDocumentSyncEventData
        {
            AccountNumber = "ACC123",
            AccountId = 42
        };
        var payload = new PennylaneDocumentSyncEvent(payloadData, "1.0");
        var company = new PennylaneCompany { FirmId = "FIRM1" };
        var internalId = 99;
        var dmsFiles = new List<PennylaneDmsFile> { new PennylaneDmsFile { Id = 1, Path = Constant.PermanentPublications } };

        pennylaneServiceMock.Setup(x => x.GetCompanyByIdAsync(payloadData.AccountNumber)).ReturnsAsync(company);
        pennylaneServiceMock.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, payloadData.AccountNumber, It.IsAny<bool>())).ReturnsAsync(internalId);
        firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>())).ReturnsAsync(dmsFiles);
        documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(payloadData.AccountNumber, It.IsAny<List<PennylaneDmsFile>>())).ReturnsAsync(1);
        documentServiceMock.Setup(x => x.GetAllDocumentsByPathAsync(payloadData.AccountNumber, Constant.PermanentPublications)).ReturnsAsync(new List<Document>());

        // Capturer les valeurs retournées par les méthodes
        int documentsAdded = 0;
        documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(payloadData.AccountNumber, It.IsAny<List<PennylaneDmsFile>>()))
            .ReturnsAsync(1)
            .Callback<string, List<PennylaneDmsFile>>((_, _) => documentsAdded = 1);

        int documentsRemoved = 0;
        var handlerType = typeof(DocumentSyncEventHandler);
        var startDeletionWorkflowMethod = handlerType.GetMethod("StartDeletionWorkflowAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var originalStartDeletionWorkflow = startDeletionWorkflowMethod;

        // Remplacer temporairement StartDeletionWorkflowAsync pour capturer documentsRemoved
        Func<object, object[], Task<int>> startDeletionWorkflowWrapper = async (instance, args) =>
        {
            var result = await (Task<int>)originalStartDeletionWorkflow.Invoke(instance, args);
            documentsRemoved = result;
            return result;
        };

        // Act
        await handler.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert
        Assert.True(documentsAdded > 0, "documentsAdded devrait être supérieur à 0");
        notificationManagerMock.Verify(
            m => m.PublishAsync(It.IsAny<PennylaneDocumentSyncCompletedEvent>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithZeroDocumentsAdded_DoesNotCallPublishSyncCompletedEventAsync()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DocumentSyncEventHandler>>();
        var syncOptionsMock = CreateDefaultSyncOptionsMock();
        var groupServiceMock = new Mock<IPennylaneApiGroupService>();
        var firmServiceMock = new Mock<IPennylaneApiFirmService>();
        var documentServiceMock = new Mock<IDocumentService>();
        var pennylaneServiceMock = new Mock<IPennylaneService>();
        var notificationManagerMock = new Mock<INotificationManager>();

        var handler = new DocumentSyncEventHandler(
            loggerMock.Object,
            syncOptionsMock.Object,
            groupServiceMock.Object,
            firmServiceMock.Object,
            documentServiceMock.Object,
            pennylaneServiceMock.Object,
            notificationManagerMock.Object);

        var payloadData = new PennylaneDocumentSyncEventData
        {
            AccountNumber = "ACC123",
            AccountId = 42
        };
        var payload = new PennylaneDocumentSyncEvent(payloadData, "1.0");
        var company = new PennylaneCompany { FirmId = "FIRM1" };
        var internalId = 99;
        var dmsFiles = new List<PennylaneDmsFile> { new PennylaneDmsFile { Id = 1, Path = Constant.PermanentPublications } };

        pennylaneServiceMock.Setup(x => x.GetCompanyByIdAsync(payloadData.AccountNumber)).ReturnsAsync(company);
        pennylaneServiceMock.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, payloadData.AccountNumber, It.IsAny<bool>())).ReturnsAsync(internalId);
        firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>())).ReturnsAsync(dmsFiles);
        documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(payloadData.AccountNumber, It.IsAny<List<PennylaneDmsFile>>())).ReturnsAsync(0); // Aucun document ajouté

        // Configurer GetAllDocumentsByPathAsync pour retourner une liste vide
        documentServiceMock.Setup(x => x.GetAllDocumentsByPathAsync(payloadData.AccountNumber, Constant.PermanentPublications))
            .ReturnsAsync(new List<Document>()); // Retourne une liste vide au lieu de null

        // Capturer l'appel à PublishAsync
        notificationManagerMock.Setup(m => m.PublishAsync(It.IsAny<PennylaneDocumentSyncCompletedEvent>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await handler.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert
        notificationManagerMock.Verify(
            m => m.PublishAsync(It.IsAny<PennylaneDocumentSyncCompletedEvent>(), It.IsAny<string>()),
            Times.Never);
    }


    [Fact]
    public async Task HandleAsync_WithValidPayload_PublishesEventWithCorrectData()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DocumentSyncEventHandler>>();
        var syncOptionsMock = CreateDefaultSyncOptionsMock();
        var groupServiceMock = new Mock<IPennylaneApiGroupService>();
        var firmServiceMock = new Mock<IPennylaneApiFirmService>();
        var documentServiceMock = new Mock<IDocumentService>();
        var pennylaneServiceMock = new Mock<IPennylaneService>();
        var notificationManagerMock = new Mock<INotificationManager>();

        var handler = new DocumentSyncEventHandler(
            loggerMock.Object,
            syncOptionsMock.Object,
            groupServiceMock.Object,
            firmServiceMock.Object,
            documentServiceMock.Object,
            pennylaneServiceMock.Object,
            notificationManagerMock.Object);

        var payloadData = new PennylaneDocumentSyncEventData
        {
            AccountNumber = "ACC123",
            AccountId = 42
        };
        var payload = new PennylaneDocumentSyncEvent(payloadData, "1.0");
        var company = new PennylaneCompany { FirmId = "FIRM1" };
        var internalId = 99;
        var dmsFiles = new List<PennylaneDmsFile> { new PennylaneDmsFile { Id = 1, Path = Constant.PermanentPublications } };

        pennylaneServiceMock.Setup(x => x.GetCompanyByIdAsync(payloadData.AccountNumber)).ReturnsAsync(company);
        pennylaneServiceMock.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, payloadData.AccountNumber, It.IsAny<bool>())).ReturnsAsync(internalId);
        firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>())).ReturnsAsync(dmsFiles);
        documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(payloadData.AccountNumber, It.IsAny<List<PennylaneDmsFile>>())).ReturnsAsync(1); // 1 document ajouté
        documentServiceMock.Setup(x => x.GetAllDocumentsByPathAsync(payloadData.AccountNumber, Constant.PermanentPublications)).ReturnsAsync(new List<Document>());
        documentServiceMock.Setup(x => x.HardDeleteDocumentsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<long>>())).ReturnsAsync(0);

        // Capturer l'événement publié
        BaseEvent<PennylaneDocumentSyncCompletedEventData> capturedBaseEvent = null;
        notificationManagerMock
            .Setup(m => m.PublishAsync(It.IsAny<BaseEvent<PennylaneDocumentSyncCompletedEventData>>(), It.IsAny<string>()))
            .Callback<BaseEvent<PennylaneDocumentSyncCompletedEventData>, string>((e, s) => capturedBaseEvent = e)
            .Returns(Task.CompletedTask);

        // Act
        await handler.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert
        Assert.NotNull(capturedBaseEvent);
        Assert.IsType<PennylaneDocumentSyncCompletedEvent>(capturedBaseEvent);

        var capturedEvent = (PennylaneDocumentSyncCompletedEvent)capturedBaseEvent;
        Assert.Equal(payloadData.AccountNumber, capturedEvent.Data.AccountNumber);
        Assert.Equal(payloadData.AccountId, capturedEvent.Data.AccountId);
        Assert.Equal(1, capturedEvent.Data.DocumentsAdded);
        Assert.Equal(0, capturedEvent.Data.DocumentsRemoved);
        Assert.Equal("1.0", capturedEvent.Version);
    }

    [Fact]
    public async Task HandleAsync_WithException_DoesNotPublishEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<DocumentSyncEventHandler>>();
        var syncOptionsMock = CreateDefaultSyncOptionsMock();
        var groupServiceMock = new Mock<IPennylaneApiGroupService>();
        var firmServiceMock = new Mock<IPennylaneApiFirmService>();
        var documentServiceMock = new Mock<IDocumentService>();
        var pennylaneServiceMock = new Mock<IPennylaneService>();
        var notificationManagerMock = new Mock<INotificationManager>();

        var handler = new DocumentSyncEventHandler(
            loggerMock.Object,
            syncOptionsMock.Object,
            groupServiceMock.Object,
            firmServiceMock.Object,
            documentServiceMock.Object,
            pennylaneServiceMock.Object,
            notificationManagerMock.Object);

        var payloadData = new PennylaneDocumentSyncEventData
        {
            AccountNumber = "ACC123",
            AccountId = 42
        };
        var payload = new PennylaneDocumentSyncEvent(payloadData, "1.0");
        var company = new PennylaneCompany { FirmId = "FIRM1" };
        var internalId = 99;

        pennylaneServiceMock.Setup(x => x.GetCompanyByIdAsync(payloadData.AccountNumber)).ReturnsAsync(company);
        pennylaneServiceMock.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, payloadData.AccountNumber, It.IsAny<bool>())).ReturnsAsync(internalId);
        firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>())).ThrowsAsync(new Exception("Failed to fetch DMS files"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => handler.HandleAsync(JsonSerializer.Serialize(payload)));

        // Vérifier que l'événement n'a pas été publié
        notificationManagerMock.Verify(
            m => m.PublishAsync(It.IsAny<PennylaneDocumentSyncCompletedEvent>(), It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData("/Dossier annuel/Exercice 2023/FEC/file.pdf", "/Dossier annuel/Exercice */FEC", true)]
    [InlineData("/Dossier annuel/Exercice 2024/FEC/subfolder/file.pdf", "/Dossier annuel/Exercice */FEC", true)]
    [InlineData("/Dossier annuel/Exercice 2023/Fiscal/TVA/file.pdf", "/Dossier annuel/Exercice */Fiscal", true)]
    [InlineData("/Dossier annuel/Exercice 2020/FEC/file.pdf", "/Dossier annuel/Exercice */FEC", true)]
    [InlineData("/Dossier annuel/Exercice 2050/Fiscal/IS/file.pdf", "/Dossier annuel/Exercice */Fiscal", true)]
    [InlineData("/Dossier permanent/00 - Publications/file.pdf", "/Dossier permanent/00 - Publications", true)]
    [InlineData("/Dossier permanent/00 - Publications/subfolder/file.pdf", "/Dossier permanent/00 - Publications", true)]
    [InlineData("/Dossier annuel/Exercice 2023/Autre/file.pdf", "/Dossier annuel/Exercice */FEC", false)]
    [InlineData("/Dossier annuel/Exercice 2023/Autre/file.pdf", "/Dossier annuel/Exercice */Fiscal", false)]
    [InlineData("/Autre dossier/file.pdf", "/Dossier annuel/Exercice */FEC", false)]
    [InlineData("/Dossier annuel/file.pdf", "/Dossier annuel/Exercice */FEC", false)]
    public void FilterPublicationFiles_WithWildcardPattern_MatchesCorrectly(string filePath, string pattern, bool shouldMatch)
    {
        // Arrange
        var files = new List<PennylaneDmsFile>
        {
            new PennylaneDmsFile { Id = 1, Path = filePath, Name = "test.pdf" }
        };
        var allowedPaths = new List<string> { pattern };

        // Act - Use reflection to call the private FilterPublicationFiles method
        var handlerType = typeof(DocumentSyncEventHandler);
        var method = handlerType.GetMethod("FilterPublicationFiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (List<PennylaneDmsFile>)method.Invoke(null, new object[] { files, allowedPaths });

        // Assert
        if (shouldMatch)
        {
            Assert.Single(result);
            Assert.Equal(filePath, result[0].Path);
        }
        else
        {
            Assert.Empty(result);
        }
    }

    [Fact]
    public void ExtractPrefixesFromPatterns_WithNoWildcard_ReturnsFullPath()
    {
        // Arrange
        var patterns = new List<string>
        {
            "/Dossier permanent/00 - Publications"
        };

        // Act
        var handlerType = typeof(DocumentSyncEventHandler);
        var method = handlerType.GetMethod("ExtractPrefixesFromPatterns",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (List<string>)method.Invoke(null, new object[] { patterns });

        // Assert
        Assert.Single(result);
        Assert.Contains("/Dossier permanent/00 - Publications", result);
    }

    [Fact]
    public void ExtractPrefixesFromPatterns_WithWildcard_ReturnsPrefix()
    {
        // Arrange
        var patterns = new List<string>
        {
            "/Dossier annuel/Exercice */FEC"
        };

        // Act
        var handlerType = typeof(DocumentSyncEventHandler);
        var method = handlerType.GetMethod("ExtractPrefixesFromPatterns",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (List<string>)method.Invoke(null, new object[] { patterns });

        // Assert
        Assert.Single(result);
        Assert.Contains("/Dossier annuel/Exercice", result);
    }

    [Fact]
    public void ExtractPrefixesFromPatterns_WithMultiplePatternsSharePrefix_ReturnsDistinctPrefixes()
    {
        // Arrange
        var patterns = new List<string>
        {
            "/Dossier annuel/Exercice */FEC",
            "/Dossier annuel/Exercice */Fiscal"
        };

        // Act
        var handlerType = typeof(DocumentSyncEventHandler);
        var method = handlerType.GetMethod("ExtractPrefixesFromPatterns",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (List<string>)method.Invoke(null, new object[] { patterns });

        // Assert - Both patterns share the same prefix, so should only return one
        Assert.Single(result);
        Assert.Contains("/Dossier annuel/Exercice", result);
    }

    [Fact]
    public void ExtractPrefixesFromPatterns_WithMixedPatterns_ReturnsCorrectPrefixes()
    {
        // Arrange
        var patterns = new List<string>
        {
            "/Dossier permanent/00 - Publications",
            "/Dossier annuel/Exercice */FEC",
            "/Dossier annuel/Exercice */Fiscal"
        };

        // Act
        var handlerType = typeof(DocumentSyncEventHandler);
        var method = handlerType.GetMethod("ExtractPrefixesFromPatterns",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (List<string>)method.Invoke(null, new object[] { patterns });

        // Assert - Should return 2 prefixes: one full path, one prefix before wildcard
        Assert.Equal(2, result.Count);
        Assert.Contains("/Dossier permanent/00 - Publications", result);
        Assert.Contains("/Dossier annuel/Exercice", result);
    }

    [Fact]
    public void ExtractPrefixesFromPatterns_WithEmptyList_ReturnsFallback()
    {
        // Arrange
        var patterns = new List<string>();

        // Act
        var handlerType = typeof(DocumentSyncEventHandler);
        var method = handlerType.GetMethod("ExtractPrefixesFromPatterns",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (List<string>)method.Invoke(null, new object[] { patterns });

        // Assert - Should return fallback (empty string to load all documents)
        Assert.Single(result);
        Assert.Contains(string.Empty, result);
    }

    [Fact]
    public void ExtractPrefixesFromPatterns_WithWildcardAndTrailingSpace_TrimsSpace()
    {
        // Arrange
        var patterns = new List<string>
        {
            "/Dossier annuel/Exercice */FEC"  // Has a space before the wildcard
        };

        // Act
        var handlerType = typeof(DocumentSyncEventHandler);
        var method = handlerType.GetMethod("ExtractPrefixesFromPatterns",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (List<string>)method.Invoke(null, new object[] { patterns });

        // Assert - Should trim the space after "Exercice"
        Assert.Single(result);
        Assert.Contains("/Dossier annuel/Exercice", result);
        Assert.DoesNotContain("/Dossier annuel/Exercice ", result);  // No trailing space
    }

    [Fact]
    public void ExtractPrefixesFromPatterns_WithWildcardAtStart_ReturnsEmptyFallback()
    {
        // Arrange
        var patterns = new List<string>
        {
            "*/FEC"  // Wildcard at the start
        };

        // Act
        var handlerType = typeof(DocumentSyncEventHandler);
        var method = handlerType.GetMethod("ExtractPrefixesFromPatterns",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (List<string>)method.Invoke(null, new object[] { patterns });

        // Assert - Should return fallback (empty string) since prefix is empty
        Assert.Single(result);
        Assert.Contains(string.Empty, result);
    }

    [Fact]
    public void FilterPublicationFiles_WithMultipleWildcardPatterns_FiltersCorrectly()
    {
        // Arrange
        var files = new List<PennylaneDmsFile>
        {
            new PennylaneDmsFile { Id = 1, Path = "/Dossier permanent/00 - Publications/file1.pdf", Name = "file1.pdf" },
            new PennylaneDmsFile { Id = 2, Path = "/Dossier annuel/Exercice 2023/FEC/file2.pdf", Name = "file2.pdf" },
            new PennylaneDmsFile { Id = 3, Path = "/Dossier annuel/Exercice 2024/Fiscal/TVA/file3.pdf", Name = "file3.pdf" },
            new PennylaneDmsFile { Id = 4, Path = "/Dossier annuel/Exercice 2023/Autre/file4.pdf", Name = "file4.pdf" },
            new PennylaneDmsFile { Id = 5, Path = "/Autre dossier/file5.pdf", Name = "file5.pdf" }
        };
        var allowedPaths = new List<string>
        {
            "/Dossier permanent/00 - Publications",
            "/Dossier annuel/Exercice */FEC",
            "/Dossier annuel/Exercice */Fiscal"
        };

        // Act
        var handlerType = typeof(DocumentSyncEventHandler);
        var method = handlerType.GetMethod("FilterPublicationFiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (List<PennylaneDmsFile>)method.Invoke(null, new object[] { files, allowedPaths });

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, f => f.Id == 1); // Publications
        Assert.Contains(result, f => f.Id == 2); // FEC 2023
        Assert.Contains(result, f => f.Id == 3); // Fiscal 2024
        Assert.DoesNotContain(result, f => f.Id == 4); // Autre (not included)
        Assert.DoesNotContain(result, f => f.Id == 5); // Other folder (not included)
    }

    [Fact]
    public async Task HandleAsync_WithWildcardPaths_LoadsLocalDocumentsCorrectlyAndDeletesDelta()
    {
        // Arrange - This test validates the bug fix for wildcard path matching in LoadLocalDocumentsAsync
        var accountNumber = "ACC123";
        var accountId = 42;
        var payload = CreateValidPayload(accountNumber, accountId);
        var company = _fixture.Build<PennylaneCompany>().With(c => c.FirmId, "FIRM1").Create();
        var internalId = 99;

        // Configure sync options with wildcard paths
        var syncOptionsWithWildcard = new DocumentSyncOptions
        {
            SyncPaths = new List<string>
            {
                "/Dossier permanent/00 - Publications",
                "/Dossier annuel/Exercice */FEC"
            }
        };
        var optionsMock = new Mock<IOptions<DocumentSyncOptions>>();
        optionsMock.Setup(x => x.Value).Returns(syncOptionsWithWildcard);

        var handlerWithWildcard = new DocumentSyncEventHandler(
            _loggerMock.Object,
            optionsMock.Object,
            _groupServiceMock.Object,
            _firmServiceMock.Object,
            _documentServiceMock.Object,
            _mockPennylaneService.Object,
            _mockNotificationManager.Object);

        // Remote files from Pennylane: IDs 1, 3
        var dmsFiles = new List<PennylaneDmsFile>
        {
            CreateDms(1, path: "/Dossier permanent/00 - Publications/file1.pdf"),
            CreateDms(3, path: "/Dossier annuel/Exercice 2023/FEC/file3.pdf")
        };

        // Local documents in DB: IDs 1, 2, 3, 4
        // ID 2 and 4 should be deleted (exist locally but not in remote)
        var localDocsFromDb = new List<Document>
        {
            CreateLocal(1, path: "/Dossier permanent/00 - Publications/file1.pdf"),
            CreateLocal(2, path: "/Dossier annuel/Exercice 2023/FEC/file2.pdf"), // To be deleted
            CreateLocal(3, path: "/Dossier annuel/Exercice 2023/FEC/file3.pdf"),
            CreateLocal(4, path: "/Dossier annuel/Exercice 2024/FEC/file4.pdf")  // To be deleted
        };

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(accountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                             .ReturnsAsync(internalId);
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>()))
                        .ReturnsAsync(dmsFiles);

        _documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(accountNumber, It.IsAny<List<PennylaneDmsFile>>()))
                            .ReturnsAsync(0); // No new documents to insert

        // Mock GetAllDocumentsByPathAsync for each prefix extracted from patterns
        // Pattern "/Dossier permanent/00 - Publications" -> prefix "/Dossier permanent/00 - Publications"
        _documentServiceMock.Setup(x => x.GetAllDocumentsByPathAsync(accountNumber, "/Dossier permanent/00 - Publications"))
                            .ReturnsAsync(localDocsFromDb.Where(d => d.Path.StartsWith("/Dossier permanent/00 - Publications", StringComparison.OrdinalIgnoreCase)).ToList());

        // Pattern "/Dossier annuel/Exercice */FEC" -> prefix "/Dossier annuel/Exercice"
        _documentServiceMock.Setup(x => x.GetAllDocumentsByPathAsync(accountNumber, "/Dossier annuel/Exercice"))
                            .ReturnsAsync(localDocsFromDb.Where(d => d.Path.StartsWith("/Dossier annuel/Exercice", StringComparison.OrdinalIgnoreCase)).ToList());

        var deletedIds = new List<long>();
        _documentServiceMock
            .Setup(x => x.HardDeleteDocumentsAsync(accountNumber, It.IsAny<IEnumerable<long>>()))
            .Callback<string, IEnumerable<long>>((_, ids) => deletedIds.AddRange(ids))
            .ReturnsAsync((string _, IEnumerable<long> ids) => ids.Count());

        // Act
        await handlerWithWildcard.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert - Documents 2 and 4 should be deleted (in local DB but not in remote API)
        deletedIds.Sort();
        Assert.Equal(new[] { 2L, 4L }, deletedIds.ToArray());

        // Verify that GetAllDocumentsByPathAsync was called with the expected prefixes
        _documentServiceMock.Verify(x => x.GetAllDocumentsByPathAsync(accountNumber, "/Dossier permanent/00 - Publications"), Times.Once);
        _documentServiceMock.Verify(x => x.GetAllDocumentsByPathAsync(accountNumber, "/Dossier annuel/Exercice"), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithWildcardPaths_NoLocalDocuments_NoDeletion()
    {
        // Arrange
        var accountNumber = "ACC123";
        var accountId = 42;
        var payload = CreateValidPayload(accountNumber, accountId);
        var company = _fixture.Build<PennylaneCompany>().With(c => c.FirmId, "FIRM1").Create();
        var internalId = 99;

        // Configure sync options with wildcard paths
        var syncOptionsWithWildcard = new DocumentSyncOptions
        {
            SyncPaths = new List<string> { "/Dossier annuel/Exercice */FEC" }
        };
        var optionsMock = new Mock<IOptions<DocumentSyncOptions>>();
        optionsMock.Setup(x => x.Value).Returns(syncOptionsWithWildcard);

        var handlerWithWildcard = new DocumentSyncEventHandler(
            _loggerMock.Object,
            optionsMock.Object,
            _groupServiceMock.Object,
            _firmServiceMock.Object,
            _documentServiceMock.Object,
            _mockPennylaneService.Object,
            _mockNotificationManager.Object);

        // Remote files from Pennylane
        var dmsFiles = new List<PennylaneDmsFile>
        {
            CreateDms(1, path: "/Dossier annuel/Exercice 2023/FEC/file1.pdf")
        };

        // No local documents
        var emptyLocalDocs = new List<Document>();

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(accountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                             .ReturnsAsync(internalId);
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>()))
                        .ReturnsAsync(dmsFiles);

        _documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(accountNumber, It.IsAny<List<PennylaneDmsFile>>()))
                            .ReturnsAsync(1);

        // Mock GetAllDocumentsByPathAsync with the expected prefix for wildcard pattern
        _documentServiceMock.Setup(x => x.GetAllDocumentsByPathAsync(accountNumber, "/Dossier annuel/Exercice"))
                            .ReturnsAsync(emptyLocalDocs);

        // Act
        await handlerWithWildcard.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert - No deletion should occur
        _documentServiceMock.Verify(x => x.HardDeleteDocumentsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<long>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenGetDmsFilesReturns404_ShouldRefreshCompanyAndInternalIdAndRetrySuccessfully()
    {
        // Arrange
        var payload = CreateValidPayload();
        var accountNumber = payload.Data.AccountNumber!;
        var company = _fixture.Build<PennylaneCompany>().With(c => c.FirmId, "OLD-FIRM").Create();
        var internalId = 99;

        var refreshedCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.FirmId, "NEW-FIRM")
            .Create();
        var newInternalId = 200;

        var dmsFiles = new List<PennylaneDmsFile>
        {
            CreateDms(1), CreateDms(2)
        };

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(accountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                             .ReturnsAsync(internalId);

        // First call throws 404
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>()))
                        .ThrowsAsync(new RequestHttpException(HttpStatusCode.NotFound, "not found"));

        // Refresh company
        _mockPennylaneService.Setup(x => x.RefreshCompanyFromApiAsync(accountNumber))
                             .ReturnsAsync(refreshedCompany);

        // Refresh internalId with new firmId
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync("NEW-FIRM", accountNumber, true))
                             .ReturnsAsync(newInternalId);

        // Retry succeeds
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync("NEW-FIRM", newInternalId, It.IsAny<string>()))
                        .ReturnsAsync(dmsFiles);

        _documentServiceMock.Setup(x => x.InsertPennylaneDocumentsAsync(accountNumber, It.IsAny<List<PennylaneDmsFile>>()))
                            .ReturnsAsync(2);
        _documentServiceMock.Setup(x => x.GetAllDocumentsByPathAsync(accountNumber, Constant.PermanentPublications))
                            .ReturnsAsync(new List<Document>());

        // Act
        await _handler.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert
        _mockPennylaneService.Verify(x => x.RefreshCompanyFromApiAsync(accountNumber), Times.Once);
        _mockPennylaneService.Verify(x => x.GetPennylaneInternalIdByAccountNumberAsync("NEW-FIRM", accountNumber, true), Times.Once);
        _firmServiceMock.Verify(x => x.GetCompanyDmsFilesAsync("NEW-FIRM", newInternalId, It.IsAny<string>()), Times.Once);
        _documentServiceMock.Verify(x => x.InsertPennylaneDocumentsAsync(accountNumber, It.IsAny<List<PennylaneDmsFile>>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenGetDmsFilesReturns404_AndRetryAlsoFails_ShouldPropagateException()
    {
        // Arrange
        var payload = CreateValidPayload();
        var accountNumber = payload.Data.AccountNumber!;
        var company = _fixture.Build<PennylaneCompany>().With(c => c.FirmId, "OLD-FIRM").Create();
        var internalId = 99;

        var refreshedCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.FirmId, "NEW-FIRM")
            .Create();
        var newInternalId = 200;

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(accountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                             .ReturnsAsync(internalId);

        // First call throws 404
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>()))
                        .ThrowsAsync(new RequestHttpException(HttpStatusCode.NotFound, "not found"));

        // Refresh company
        _mockPennylaneService.Setup(x => x.RefreshCompanyFromApiAsync(accountNumber))
                             .ReturnsAsync(refreshedCompany);

        // Refresh internalId
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync("NEW-FIRM", accountNumber, true))
                             .ReturnsAsync(newInternalId);

        // Retry also fails
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync("NEW-FIRM", newInternalId, It.IsAny<string>()))
                        .ThrowsAsync(new RequestHttpException(HttpStatusCode.NotFound, "still not found"));

        // Act & Assert
        await Assert.ThrowsAsync<RequestHttpException>(() =>
            _handler.HandleAsync(JsonSerializer.Serialize(payload)));

        _documentServiceMock.Verify(x =>
            x.InsertPennylaneDocumentsAsync(It.IsAny<string>(), It.IsAny<List<PennylaneDmsFile>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenGetDmsFilesReturns500_ShouldNotRetryAndPropagateException()
    {
        // Arrange
        var payload = CreateValidPayload();
        var accountNumber = payload.Data.AccountNumber!;
        var company = _fixture.Build<PennylaneCompany>().With(c => c.FirmId, "FIRM1").Create();
        var internalId = 99;

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(accountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                             .ReturnsAsync(internalId);

        // 500 error - should NOT be caught
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>()))
                        .ThrowsAsync(new RequestHttpException(HttpStatusCode.InternalServerError, "server error"));

        // Act & Assert
        await Assert.ThrowsAsync<RequestHttpException>(() =>
            _handler.HandleAsync(JsonSerializer.Serialize(payload)));

        // RefreshCompanyFromApiAsync should NOT be called for non-404 errors
        _mockPennylaneService.Verify(x => x.RefreshCompanyFromApiAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenGetDmsFilesReturns404_AndRefreshReturnsNullInternalId_ShouldReturnEarly()
    {
        // Arrange
        var payload = CreateValidPayload();
        var accountNumber = payload.Data.AccountNumber!;
        var company = _fixture.Build<PennylaneCompany>().With(c => c.FirmId, "OLD-FIRM").Create();
        var internalId = 99;

        var refreshedCompany = _fixture.Build<PennylaneCompany>()
            .With(c => c.FirmId, "NEW-FIRM")
            .Create();

        _mockPennylaneService.Setup(x => x.GetCompanyByIdAsync(accountNumber)).ReturnsAsync(company);
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                             .ReturnsAsync(internalId);

        // First call throws 404
        _firmServiceMock.Setup(x => x.GetCompanyDmsFilesAsync(company.FirmId, internalId, It.IsAny<string>()))
                        .ThrowsAsync(new RequestHttpException(HttpStatusCode.NotFound, "not found"));

        // Refresh company succeeds
        _mockPennylaneService.Setup(x => x.RefreshCompanyFromApiAsync(accountNumber))
                             .ReturnsAsync(refreshedCompany);

        // Refresh internalId returns null
        _mockPennylaneService.Setup(x => x.GetPennylaneInternalIdByAccountNumberAsync("NEW-FIRM", accountNumber, true))
                             .ReturnsAsync((int?)null);

        // Act
        await _handler.HandleAsync(JsonSerializer.Serialize(payload));

        // Assert - should return early without retrying or inserting
        _firmServiceMock.Verify(x => x.GetCompanyDmsFilesAsync("NEW-FIRM", It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _documentServiceMock.Verify(x =>
            x.InsertPennylaneDocumentsAsync(It.IsAny<string>(), It.IsAny<List<PennylaneDmsFile>>()), Times.Never);
    }

}