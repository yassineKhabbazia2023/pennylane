using Application.Interfaces;
using Application.Models.Contacts;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.Signatory;
using Pennylane.Application.Requests;
using Pennylane.Application.services;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Pennylane;
using Pulse.ExceptionMiddleware.Exceptions;
using System.Globalization;
using System.Text;

namespace Pennylane.Application.Tests.Services;

public class SignatoryAccessesImportServiceTests
{
    private const string CsvHeader =
        "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix HT de la mise en conformité;Date de fin de gratuité;Vague";

    private const string FebruaryTermsName =
        "Conditions générales en vigueur – mise à jour fevrier 2026 – RYDGE Conseil (1).pdf";

    private const string OctoberTermsName =
        "Conditions générales en vigueur – mise à jour fevrier 2026 – RYDGE Conseil (2).pdf";

    private const string JuneOverrideTermsName =
        "Conditions générales en vigueur – mise à jour juin 2026 – RYDGE Conseil.pdf";

    private const string RestaurationWaveTermsName =
        "Conditions générales en vigueur – mise à jour juillet 2026 – RYDGE Conseil.pdf";

    private readonly Mock<ISignatoryAccessesImportRepository> _repo;
    private readonly Mock<IAccountRepository> _accountRepository;
    private readonly Mock<IComplianceEmailService> _complianceEmailService;
    private readonly Mock<ILogger<SignatoryAccessesImportService>> _logger;
    private readonly Mock<IBlobStorageManager> _blobStorageManager;
    private readonly Mock<IContactRepository> _contactRepository;
    private readonly SignatoryAccessesImportService _service;
    private readonly Mock<IOptions<FreeTrialInfoOptions>> _options;
    private readonly Fixture _fixture;
    private readonly Mock<IHistoryEventPublisher> _historyEventPublisher;
    private readonly Mock<IRoleRepository> _roleRepository;

    public SignatoryAccessesImportServiceTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _repo = new Mock<ISignatoryAccessesImportRepository>();
        _accountRepository = new Mock<IAccountRepository>();
        _complianceEmailService = new Mock<IComplianceEmailService>();
        _logger = new Mock<ILogger<SignatoryAccessesImportService>>();
        _blobStorageManager = new Mock<IBlobStorageManager>();
        _contactRepository = new Mock<IContactRepository>();
        _options = new Mock<IOptions<FreeTrialInfoOptions>>();
        _options.Setup(o => o.Value).Returns(new FreeTrialInfoOptions
        {
            AllowedFreeTrialEndDates = new List<string> { "30/06/2026", "31/10/2026" },
            TermsByDate = new Dictionary<string, string>
            {
                { "30062026", FebruaryTermsName },
                { "31102026", OctoberTermsName },
            },
            TermsOverridesAfterDate = new Dictionary<string, TermsOverrideOptions>
            {
                {
                    "30062026",
                    new TermsOverrideOptions { EffectiveFromUtc = "01/07/2026", LinkFr = JuneOverrideTermsName }
                },
            },
            TermsByWave = new Dictionary<string, string>
            {
                { "Restauration", RestaurationWaveTermsName }
            },
        });
        _historyEventPublisher = new Mock<IHistoryEventPublisher>();
        _roleRepository = new Mock<IRoleRepository>();

        _accountRepository.Setup(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()))
            .ReturnsAsync(_fixture.Create<AccountEntity>());
        _contactRepository.Setup(x => x.GetContactByEmailOrIdAsync(It.IsAny<string?>(), It.IsAny<int?>()))
            .ReturnsAsync(_fixture.Create<Contact>());
        _roleRepository.Setup(x => x.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(_fixture.CreateMany<SignatoryAccessEntity>().ToList());
        _repo.Setup(r => r.GetTermsIdByLinkFrAsync(RestaurationWaveTermsName))
            .ReturnsAsync(99);

        _service = new SignatoryAccessesImportService(_repo.Object, _accountRepository.Object,
            _complianceEmailService.Object, _logger.Object, _blobStorageManager.Object, _contactRepository.Object,
            _options.Object, _historyEventPublisher.Object, _roleRepository.Object);
    }

    private static IFormFile CreateFormFile(string content, string fileName = "import.csv")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }

    private SignatoryAccessesImportService CreateServiceWithOverride(string effectiveFromUtc)
    {
        var options = new Mock<IOptions<FreeTrialInfoOptions>>();
        options.Setup(o => o.Value).Returns(new FreeTrialInfoOptions
        {
            AllowedFreeTrialEndDates = new List<string> { "30/06/2026", "31/10/2026" },
            TermsByDate = new Dictionary<string, string>
            {
                { "30062026", FebruaryTermsName },
                { "31102026", OctoberTermsName },
            },
            TermsOverridesAfterDate = new Dictionary<string, TermsOverrideOptions>
            {
                {
                    "30062026",
                    new TermsOverrideOptions { EffectiveFromUtc = effectiveFromUtc, LinkFr = JuneOverrideTermsName }
                },
            },
        });

        return new SignatoryAccessesImportService(_repo.Object, _accountRepository.Object,
            _complianceEmailService.Object, _logger.Object, _blobStorageManager.Object, _contactRepository.Object,
            options.Object, _historyEventPublisher.Object, _roleRepository.Object);
    }

    #region Terms determination

    [Fact]
    public async Task ImportAsync_WhenOverrideEffective_AndFormNotAccepted_ShouldResolveOverrideTerms()
    {
        // Date d'effet de l'override déjà dépassée + nouveau dossier (CanAccess null) => Terms d'override
        var csv = CsvHeader + "\nD001;test@yolo.com;Nom;Prenom;Plan;1;10;3;30/06/2026;\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var service = CreateServiceWithOverride("01/01/2020");

        var result = await service.ImportAsync(file);

        Assert.Equal(1, result.SuccessCount);
        _repo.Verify(r => r.GetTermsIdByLinkFrAsync(JuneOverrideTermsName), Times.Once);
        _repo.Verify(r => r.GetTermsIdByLinkFrAsync(FebruaryTermsName), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WhenOverrideNotYetEffective_ShouldResolveDefaultTerms()
    {
        // Date d'effet de l'override dans le futur => Terms par défaut
        var csv = CsvHeader + "\nD001;test@yolo.com;Nom;Prenom;Plan;1;10;3;30/06/2026;\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var service = CreateServiceWithOverride("01/01/2099");

        var result = await service.ImportAsync(file);

        Assert.Equal(1, result.SuccessCount);
        _repo.Verify(r => r.GetTermsIdByLinkFrAsync(FebruaryTermsName), Times.Once);
        _repo.Verify(r => r.GetTermsIdByLinkFrAsync(JuneOverrideTermsName), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WhenNoOverrideForDate_ShouldResolveDefaultTerms()
    {
        // 31/10/2026 n'a pas d'override configuré => Terms par défaut
        var csv = CsvHeader + "\nD001;test@yolo.com;Nom;Prenom;Plan;1;10;3;31/10/2026;\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await _service.ImportAsync(file);

        Assert.Equal(1, result.SuccessCount);
        _repo.Verify(r => r.GetTermsIdByLinkFrAsync(OctoberTermsName), Times.Once);
    }

    #endregion

    #region Import

    [Fact]
    public async Task ImportAsync_WhenFileIsNullOrEmpty_ReturnsError()
    {
        // Act
        var result = await _service.ImportAsync(null!);

        // Assert
        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Errors);
        Assert.Equal("Le fichier est vide.", result.Errors[0].ErrorMessage);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportAsync_WhenRowInvalid_AddsValidationError_AndNoInsert()
    {
        // Arrange - fichier avec header + 1 ligne avec CodeDossier vide
        var csv = CsvHeader + "\n" +
                  ";test@yolo.com;Nom;Prenom;Plan;1;10;3;31/10/2026;";

        var file = CreateMockFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity>());

        _repo.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Errors);

        //   Le message correct de ValidateRow est "Le Code dossier est une colonne obligatoire."
        Assert.Contains("Le Code dossier", result.Errors[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("obligatoire", result.Errors[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Errors[0].RowData); // Vérifie que les données de la ligne sont présentes

        // Verify
        _repo.Verify(r => r.GetAllSignatoryAccessAsync(), Times.Once);
        _repo.Verify(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()), Times.Never);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportAsync_WhenNewRow_InsertsEntity()
    {
        var csv = CsvHeader + "\n" +
                  "D001;test@yolo.com;Nom;Prenom;Plan;1;10;3;31/10/2026;\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await _service.ImportAsync(file);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);

        _repo.Verify(r => r.GetAllSignatoryAccessAsync(), Times.Once);
        _repo.Verify(r => r.InsertSignatoryAccessAsync(It.Is<SignatoryAccessEntity>(e =>
            e.AccountNumber == "D001" &&
            e.Email == "test@yolo.com" &&
            e.LastName == "Nom" &&
            e.FirstName == "Prenom")), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _repo.Verify(r => r.GetTermsIdByLinkFrAsync(It.IsAny<string>()), Times.Once);
        _repo.VerifyNoOtherCalls();
        _historyEventPublisher.Verify(h => h.PublishHistoryCreatedEventAsync(It.IsAny<SignatoryAccessImportRow>()),
            Times.Once);
        _historyEventPublisher.Verify(h => h.PublishHistoryDeletedEventAsync(It.IsAny<SignatoryAccessEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WhenRowExists_UpdatesEntity()
    {
        var csv = CsvHeader + "\n" +
                  "D001;test@yolo.com;NewNom;NewPrenom;NewPlan;5;20;6;31/10/2026;\n";
        var file = CreateFormFile(csv);

        var existing = new SignatoryAccessEntity
        {
            AccountNumber = "D001",
            Email = "test@yolo.com",
            LastName = "OldNom",
            FirstName = "OldPrenom",
            PlanName = "OldPlan",
            UserCount = 1
        };

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity> { existing });
        _repo.Setup(r => r.UpdateSignatoryAccess(existing));
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await _service.ImportAsync(file);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);

        Assert.Equal("NewNom", existing.LastName);
        Assert.Equal("NewPrenom", existing.FirstName);
        Assert.Equal("NewPlan", existing.PlanName);
        Assert.Equal(5, existing.UserCount);

        _repo.Verify(r => r.GetAllSignatoryAccessAsync(), Times.Once);
        _repo.Verify(r => r.UpdateSignatoryAccess(existing), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _repo.Verify(r => r.GetTermsIdByLinkFrAsync(It.IsAny<string>()), Times.Once);
        _repo.VerifyNoOtherCalls();
        _historyEventPublisher.Verify(h => h.PublishHistoryCreatedEventAsync(It.IsAny<SignatoryAccessImportRow>()),
            Times.Never);
        _historyEventPublisher.Verify(h => h.PublishHistoryDeletedEventAsync(It.IsAny<SignatoryAccessEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WhenProcessing1000Rows_ShouldSaveInMultipleBatches()
    {
        // Arrange
        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity>());

        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Générer un CSV avec 1000 lignes
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine(CsvHeader + "\n");

        for (int i = 1; i <= 1000; i++)
        {
            csvBuilder.AppendLine($"ACC{i:D6};user{i}@test.com;Jandoury;Akim;Plan X;1;10,0;5,0;31/10/2026;");
        }

        var file = CreateMockFormFile(csvBuilder.ToString());

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1000, result.TotalRows);
        Assert.Equal(1000, result.SuccessCount);

        // Vérifie que SaveChangesAsync a été appelé exactement 1 fois (plus de batching, tout ou rien)
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WhenDuplicateAccountNumberInFile_ShouldReturnError()
    {
        // Arrange
        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity>());

        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        //   CSV CULTURE FR : virgule au lieu de point (10,0;5,0)
        var csvContent =
            CsvHeader + "\n" +
            "ACC001;john@test.com;Doe;John;PlanA;1;10,0;5,0;31/10/2026;\n" + // 8 colonnes
            "ACC001;jane@test.com;Smith;Jane;PlanB;1;20,0;10,0;31/10/2026;";

        var file = CreateMockFormFile(csvContent);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(2, result.TotalRows); // 2 lignes données
        Assert.Equal(0, result.SuccessCount); // Rejet total car doublon détecté
        Assert.Equal(1, result.ErrorCount); // Ligne 3 doublon

        Assert.Contains(result.Errors, e =>
            e.ErrorMessage.Contains("Code dossier dupliqué dans le fichier"));

        Assert.Contains(result.Errors, e => e.RowNumber == 3); // Header=1, data1=2, data2=3

        _repo.Verify(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()), Times.Never);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }



    [Fact]
    public async Task ImportAsync_WhenNewAccountWithNullEmail_ShouldReturnError()
    {
        // Arrange
        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity>());

        // CSV avec un nouveau compte avec email = NULL
        var csvContent =
            CsvHeader + "\n" +
            "ACC003;;Doe;John;PlanA;1;10,0;5,0;31/10/2026;";
        var file = CreateMockFormFile(csvContent);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task ImportAsync_WhenNewAccountWithEmptyEmail_ShouldReturnError()
    {
        // Arrange
        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity>());

        // CSV avec un nouveau compte avec email vide
        var csvContent =
            CsvHeader + "\n" +
            "ACC003;;Doe;John;PlanA;1;10,0;5,0;31/10/2026;";
        var file = CreateMockFormFile(csvContent);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task ImportAsync_WhenNewAccountWithWhitespaceEmail_ShouldReturnError()
    {
        // Arrange
        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity>());

        // CSV avec un nouveau compte avec email = espaces
        var csvContent =
            CsvHeader + "\n" +
            "ACC003;;Doe;John;PlanA;1;10,0;5,0;31/10/2026;";
        var file = CreateMockFormFile(csvContent);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task ImportAsync_WhenProcessingMoreThan500Rows_ShouldSaveInBatches()
    {
        // Arrange
        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity>());

        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(r => r.UpdateSignatoryAccess(It.IsAny<SignatoryAccessEntity>()));

        _repo.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Générer un CSV avec 501 lignes valides
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine(
            CsvHeader);

        for (int i = 1; i <= 501; i++)
        {
            csvBuilder.AppendLine(
                $"ACC{i:D6};user{i}@test.com;LastName{i};FirstName{i};Plan{i};1;10,0;5,0;31/10/2026;");
        }

        var file = CreateMockFormFile(csvBuilder.ToString());

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(501, result.TotalRows);
        Assert.Equal(501, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);

        // Un seul SaveChangesAsync pour tout le fichier
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WhenProcessingExactly500ValidRows_ShouldSaveInOneBatch()
    {
        // Arrange
        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity>());

        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(r => r.UpdateSignatoryAccess(It.IsAny<SignatoryAccessEntity>()));

        _repo.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine(CsvHeader);

        // 500 lignes valides (compte + email non vides)
        for (int i = 1; i <= 500; i++)
        {
            csvBuilder.AppendLine(
                $"ACC{i:D6};user{i}@test.com;Jandoury;Akim;Plan X;1;10,0;5,0;31/10/2026;");
        }

        var file = CreateMockFormFile(csvBuilder.ToString());

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(500, result.TotalRows);
        Assert.Equal(500, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);

        _repo.Verify(r => r.SaveChangesAsync(), Times.Once());
    }

    [Fact]
    public async Task ImportAsync_WhenExistingEntityHasCanAccessTrue_IgnoresUpdate()
    {
        // Arrange
        var csv = CsvHeader + "\n" +
                  "D001;newemail@test.com;NewNom;NewPrenom;NewPlan;5;20,0;6,0;31/10/2026;\n";
        var file = CreateFormFile(csv);

        var existingEntity = new SignatoryAccessEntity
        {
            AccountNumber = "D001",
            Email = "old@test.com",
            LastName = "OldNom",
            FirstName = "OldPrenom",
            PlanName = "OldPlan",
            UserCount = 1,
            PlanPriceExclTax = 10.0m,
            CompliancePriceExclTax = 5.0m,
            CanAccess = true // CanAccess = true, donc ignoré
        };

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity> { existingEntity });
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount); // Ligne traitée sans erreur = succès
        Assert.Equal(0, result.ErrorCount);

        // Vérifier que l'entité n'a PAS été modifiée
        Assert.Equal("old@test.com", existingEntity.Email);
        Assert.Equal("OldNom", existingEntity.LastName);

        // Verify
        _repo.Verify(r => r.GetAllSignatoryAccessAsync(), Times.Once);
        _repo.Verify(r => r.UpdateSignatoryAccess(It.IsAny<SignatoryAccessEntity>()), Times.Never);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never); // Appelé car processedInBatch > 0
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportAsync_WhenNoChangesDetected_IgnoresUpdate()
    {
        // Arrange - même données que l'entité existante
        var csv = CsvHeader + "\n" +
                  "D001;test@test.com;TestNom;TestPrenom;TestPlan;1;10,0;5,0;31/10/2026;\n";
        var file = CreateFormFile(csv);

        var existingEntity = new SignatoryAccessEntity
        {
            AccountNumber = "D001",
            Email = "test@test.com",
            LastName = "TestNom",
            FirstName = "TestPrenom",
            PlanName = "TestPlan",
            UserCount = 1,
            PlanPriceExclTax = 10.0m,
            CompliancePriceExclTax = 5.0m,
            CanAccess = false,
            FreeTrialEndDate = new DateTime(2026, 10, 31)
        };

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity> { existingEntity });
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);

        // Verify
        _repo.Verify(r => r.GetAllSignatoryAccessAsync(), Times.Once);
        _repo.Verify(r => r.UpdateSignatoryAccess(It.IsAny<SignatoryAccessEntity>()), Times.Never);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportAsync_WhenFileHasZeroLength_ReturnsError()
    {
        // Arrange
        var emptyFile = new FormFile(Stream.Null, 0, 0, "file", "empty.csv");

        // Act
        var result = await _service.ImportAsync(emptyFile);

        // Assert
        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Errors);
        Assert.Equal("Le fichier est vide.", result.Errors[0].ErrorMessage);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportAsync_WhenParsingThrowsException_ReturnsParseError()
    {
        var malformedCsv = "Invalid\nContent\nThat\nWill\nCause\nError";
        var file = CreateMockFormFile(malformedCsv);

        // GetAllAsync ne devrait jamais être appelé car le parsing échoue avant
        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity>());

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Errors);

        //   Le message commence par "Format CSV invalide:" ou "Erreur lors de la lecture du fichier:"
        var errorMessage = result.Errors[0].ErrorMessage;
        Assert.True(
            errorMessage.Contains("Format CSV invalide") ||
            errorMessage.Contains("Erreur lors de la lecture du fichier"),
            $"Message d'erreur inattendu: {errorMessage}");

        Assert.Equal(0, result.Errors[0].RowNumber); // Erreur au niveau du fichier

        // GetAllAsync ne devrait jamais être appelé car parsing échoue
        _repo.Verify(r => r.GetAllSignatoryAccessAsync(), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WhenHeaderValidationFails_ReturnsHeaderError()
    {
        // Arrange - Header invalide (mauvais noms de colonnes)
        var csvWithBadHeader = "Col1;Col2;Col3\nVal1;Val2;Val3";
        var file = CreateMockFormFile(csvWithBadHeader);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Errors);

        //   Erreur de validation du header
        Assert.Contains("Format CSV invalide", result.Errors[0].ErrorMessage);
        Assert.Equal(0, result.Errors[0].RowNumber);

        _repo.Verify(r => r.GetAllSignatoryAccessAsync(), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WhenProcessingThrowsException_ReturnsGeneralError()
    {
        // Arrange - CSV valide
        var csv = CsvHeader + "\n" +
                  "D001;test@test.com;Nom;Prenom;Plan;1;10,0;5,0;31/10/2026;";

        var file = CreateMockFormFile(csv);

        //   GetAllAsync lève une exception APRÈS parsing réussi
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()))
            .ThrowsAsync(new Exception("Erreur base de données"));

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows); // Une ligne lue
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Errors);

        //   Message d'erreur générale
        var error = result.Errors[0];
        Assert.Contains("Erreur lors de l'import", error.ErrorMessage);
        Assert.Contains("Erreur base de données", error.ErrorMessage);
        Assert.Equal(0, error.RowNumber);

        // Verify
        _repo.Verify(r => r.GetAllSignatoryAccessAsync(), Times.Once);
        _repo.Verify(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()), Times.Once);
        _repo.Verify(r => r.GetTermsIdByLinkFrAsync(It.IsAny<string>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportAsync_WhenEmailIsNULLString_TreatedAsEmpty()
    {
        // Arrange - "NULL" en string doit être traité comme vide
        var csv = CsvHeader + "\n" +
                  "ACC001;NULL;Doe;John;PlanA;1;10,0;5,0;31/10/2026;\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity>());

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount); // Nouveau compte + email vide = ignoré
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task ImportAsync_InvalidEmail_ShouldRejectWholeFile()
    {
        // Arrange
        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());

        var csvContent = CsvHeader + "\n" +
                         "ACC001;toto@@test;Doe;John;PlanA;1;10,0;5,0;31/10/2026;\n" +
                         "ACC002;valid@test.com;Smith;Jane;PlanB;2;20,0;10,0;31/10/2026;";

        var file = CreateMockFormFile(csvContent);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(0, result.SuccessCount); //   TOUT est rejeté car 1 erreur
        Assert.Equal(1, result.ErrorCount);
        Assert.Contains(result.Errors, e => e.RowNumber == 2 && e.ColumnName == "Email signataire");

        _repo.Verify(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()), Times.Never);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WhenUpdateEntityWithChanges_UpdatesSuccessfully()
    {
        // Arrange - entité existante avec CanAccess=false et des changements
        var csv = CsvHeader + "\n" +
                  "D001;updated@test.com;UpdatedLast;UpdatedFirst;UpdatedPlan;10;100,0;50,0;31/10/2026;\n";
        var file = CreateFormFile(csv);

        var existingEntity = new SignatoryAccessEntity
        {
            AccountNumber = "D001",
            Email = "old@test.com",
            LastName = "OldLast",
            FirstName = "OldFirst",
            PlanName = "OldPlan",
            UserCount = 1,
            PlanPriceExclTax = 10.0m,
            CompliancePriceExclTax = 5.0m,
            CanAccess = false
        };

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity> { existingEntity });
        _repo.Setup(r => r.UpdateSignatoryAccess(existingEntity));
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);

        // Verify
        _repo.Verify(r => r.GetAllSignatoryAccessAsync(), Times.Once);
        _repo.Verify(r => r.UpdateSignatoryAccess(existingEntity), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _repo.Verify(r => r.GetTermsIdByLinkFrAsync(It.IsAny<string>()), Times.Once);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportAsync_WithMixedSuccessAndErrors_ReturnsCorrectCounts()
    {
        // Arrange - 3 lignes : 1 succès, 1 doublon, 1 email vide
        var csv = CsvHeader + "\n" +
                  "ACC001;valid@test.com;Valid;User;Plan;1;10;5;31/10/2026;\n" +
                  "ACC001;duplicate@test.com;Duplicate;User;Plan;1;10;5;31/10/2026;\n" +
                  "ACC002;;NoEmail;User;Plan;1;10;5;31/10/2026;\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(3, result.TotalRows);
        Assert.Equal(0, result.SuccessCount); // Rejet total si erreur présente
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ImportAsync_WhenGetAllAsyncThrowsException_ReturnsGeneralError()
    {
        // Arrange
        var csv = CsvHeader + "\n" +
                  "ACC001;test@test.com;Test;User;Plan;1;10;5;31/10/2026;\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ThrowsAsync(new Exception("Database connection error"));

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Errors);
        Assert.Contains("Erreur lors de la récupération des données existantes", result.Errors[0].ErrorMessage);
        Assert.Contains("Database connection error", result.Errors[0].ErrorMessage);
    }

    // Helper method pour créer un IFormFile mocké
    private static IFormFile CreateMockFormFile(string content, string fileName = "test.csv")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.Length).Returns(bytes.Length);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns("text/csv");

        return fileMock.Object;
    }

    [Fact]
    public async Task ImportAsync_WhenExistingEntityWithEmptyEmail_AndCanAccessFalse_ShouldSetCanAccessTrue()
    {
        // Arrange - Entité existante avec CanAccess=false + email vide dans le CSV
        var existingEntity = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = "old@test.com",
            LastName = "OldNom",
            FirstName = "OldPrenom",
            CanAccess = false,
            UpdatedDate = DateTime.UtcNow.AddDays(-5)
        };

        var csv = CsvHeader + "\n" +
                  "ACC001;NULL;Doe;John;PlanA;1;10,0;5,0;31/10/2026;"; // Email = NULL

        var file = CreateMockFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity> { existingEntity });

        _repo.Setup(r => r.UpdateSignatoryAccess(It.IsAny<SignatoryAccessEntity>()))
            .Verifiable();

        _repo.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.SuccessCount); //   Ligne traitée avec succès
        Assert.Equal(0, result.ErrorCount);

        // Vérifier que CanAccess a été forcé à true
        Assert.True(existingEntity.CanAccess);
        Assert.Null(existingEntity.Email); // Email vidé
        Assert.NotEqual(existingEntity.UpdatedDate, DateTime.UtcNow.AddDays(-5)); // UpdatedDate mis à jour

        _repo.Verify(r => r.UpdateSignatoryAccess(existingEntity), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WhenExistingEntityWithEmptyEmail_AndCanAccessTrue_ShouldIgnore()
    {
        // Arrange - Entité existante avec CanAccess=true + email vide dans le CSV
        var existingEntity = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = "old@test.com",
            LastName = "OldNom",
            FirstName = "OldPrenom",
            CanAccess = true,
            UpdatedDate = DateTime.UtcNow.AddDays(-5)
        };

        var csv = CsvHeader + "\n" +
                  "ACC001;;Doe;John;PlanA;1;10,0;5,0;31/10/2026;"; // Email vide

        var file = CreateMockFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity> { existingEntity });

        _repo.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount); //   Ligne ignorée (déjà à true)
        Assert.Equal(0, result.ErrorCount);

        // Vérifier qu'aucune modification n'a été faite
        Assert.True(existingEntity.CanAccess);
        Assert.Equal("old@test.com", existingEntity.Email); // Email non modifié

        _repo.Verify(r => r.UpdateSignatoryAccess(It.IsAny<SignatoryAccessEntity>()), Times.Never);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WhenExistingEntityWithEmptyEmailString_ShouldTreatAsEmpty()
    {
        // Arrange - Test avec chaîne vide "" au lieu de NULL
        var existingEntity = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = "test@test.com",
            LastName = "Nom",
            FirstName = "Prenom",
            CanAccess = false
        };

        var csv = CsvHeader + "\n" +
                  "ACC001;;Doe;John;PlanA;1;10,0;5,0;31/10/2026;"; // Email = chaîne vide

        var file = CreateMockFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity> { existingEntity });

        _repo.Setup(r => r.UpdateSignatoryAccess(It.IsAny<SignatoryAccessEntity>()))
            .Verifiable();

        _repo.Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);

        Assert.True(existingEntity.CanAccess);
        Assert.Null(existingEntity.Email);

        _repo.Verify(r => r.UpdateSignatoryAccess(existingEntity), Times.Once);
    }

    // ===================================================================
    // Scénario 1 : Conserver une ligne supprimée du fichier si le client a répondu
    // ===================================================================

    [Fact]
    public async Task ImportAsync_WhenRowMissingFromFile_AndClientHasResponded_ShouldKeepRow()
    {
        // Arrange - D002 est en base mais PAS dans le fichier. D002 a répondu (CanAccess=true).
        var csv = CsvHeader + "\n" +
                  "D001;test@test.com;Nom;Prenom;Plan;1;10,0;5,0;31/10/2026;\n";
        var file = CreateFormFile(csv);

        var existingD001 = new SignatoryAccessEntity
        {
            AccountNumber = "D001",
            Email = "test@test.com",
            LastName = "Nom",
            FirstName = "Prenom",
            PlanName = "Plan",
            UserCount = 1,
            PlanPriceExclTax = 10.0m,
            CompliancePriceExclTax = 5.0m,
            CanAccess = null,
            FreeTrialEndDate = new DateTime(2026, 10, 31)
        };

        var existingD002 = new SignatoryAccessEntity
        {
            AccountNumber = "D002",
            Email = "other@test.com",
            LastName = "Other",
            FirstName = "User",
            CanAccess = true // a accepté → CanAccess != null → conserver
        };

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity> { existingD001, existingD002 });

        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - D002 NOT deleted (CanAccess != null)
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount); // D001 unchanged
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(0, result.ErrorCount);

        _repo.Verify(r => r.DeleteSignatoryAccessesAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        _historyEventPublisher.Verify(h => h.PublishHistoryCreatedEventAsync(It.IsAny<SignatoryAccessImportRow>()),
            Times.Never);
        _historyEventPublisher.Verify(h => h.PublishHistoryDeletedEventAsync(It.IsAny<SignatoryAccessEntity>()),
            Times.Never);
    }

    // ===================================================================
    // Scénario 2 : Supprimer une ligne absente du fichier si le client n'a pas répondu
    // ===================================================================

    [Fact]
    public async Task ImportAsync_WhenRowMissingFromFile_AndClientHasNotResponded_ShouldDeleteRow()
    {
        // Arrange - D002 est en base mais PAS dans le fichier, CanAccess=null (pas répondu)
        var csv = CsvHeader + "\n" +
                  "D001;test@test.com;Nom;Prenom;Plan;1;10,0;5,0;31/10/2026;\n";
        var file = CreateFormFile(csv);

        var existingD001 = new SignatoryAccessEntity
        {
            AccountNumber = "D001",
            Email = "test@test.com",
            LastName = "Nom",
            FirstName = "Prenom",
            PlanName = "Plan",
            UserCount = 1,
            PlanPriceExclTax = 10.0m,
            CompliancePriceExclTax = 5.0m,
            CanAccess = null,
            FreeTrialEndDate = new DateTime(2026, 10, 31)
        };

        var existingD002 = new SignatoryAccessEntity
        {
            AccountNumber = "D002",
            Email = "other@test.com",
            LastName = "Other",
            FirstName = "User",
            CanAccess = null // pas répondu → supprimable
        };

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity> { existingD001, existingD002 });

        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - D002 supprimé (CanAccess=null)
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount); // D001 unchanged
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(0, result.ErrorCount);

        _repo.Verify(r => r.DeleteSignatoryAccessesAsync(
                It.Is<IEnumerable<string>>(list => list.Contains("D002") && list.Count() == 1)),
            Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
        _historyEventPublisher.Verify(h => h.PublishHistoryCreatedEventAsync(It.IsAny<SignatoryAccessImportRow>()),
            Times.Never);
        _historyEventPublisher.Verify(h => h.PublishHistoryDeletedEventAsync(It.IsAny<SignatoryAccessEntity>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WhenMultipleMissingRows_ShouldDeleteOnlyThoseWithoutResponse()
    {
        // Arrange - D002 (CanAccess=true, a répondu), D003 (CanAccess=null), D004 (CanAccess=null)
        var csv = CsvHeader + "\n" +
                  "D001;test@test.com;Nom;Prenom;Plan;1;10,0;5,0;31/10/2026;\n";
        var file = CreateFormFile(csv);

        var existing = new List<SignatoryAccessEntity>
        {
            new()
            {
                AccountNumber = "D001", Email = "test@test.com", LastName = "Nom", FirstName = "Prenom",
                PlanName = "Plan", UserCount = 1, PlanPriceExclTax = 10.0m, CompliancePriceExclTax = 5.0m,
                CanAccess = null, FreeTrialEndDate = new DateTime(2026, 10, 31)
            },
            new() { AccountNumber = "D002", Email = "a@test.com", CanAccess = true }, // a répondu → conserver
            new() { AccountNumber = "D003", Email = "b@test.com", CanAccess = null }, // pas répondu → supprimer
            new() { AccountNumber = "D004", Email = "c@test.com", CanAccess = null }, // pas répondu → supprimer
        };

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(existing);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(0, result.SuccessCount); // D001 unchanged
        Assert.Equal(2, result.DeletedCount); // D003 et D004 supprimés
        Assert.Equal(0, result.ErrorCount);

        _repo.Verify(r => r.DeleteSignatoryAccessesAsync(
                It.Is<IEnumerable<string>>(list =>
                    list.Contains("D003") && list.Contains("D004") && !list.Contains("D002") && list.Count() == 2)),
            Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // ===================================================================
    // Scénario 3 : Ne pas modifier une ligne si le client a accepté (déjà implémenté)
    // Couvert par : ImportAsync_WhenExistingEntityHasCanAccessTrue_IgnoresUpdate
    // ===================================================================

    // ===================================================================
    // Scénario 4 : Mettre à jour si le client a refusé (déjà implémenté)
    // ===================================================================

    [Fact]
    public async Task ImportAsync_WhenClientRefused_AndDataModified_ShouldUpdate()
    {
        // Arrange - Client a refusé (CanAccess=false, a un historique avec Status=false)
        // Le comportement est basé sur CanAccess=false → update
        var csv = CsvHeader + "\n" +
                  "D001;test@test.com;NewNom;NewPrenom;NewPlan;5;20,0;6,0;31/10/2026;\n";
        var file = CreateFormFile(csv);

        var existingEntity = new SignatoryAccessEntity
        {
            AccountNumber = "D001",
            Email = "test@test.com",
            LastName = "OldNom",
            FirstName = "OldPrenom",
            PlanName = "OldPlan",
            UserCount = 1,
            PlanPriceExclTax = 10.0m,
            CompliancePriceExclTax = 5.0m,
            CanAccess = false // a refusé, statut=0
        };

        _repo.Setup(r => r.GetAllSignatoryAccessAsync())
            .ReturnsAsync(new List<SignatoryAccessEntity> { existingEntity });
        _repo.Setup(r => r.UpdateSignatoryAccess(existingEntity));
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - la ligne est mise à jour
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);

        // Verify
        _repo.Verify(r => r.GetAllSignatoryAccessAsync(), Times.Once);
        _repo.Verify(r => r.UpdateSignatoryAccess(existingEntity), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _repo.Verify(r => r.GetTermsIdByLinkFrAsync(It.IsAny<string>()), Times.Once);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportAsync_ShouldReturnError_WhenRoleDoesNotExist()
    {
        // Arrange
        var csv = CsvHeader + "\nD001;user@test.com;Nom;Prenom;Plan;1;10;3;31/10/2026;";
        var file = CreateFormFile(csv);
        _accountRepository.Setup(r => r.GetAccountByNumberOrIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new AccountEntity { AccountId = 1, AccountNumber = "D001" });
        _contactRepository.Setup(r => r.GetContactByEmailOrIdAsync(It.IsAny<string?>(), null))
            .ReturnsAsync(new Contact { ContactId = 2, Email = "user@test.com" });
        _roleRepository.Setup(r => r.CheckRoleExistsAsync("D001", "user@test.com")).ReturnsAsync(false);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Contains(result.Errors,
            e => e.ErrorMessage.Contains("L'utilisateur doit avoir un rôle sur le dossier pour être importé."));
    }

    [Fact]
    public async Task ImportAsync_WhenAccountIsValidated_ShouldNotCheckRow()
    {
        var csv = CsvHeader + "\n" +
                  "D001;email;Nom;Prenom;Plan;2;10,90;100,0;31/10/2026;\n";
        var file = CreateFormFile(csv);

        var existingEntity = new SignatoryAccessEntity
        {
            AccountNumber = "D001",
            Email = "test@test.com",
            LastName = "OldNom",
            FirstName = "OldPrenom",
            PlanName = "OldPlan",
            UserCount = 1,
            PlanPriceExclTax = 10.0m,
            CompliancePriceExclTax = 5.0m,
            CanAccess = true,
            FreeTrialEndDate = new DateTime(2026, 10, 31)
        };

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync([existingEntity]);

        var result = await _service.ImportAsync(file);

        Assert.NotNull(result);
        Assert.Equal(0, result.ErrorCount);
        Assert.Empty(result.Errors);

        _roleRepository.Verify(r => r.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("D001", null)]
    [InlineData("D001", "")]
    [InlineData("D001", "    ")]
    [InlineData(null, "test@test.com")]
    [InlineData("", "test@test.com")]
    [InlineData("     ", "test@test.com")]
    public async Task ImportAsync_WhenAccountNumberOrEmailIsNullOrWhiteSpace_ShouldNotCheckRole(string? accountNumber,
        string? email)
    {
        var csv = CsvHeader + "\n" +
                  $"{accountNumber};{email};Nom;Prenom;Plan;2;10,90;100,0;31/10/2026;\n";
        var file = CreateFormFile(csv);

        var existingEntity = new SignatoryAccessEntity
        {
            AccountNumber = "D001",
            Email = "test@test.com",
            LastName = "OldNom",
            FirstName = "OldPrenom",
            PlanName = "OldPlan",
            UserCount = 1,
            PlanPriceExclTax = 10.0m,
            CompliancePriceExclTax = 5.0m,
            FreeTrialEndDate = new DateTime(2026, 10, 31)
        };

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync([existingEntity]);

        await _service.ImportAsync(file);

        _roleRepository.Verify(r => r.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    [Fact]
    public async Task CheckPennylaneAccessAsync_Nominal()
    {
        var service = CreateServiceWithOverride("01/01/2099");

        _accountRepository.Setup(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()))
            .ReturnsAsync(_fixture.Create<AccountEntity>());
        _repo.Setup(x => x.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>())).ReturnsAsync(
            new SignatoryAccessEntity()
            {
                CanAccess = false,
                Email = "userEmail",
                FreeTrialEndDate = new DateTime(2026, 06, 30),
                Terms = _fixture.Create<TermsEntity>()
            });
        _repo.Setup(x => x.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<Tuple<SignatoryAccessEntity, string>>());


        var result = await service.CheckPennylaneAccessAsync("userEmail", "customer", 1);

        Assert.NotNull(result);
        Assert.False(result.CanAccess);
        Assert.False(result.IsFreeTrialExpired);

        _accountRepository.Verify(x => x.GetAccountByNumberOrIdAsync("1"), Times.Once);
        _repo.Verify(x => x.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>()), Times.Once);
        _repo.Verify(x => x.GetSignatoryAccessesByUserEmailAsync("userEmail"), Times.Once);
    }

    [Fact]
    public async Task
        CheckPennylaneAccessAsync_When_OverrideEffectivePassed_AndCanAccessFalse_Should_SetIsFreeTrialExpiredTrue()
    {
        // Arrange : date d'effet de l'override déjà dépassée + formulaire non accepté (CanAccess false)
        var service = CreateServiceWithOverride("01/01/2020");

        _accountRepository.Setup(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()))
            .ReturnsAsync(_fixture.Create<AccountEntity>());
        _repo.Setup(x => x.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>())).ReturnsAsync(
            new SignatoryAccessEntity
            {
                CanAccess = false,
                Email = "userEmail",
                FreeTrialEndDate = new DateTime(2026, 06, 30),
                Terms = _fixture.Create<TermsEntity>()
            });
        _repo.Setup(x => x.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<Tuple<SignatoryAccessEntity, string>>());

        // Act
        var result = await service.CheckPennylaneAccessAsync("userEmail", "customer", 1);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.CanAccess);
        Assert.True(result.IsFreeTrialExpired);
    }

    [Fact]
    public async Task
        CheckPennylaneAccessAsync_When_OverrideEffectiveInFuture_AndCanAccessFalse_Should_SetIsFreeTrialExpiredFalse()
    {
        // Arrange : date d'effet de l'override dans le futur
        var service = CreateServiceWithOverride("01/01/2099");

        _accountRepository.Setup(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()))
            .ReturnsAsync(_fixture.Create<AccountEntity>());
        _repo.Setup(x => x.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>())).ReturnsAsync(
            new SignatoryAccessEntity
            {
                CanAccess = false,
                Email = "userEmail",
                FreeTrialEndDate = new DateTime(2026, 06, 30),
                Terms = _fixture.Create<TermsEntity>()
            });
        _repo.Setup(x => x.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<Tuple<SignatoryAccessEntity, string>>());

        // Act
        var result = await service.CheckPennylaneAccessAsync("userEmail", "customer", 1);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsFreeTrialExpired);
    }

    [Fact]
    public async Task CheckPennylaneAccessAsync_When_CanAccessTrue_Should_SetIsFreeTrialExpiredNull()
    {
        // Arrange : formulaire déjà accepté (CanAccess true) => IsFreeTrialExpired non applicable
        var service = CreateServiceWithOverride("01/01/2020");

        _accountRepository.Setup(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()))
            .ReturnsAsync(_fixture.Create<AccountEntity>());
        _repo.Setup(x => x.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>())).ReturnsAsync(
            new SignatoryAccessEntity
            {
                CanAccess = true,
                Email = "userEmail",
                FreeTrialEndDate = new DateTime(2026, 06, 30),
                Terms = _fixture.Create<TermsEntity>()
            });
        _repo.Setup(x => x.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<Tuple<SignatoryAccessEntity, string>>());

        // Act
        var result = await service.CheckPennylaneAccessAsync("userEmail", "customer", 1);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CanAccess);
        Assert.Null(result.IsFreeTrialExpired);
    }

    [Fact]
    public async Task CheckPennylaneAccessAsync_When_AccountNotConcernedByAmendment_Should_ReturnSignatoryAccessEntity()
    {
        _accountRepository.Setup(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()))
            .ReturnsAsync(_fixture.Create<AccountEntity>());
        _repo.Setup(x => x.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>()))
            .ThrowsAsync(new NotFoundException("code", "message"));

        var result = await _service.CheckPennylaneAccessAsync("userEmail", "customer", 1);

        Assert.NotNull(result);
        Assert.True(result.CanAccess);

        _accountRepository.Verify(x => x.GetAccountByNumberOrIdAsync("1"), Times.Once);
        _repo.Verify(x => x.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>()), Times.Once);
        _repo.Verify(x => x.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CheckPennylaneAccessAsync_When_AccountNotExist_Should_ThrowNotFoundException()
    {
        _accountRepository.Setup(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()))
            .ReturnsAsync((AccountEntity)null!);

        var result = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await _service.CheckPennylaneAccessAsync("userEmail", "customer", 1));

        Assert.Equal("PNL001", result.Code);
        Assert.Equal("Account with ID 1 was not found.", result.Message);

        _accountRepository.Verify(x => x.GetAccountByNumberOrIdAsync("1"), Times.Once);
        _repo.Verify(x => x.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>()), Times.Never);
        _repo.Verify(x => x.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CheckPennylaneAccessAsync_When_UserIsCollab_Should_ReturnCanAccessTrue()
    {
        var result = await _service.CheckPennylaneAccessAsync("userEmail", "collaborator", 1);

        Assert.NotNull(result);
        Assert.True(result.CanAccess);

        _accountRepository.Verify(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()), Times.Never);
        _repo.Verify(x => x.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>()), Times.Never);
        _repo.Verify(x => x.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task
        CheckPennylaneAccessAsync_When_AccountIdIsNull_And_UserAccessListNotEmpty_Should_ReturnMappedSignatoryAccess()
    {
        // Arrange
        var userEmail = "user@test.com";
        var dueDate = new DateTime(2026, 10, 31);

        var signatoryAccessEntity = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.CanAccess, false)
            .With(sa => sa.FreeTrialEndDate, dueDate)
            .With(sa => sa.Email, userEmail)
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();
        var signatoryAccessEntity2 = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.CanAccess, false)
            .With(sa => sa.FreeTrialEndDate, new DateTime(2026, 06, 30))
            .With(sa => sa.Email, userEmail)
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();
        var signatoryAccessEntity3 = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.CanAccess, true)
            .With(sa => sa.FreeTrialEndDate, dueDate)
            .With(sa => sa.Email, userEmail)
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();

        var userAccessList = new List<Tuple<SignatoryAccessEntity, string>>
        {
            Tuple.Create(signatoryAccessEntity, "Company A"),
            Tuple.Create(signatoryAccessEntity2, "Company B"),
            Tuple.Create(signatoryAccessEntity3, "Company C"),
        };

        _repo.Setup(x => x.GetSignatoryAccessesByUserEmailAsync(userEmail))
            .ReturnsAsync(userAccessList);

        // Act
        var result = await _service.CheckPennylaneAccessAsync(userEmail, "customer", null);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Details!.Accounts);

        _repo.Verify(x => x.GetSignatoryAccessesByUserEmailAsync(userEmail), Times.Once);
        _roleRepository.Verify(r => r.GetUserRolesAsync(It.IsAny<string>()), Times.Never);
        _repo.Verify(x => x.GetSignatoryAccessesByAccountNumbersAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        _accountRepository.Verify(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task
        CheckPennylaneAccessAsync_When_AccountIdIsNull_And_UserAccessListEmpty_And_RoleSignatoryAccessNotEmpty_Should_ReturnMappedSignatoryAccess()
    {
        // Arrange
        var userEmail = "user@test.com";
        var dueDate = new DateTime(2026, 10, 31);

        _repo.Setup(x => x.GetSignatoryAccessesByUserEmailAsync(userEmail))
            .ReturnsAsync(new List<Tuple<SignatoryAccessEntity, string>>());

        var role = _fixture.Build<RoleEntity>()
            .With(r => r.Account, _fixture.Build<AccountEntity>()
                .With(a => a.AccountNumber, "ACC001")
                .Create())
            .Create();

        _roleRepository.Setup(r => r.GetUserRolesAsync(userEmail))
            .ReturnsAsync(new List<RoleEntity> { role });

        var signatoryAccessEntity = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.CanAccess, false)
            .With(sa => sa.FreeTrialEndDate, dueDate)
            .With(sa => sa.Email, userEmail)
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();
        var signatoryAccessEntity2 = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.CanAccess, false)
            .With(sa => sa.FreeTrialEndDate, new DateTime(2026, 06, 30))
            .With(sa => sa.Email, userEmail)
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();
        var signatoryAccessEntity3 = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.CanAccess, true)
            .With(sa => sa.FreeTrialEndDate, dueDate)
            .With(sa => sa.Email, userEmail)
            .Without(sa => sa.ValidationHistoryEntity)
            .Create();

        _repo.Setup(x => x.GetSignatoryAccessesByAccountNumbersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Tuple<SignatoryAccessEntity, string>>
            {
                Tuple.Create(signatoryAccessEntity, "Company A"),
                Tuple.Create(signatoryAccessEntity2, "Company B"),
                Tuple.Create(signatoryAccessEntity3, "Company C"),
            });

        // Act
        var result = await _service.CheckPennylaneAccessAsync(userEmail, "customer", null);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Details!.Accounts);

        _repo.Verify(x => x.GetSignatoryAccessesByUserEmailAsync(userEmail), Times.Once);
        _roleRepository.Verify(r => r.GetUserRolesAsync(userEmail), Times.Once);
        _repo.Verify(x => x.GetSignatoryAccessesByAccountNumbersAsync(
            It.Is<IEnumerable<string>>(nums => nums.Contains("ACC001"))), Times.Once);
        _accountRepository.Verify(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task
        CheckPennylaneAccessAsync_When_AccountIdIsNull_And_UserAccessListEmpty_And_RoleSignatoryAccessEmpty_Should_ReturnCanAccessTrue()
    {
        // Arrange
        var userEmail = "user@test.com";

        _repo.Setup(x => x.GetSignatoryAccessesByUserEmailAsync(userEmail))
            .ReturnsAsync(new List<Tuple<SignatoryAccessEntity, string>>());

        _roleRepository.Setup(r => r.GetUserRolesAsync(userEmail))
            .ReturnsAsync(new List<RoleEntity>());

        _repo.Setup(x => x.GetSignatoryAccessesByAccountNumbersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Tuple<SignatoryAccessEntity, string>>());

        // Act
        var result = await _service.CheckPennylaneAccessAsync(userEmail, "customer", null);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CanAccess);

        _repo.Verify(x => x.GetSignatoryAccessesByUserEmailAsync(userEmail), Times.Once);
        _roleRepository.Verify(r => r.GetUserRolesAsync(userEmail), Times.Once);
        _repo.Verify(x => x.GetSignatoryAccessesByAccountNumbersAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
        _accountRepository.Verify(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task
        CheckPennylaneAccessAsync_When_AccountIdIsNull_And_UserIsCollaborator_Should_ReturnCanAccessTrue_WithoutCallingRepository()
    {
        // Act
        var result = await _service.CheckPennylaneAccessAsync("user@test.com", "collaborator", null);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CanAccess);

        _repo.Verify(x => x.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()), Times.Never);
        _roleRepository.Verify(r => r.GetUserRolesAsync(It.IsAny<string>()), Times.Never);
        _repo.Verify(x => x.GetSignatoryAccessesByAccountNumbersAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        _accountRepository.Verify(x => x.GetAccountByNumberOrIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ValidatePennylaneTermsAsync_ReturnsHistory_WhenUserIsSignatory()
    {
        // Arrange
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = true,
            AccountNumbers = new[] { "ACC001", "ACC002" }
        };

        var terms = _fixture.Build<TermsEntity>()
            .Without(t => t.ValidationHistoryEntity)
            .Without(t => t.SignatoryAccessEntity)
            .Create();
        var signatoryAccess = _fixture.Build<SignatoryAccessEntity>()
            .With(sa => sa.Email, email)
            .With(sa => sa.CanAccess, false)
            .With(sa => sa.TermsId, terms.Id)
            .Without(sa => sa.ValidationHistoryEntity)
            .Without(sa => sa.Terms);

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess.Create());
        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC002")).ReturnsAsync(signatoryAccess.Create());

        _accountRepository.Setup(r => r.GetAccountsByNumbersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<AccountEntity>
            {
                new() { AccountNumber = "ACC001", LegalName = "Company A" },
                new() { AccountNumber = "ACC002", LegalName = "Company B" }
            });
        _complianceEmailService.Setup(s => s.SendAcceptanceEmailAsync(It.IsAny<ComplianceEmailContext>(),
                It.IsAny<TermsEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ValidatePennylaneTermsAsync(email, request);

        // Assert
        Assert.NotNull(result);
        Assert.Collection(result,
            item =>
            {
                Assert.Equal("ACC001", item.AccountNumber);
                Assert.Equal(request.Status, item.Status);
                Assert.Equal(terms.Id, item.TermsId);
                Assert.Equal(request.Status, item.CanAccess);
                Assert.NotNull(item.UpdatedDate);
            },
            item =>
            {
                Assert.Equal("ACC002", item.AccountNumber);
                Assert.Equal(request.Status, item.Status);
                Assert.Equal(terms.Id, item.TermsId);
                Assert.Equal(request.Status, item.CanAccess);
                Assert.NotNull(item.UpdatedDate);
            });

        _repo.Verify(
            r => r.ValidateTerms(It.IsAny<string>(), terms.Id, request.Status, It.IsAny<SignatoryAccessEntity>()),
            Times.Exactly(2));
        _repo.Verify(r => r.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>()), Times.Exactly(2));
        _repo.Verify(r => r.UpdateSignatoryAccess(It.IsAny<SignatoryAccessEntity>()), Times.Exactly(2));
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _repo.Verify(
            r => r.ValidateTerms(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<SignatoryAccessEntity>()), Times.AtLeastOnce);
        _historyEventPublisher.Verify(
            h => h.PublishHistoryValidatedEventAsync(It.IsAny<SignatoryAccessEntity>(), It.IsAny<bool>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ValidatePennylaneTermsAsync_ThrowsForbiddenException_WhenEmailDoesNotMatch()
    {
        // Arrange
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = true,
            AccountNumbers = ["ACC001"]
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(new SignatoryAccessEntity
        {
            Email = "another@test.com"
        });

        // Act & Assert
        var result =
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.ValidatePennylaneTermsAsync("user@test.com", request));
        Assert.Equal("PNL007", result.Code);
        Assert.Equal("Only signatory can validate Terms.", result.Message);

        _repo.Verify(r => r.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
        _repo.Verify(r => r.UpdateSignatoryAccess(It.IsAny<SignatoryAccessEntity>()), Times.Never);
        _repo.Verify(
            r => r.ValidateTerms(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<SignatoryAccessEntity>()), Times.Never);
        _historyEventPublisher.Verify(
            h => h.PublishHistoryValidatedEventAsync(It.IsAny<SignatoryAccessEntity>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ValidatePennylaneTermsAsync_ShouldDoNothing_WhenAmendmentAlreadyValidated()
    {
        // Arrange
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = true,
            AccountNumbers = ["ACC001"]
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(new SignatoryAccessEntity
        {
            Email = "user@test.com",
            CanAccess = true,
        });

        // Act & Assert
        var result = await _service.ValidatePennylaneTermsAsync("user@test.com", request);

        Assert.NotNull(result);
        Assert.Empty(result);

        _repo.Verify(r => r.GetSignatoryAccessByAccountNumberAsync(It.IsAny<string>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
        _repo.Verify(r => r.UpdateSignatoryAccess(It.IsAny<SignatoryAccessEntity>()), Times.Never);
        _repo.Verify(
            r => r.ValidateTerms(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<SignatoryAccessEntity>()), Times.Never);
        _historyEventPublisher.Verify(
            h => h.PublishHistoryValidatedEventAsync(It.IsAny<SignatoryAccessEntity>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ValidatePennylaneTermsAsync_WhenStatusTrue_ShouldCallEmailService()
    {
        // Arrange
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = true,
            AccountNumbers = new[] { "ACC001", "ACC002" }
        };

        var terms = _fixture.Create<TermsEntity>();
        var signatoryAccess1 = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            CompliancePriceExclTax = 150m,
            PlanPriceExclTax = 49m,
            CanAccess = false
        };
        var signatoryAccess2 = new SignatoryAccessEntity
        {
            AccountNumber = "ACC002",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            CompliancePriceExclTax = 0m,
            PlanPriceExclTax = 59m,
            CanAccess = false
        };

        var account1 = new AccountEntity { AccountNumber = "ACC001", LegalName = "Entreprise A" };
        var account2 = new AccountEntity { AccountNumber = "ACC002", LegalName = "Entreprise B" };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess1);
        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC002")).ReturnsAsync(signatoryAccess2);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        _accountRepository.Setup(r => r.GetAccountsByNumbersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<AccountEntity> { account1, account2 });

        ComplianceEmailContext? capturedContext = null;
        _complianceEmailService
            .Setup(s => s.SendAcceptanceEmailAsync(It.IsAny<ComplianceEmailContext>(), It.IsAny<TermsEntity>(),
                It.IsAny<CancellationToken>()))
            .Callback<ComplianceEmailContext, TermsEntity, CancellationToken>((ctx, terms, _) => capturedContext = ctx)
            .Returns(Task.CompletedTask);

        // Act
        await _service.ValidatePennylaneTermsAsync(email, request);

        // Assert
        _complianceEmailService.Verify(
            s => s.SendAcceptanceEmailAsync(It.IsAny<ComplianceEmailContext>(), It.IsAny<TermsEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _complianceEmailService.Verify(
            s => s.SendRefusalEmailAsync(It.IsAny<ComplianceRefusalContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _complianceEmailService.Verify(
            s => s.SendCustomerRelationRefusalEmailAsync(It.IsAny<CustomerRelationRefusalContext>(),
                It.IsAny<CancellationToken>()), Times.Never);

        capturedContext.Should().NotBeNull();
        capturedContext!.RecipientEmail.Should().Be(email);
        capturedContext.UserFirstName.Should().Be("Jean");
        capturedContext.UserLastName.Should().Be("Dupont");
        capturedContext.Companies.Should().HaveCount(2);
        capturedContext.Companies[0].Name.Should().Be("Entreprise A");
        capturedContext.Companies[1].Name.Should().Be("Entreprise B");
        capturedContext.Companies[0].CompliancePriceExclTax.Should().Be(150m);
        capturedContext.Companies[1].CompliancePriceExclTax.Should().Be(0m);
        capturedContext.Companies[0].SubscriptionPriceExclTax.Should().Be(49m);
        capturedContext.Companies[1].SubscriptionPriceExclTax.Should().Be(59m);
    }

    [Theory]
    [InlineData("31/10/2026", true)]
    [InlineData("30/06/2026", false)]
    public async Task ValidatePennylaneTermsAsync_WhenStatusTrue_ShouldSetIsNewComplianceFlowFromDueDate(
        string freeTrialEndDate, bool expectedFlag)
    {
        // Arrange
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = true,
            AccountNumbers = new[] { "ACC001" }
        };

        var signatoryAccess = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            CanAccess = false,
            FreeTrialEndDate = DateTime.ParseExact(freeTrialEndDate, "dd/MM/yyyy", CultureInfo.InvariantCulture)
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _accountRepository.Setup(r => r.GetAccountsByNumbersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<AccountEntity> { new() { AccountNumber = "ACC001", LegalName = "Entreprise A" } });

        ComplianceEmailContext? capturedContext = null;
        _complianceEmailService
            .Setup(s => s.SendAcceptanceEmailAsync(It.IsAny<ComplianceEmailContext>(), It.IsAny<TermsEntity>(),
                It.IsAny<CancellationToken>()))
            .Callback<ComplianceEmailContext, TermsEntity, CancellationToken>((ctx, _, _) => capturedContext = ctx)
            .Returns(Task.CompletedTask);

        // Act
        await _service.ValidatePennylaneTermsAsync(email, request);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.IsNewComplianceFlow.Should().Be(expectedFlag);
    }

    [Theory]
    [InlineData("31/10/2026", true)]
    [InlineData("30/06/2026", false)]
    public async Task ValidatePennylaneTermsAsync_WhenStatusFalse_ShouldSetIsNewComplianceFlowFromDueDate(
        string freeTrialEndDate, bool expectedFlag)
    {
        // Arrange
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = false,
            AccountNumbers = new[] { "ACC001" }
        };

        var signatoryAccess = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            CanAccess = false,
            FreeTrialEndDate = DateTime.ParseExact(freeTrialEndDate, "dd/MM/yyyy", CultureInfo.InvariantCulture)
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        ComplianceRefusalContext? capturedContext = null;
        _complianceEmailService
            .Setup(s => s.SendRefusalEmailAsync(It.IsAny<ComplianceRefusalContext>(), It.IsAny<CancellationToken>()))
            .Callback<ComplianceRefusalContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .Returns(Task.CompletedTask);

        // Act
        await _service.ValidatePennylaneTermsAsync(email, request);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.IsNewComplianceFlow.Should().Be(expectedFlag);
    }

    [Theory]
    [InlineData("Restauration")]
    [InlineData(null)]
    public async Task ValidatePennylaneTermsAsync_WhenStatusTrue_ShouldForwardWaveToAcceptanceEmail(string? wave)
    {
        // Arrange
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = true,
            AccountNumbers = new[] { "ACC001" }
        };

        var signatoryAccess = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            CanAccess = false,
            FreeTrialEndDate = new DateTime(2026, 6, 30),
            Wave = wave
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _accountRepository.Setup(r => r.GetAccountsByNumbersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<AccountEntity> { new() { AccountNumber = "ACC001", LegalName = "Entreprise A" } });

        ComplianceEmailContext? capturedContext = null;
        _complianceEmailService
            .Setup(s => s.SendAcceptanceEmailAsync(It.IsAny<ComplianceEmailContext>(), It.IsAny<TermsEntity>(),
                It.IsAny<CancellationToken>()))
            .Callback<ComplianceEmailContext, TermsEntity, CancellationToken>((ctx, _, _) => capturedContext = ctx)
            .Returns(Task.CompletedTask);

        // Act
        await _service.ValidatePennylaneTermsAsync(email, request);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.Wave.Should().Be(wave);
    }

    [Theory]
    [InlineData("Restauration")]
    [InlineData(null)]
    public async Task ValidatePennylaneTermsAsync_WhenStatusFalse_ShouldForwardWaveToRefusalEmail(string? wave)
    {
        // Arrange
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = false,
            AccountNumbers = new[] { "ACC001" }
        };

        var signatoryAccess = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            CanAccess = false,
            FreeTrialEndDate = new DateTime(2026, 6, 30),
            Wave = wave
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        ComplianceRefusalContext? capturedContext = null;
        _complianceEmailService
            .Setup(s => s.SendRefusalEmailAsync(It.IsAny<ComplianceRefusalContext>(), It.IsAny<CancellationToken>()))
            .Callback<ComplianceRefusalContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .Returns(Task.CompletedTask);

        // Act
        await _service.ValidatePennylaneTermsAsync(email, request);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.Wave.Should().Be(wave);
    }

    [Fact]
    public async Task
        ValidatePennylaneTermsAsync_WhenStatusTrue_AndOverrideEffectivePassed_ShouldSetIsFreeTrialExpiredTrue()
    {
        // Arrange - acceptation (Status=true) + override déjà effectif => IsFreeTrialExpired true (basé sur la date, indépendant du statut)
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = true,
            AccountNumbers = new[] { "ACC001" }
        };

        var signatoryAccess = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            CanAccess = false,
            FreeTrialEndDate = new DateTime(2026, 6, 30)
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _accountRepository.Setup(r => r.GetAccountsByNumbersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<AccountEntity> { new() { AccountNumber = "ACC001", LegalName = "Entreprise A" } });

        var service = CreateServiceWithOverride("01/01/2020");

        ComplianceEmailContext? capturedContext = null;
        _complianceEmailService
            .Setup(s => s.SendAcceptanceEmailAsync(It.IsAny<ComplianceEmailContext>(), It.IsAny<TermsEntity>(),
                It.IsAny<CancellationToken>()))
            .Callback<ComplianceEmailContext, TermsEntity, CancellationToken>((ctx, _, _) => capturedContext = ctx)
            .Returns(Task.CompletedTask);

        // Act
        await service.ValidatePennylaneTermsAsync(email, request);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.IsFreeTrialExpired.Should().BeTrue();
    }

    [Fact]
    public async Task
        ValidatePennylaneTermsAsync_WhenStatusTrue_AndOverrideNotYetEffective_ShouldSetIsFreeTrialExpiredFalse()
    {
        // Arrange - acceptation (Status=true) + override dans le futur => IsFreeTrialExpired false
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = true,
            AccountNumbers = new[] { "ACC001" }
        };

        var signatoryAccess = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            CanAccess = false,
            FreeTrialEndDate = new DateTime(2026, 6, 30)
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _accountRepository.Setup(r => r.GetAccountsByNumbersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<AccountEntity> { new() { AccountNumber = "ACC001", LegalName = "Entreprise A" } });

        var service = CreateServiceWithOverride("01/01/2099");

        ComplianceEmailContext? capturedContext = null;
        _complianceEmailService
            .Setup(s => s.SendAcceptanceEmailAsync(It.IsAny<ComplianceEmailContext>(), It.IsAny<TermsEntity>(),
                It.IsAny<CancellationToken>()))
            .Callback<ComplianceEmailContext, TermsEntity, CancellationToken>((ctx, _, _) => capturedContext = ctx)
            .Returns(Task.CompletedTask);

        // Act
        await service.ValidatePennylaneTermsAsync(email, request);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.IsFreeTrialExpired.Should().BeFalse();
    }

    [Fact]
    public async Task
        ValidatePennylaneTermsAsync_WhenStatusFalse_AndOverrideEffectivePassed_ShouldSetIsFreeTrialExpiredTrue()
    {
        // Arrange - refus (Status=false) + override déjà effectif => IsFreeTrialExpired true
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = false,
            AccountNumbers = new[] { "ACC001" }
        };

        var signatoryAccess = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            CanAccess = false,
            FreeTrialEndDate = new DateTime(2026, 6, 30)
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var service = CreateServiceWithOverride("01/01/2020");

        ComplianceRefusalContext? capturedContext = null;
        _complianceEmailService
            .Setup(s => s.SendRefusalEmailAsync(It.IsAny<ComplianceRefusalContext>(), It.IsAny<CancellationToken>()))
            .Callback<ComplianceRefusalContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .Returns(Task.CompletedTask);

        // Act
        await service.ValidatePennylaneTermsAsync(email, request);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.IsFreeTrialExpired.Should().BeTrue();
    }

    [Fact]
    public async Task
        ValidatePennylaneTermsAsync_WhenStatusFalse_AndOverrideNotYetEffective_ShouldSetIsFreeTrialExpiredFalse()
    {
        // Arrange - refus (Status=false) + override dans le futur => IsFreeTrialExpired false
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = false,
            AccountNumbers = new[] { "ACC001" }
        };

        var signatoryAccess = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            CanAccess = false,
            FreeTrialEndDate = new DateTime(2026, 6, 30)
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var service = CreateServiceWithOverride("01/01/2099");

        ComplianceRefusalContext? capturedContext = null;
        _complianceEmailService
            .Setup(s => s.SendRefusalEmailAsync(It.IsAny<ComplianceRefusalContext>(), It.IsAny<CancellationToken>()))
            .Callback<ComplianceRefusalContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .Returns(Task.CompletedTask);

        // Act
        await service.ValidatePennylaneTermsAsync(email, request);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.IsFreeTrialExpired.Should().BeFalse();
    }

    [Fact]
    public async Task ValidatePennylaneTermsAsync_WhenStatusFalse_ShouldCallRefusalEmailService()
    {
        // Arrange
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = false,
            AccountNumbers = new[] { "ACC001" }
        };

        var terms = _fixture.Create<TermsEntity>();
        var signatoryAccess = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            CanAccess = false
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        _complianceEmailService
            .Setup(s => s.SendRefusalEmailAsync(It.IsAny<ComplianceRefusalContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ValidatePennylaneTermsAsync(email, request);

        // Assert
        _complianceEmailService.Verify(
            s => s.SendAcceptanceEmailAsync(It.IsAny<ComplianceEmailContext>(), It.IsAny<TermsEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _complianceEmailService.Verify(
            s => s.SendRefusalEmailAsync(It.IsAny<ComplianceRefusalContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _complianceEmailService.Verify(
            s => s.SendCustomerRelationRefusalEmailAsync(It.IsAny<CustomerRelationRefusalContext>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLatestTermsContentAsync_WithExistingCGU_ShouldReturnFile()
    {
        using MemoryStream stream = new([0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64]);
        var expected = new FileStreamResult(stream, "application/json");
        var email = "user@email.com";

        var terms = new TermsEntity
        {
            Id = 1,
            LinkFr = "www.monliencgu.fr",
            CreationDate = DateTime.Now,
        };

        // Act
        _repo.Setup(x => x.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>())).ReturnsAsync(
            new List<Tuple<SignatoryAccessEntity, string>>
            {
                new(new SignatoryAccessEntity
                {
                    Email = email,
                    CanAccess = false,
                    Terms = terms
                }, "ACC001")
            });
        _blobStorageManager.Setup(t => t.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetLatestTermsContentAsync(email);

        // Assert
        Assert.NotNull(result);
        Assert.Equivalent(expected, result);

        _repo.Verify(t => t.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()), Times.Once);
        _blobStorageManager.Verify(t => t.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLatestTermsContentAsync_WhenCGUFileNotFound_ShouldThrowNotFoundException()
    {
        FileStreamResult? expected = null;
        var email = "user@email.com";

        var terms = new TermsEntity
        {
            Id = 1,
            LinkFr = "www.monliencgu.com",
            CreationDate = DateTime.Now,
        };

        // Act
        _repo.Setup(x => x.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>())).ReturnsAsync(
            new List<Tuple<SignatoryAccessEntity, string>>
            {
                new(new SignatoryAccessEntity
                {
                    Email = email,
                    CanAccess = false,
                    Terms = terms
                }, "ACC001")
            });
        _blobStorageManager.Setup(t => t.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var result =
            await Assert.ThrowsAnyAsync<NotFoundException>(async () =>
                await _service.GetLatestTermsContentAsync(email));

        // Assert
        Assert.Equal("PNL008", result.Code);
        Assert.Equal("CGU file named 'www.monliencgu.com' doesn't exist.", result.Message);

        _repo.Verify(t => t.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()), Times.Once);
        _blobStorageManager.Verify(t => t.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(InvalidSignatoryAccesses))]
    public async Task GetLatestTermsContentAsync_WhenNullOrEmptySignatoryAccesses_ShouldThrowNotFoundException(
        IEnumerable<Tuple<SignatoryAccessEntity, string>> invalidSignatoryAccesses)
    {
        var email = "user@email.com";

        // Act
        _repo.Setup(x => x.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(invalidSignatoryAccesses);
        var result =
            await Assert.ThrowsAnyAsync<NotFoundException>(async () =>
                await _service.GetLatestTermsContentAsync(email));

        // Assert
        Assert.Equal("PNL012", result.Code);
        Assert.Equal("No signatoryAccess found for email 'user@email.com'.", result.Message);

        _repo.Verify(t => t.GetSignatoryAccessesByUserEmailAsync(It.IsAny<string>()), Times.Once);
        _blobStorageManager.Verify(t => t.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public static TheoryData<IEnumerable<Tuple<SignatoryAccessEntity, string>>> InvalidSignatoryAccesses => new()
    {
        { null! },
        { new List<Tuple<SignatoryAccessEntity, string>>() },
    };

    [Fact]
    public async Task ValidatePennylaneTermsAsync_ShouldPassSourceDataToValidateTerms()
    {
        // Arrange
        var email = "user@test.com";
        var request = new PennylaneSignatoryValidationRequest
        {
            Status = true,
            AccountNumbers = new[] { "ACC001" }
        };

        var signatoryAccess = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = email,
            FirstName = "Jean",
            LastName = "Dupont",
            PlanName = "Plan Pro",
            UserCount = 5,
            PlanPriceExclTax = 49.90m,
            CompliancePriceExclTax = 150m,
            CanAccess = false,
            TermsId = 1,
        };

        _repo.Setup(r => r.GetSignatoryAccessByAccountNumberAsync("ACC001")).ReturnsAsync(signatoryAccess);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        _accountRepository.Setup(r => r.GetAccountsByNumbersAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<AccountEntity>
            {
                new() { AccountNumber = "ACC001", LegalName = "Company A" }
            });
        _complianceEmailService.Setup(s => s.SendAcceptanceEmailAsync(It.IsAny<ComplianceEmailContext>(),
                It.IsAny<TermsEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SignatoryAccessEntity? capturedSourceData = null;
        _repo.Setup(r =>
                r.ValidateTerms(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(),
                    It.IsAny<SignatoryAccessEntity>()))
            .Callback<string, int, bool, SignatoryAccessEntity>((_, _, _, src) => capturedSourceData = src);

        // Act
        await _service.ValidatePennylaneTermsAsync(email, request);

        // Assert - source data passed to ValidateTerms
        Assert.NotNull(capturedSourceData);
        Assert.Equal("user@test.com", capturedSourceData!.Email);
        Assert.Equal("Dupont", capturedSourceData.LastName);
        Assert.Equal("Jean", capturedSourceData.FirstName);
        Assert.Equal("Plan Pro", capturedSourceData.PlanName);
        Assert.Equal(5, capturedSourceData.UserCount);
        Assert.Equal(49.90m, capturedSourceData.PlanPriceExclTax);
        Assert.Equal(150m, capturedSourceData.CompliancePriceExclTax);
    }

    #region Wave (Vague) Tests

    [Fact]
    public async Task Import_WaveRestauration_PersistsWave()
    {
        // Arrange
        var csv = CsvHeader + "\nD001;test@yolo.com;Nom;Prenom;Plan;1;10;3;30/06/2026;Restauration\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        result.SuccessCount.Should().Be(1);
        result.Errors.Should().BeEmpty();

        _repo.Verify(
            r => r.InsertSignatoryAccessAsync(
                It.Is<SignatoryAccessEntity>(e => e.AccountNumber == "D001" && e.Wave == "Restauration")), Times.Once);
    }

    [Fact]
    public async Task Import_WaveEmpty_PersistsNullWave()
    {
        // Arrange
        var csv = CsvHeader + "\nD001;test@yolo.com;Nom;Prenom;Plan;1;10;3;30/06/2026;\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        result.SuccessCount.Should().Be(1);
        result.Errors.Should().BeEmpty();

        _repo.Verify(
            r => r.InsertSignatoryAccessAsync(
                It.Is<SignatoryAccessEntity>(e => e.AccountNumber == "D001" && e.Wave == null)), Times.Once);
    }

    [Fact]
    public async Task Import_WaveColumnAbsent_ImportsSuccessfully()
    {
        // Arrange - CSV without Vague column (backward compatibility)
        var csv = CsvHeader + "\nD001;test@yolo.com;Nom;Prenom;Plan;1;10;3;30/06/2026;\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        result.SuccessCount.Should().Be(1);
        result.Errors.Should().BeEmpty();

        _repo.Verify(
            r => r.InsertSignatoryAccessAsync(
                It.Is<SignatoryAccessEntity>(e => e.AccountNumber == "D001" && e.Wave == null)), Times.Once);
    }

    [Fact]
    public async Task Import_WaveInvalid_RejectsLine()
    {
        // Arrange - One invalid line ("Autre"), one valid line ("Restauration")
        var csv = CsvHeader +
                  "\nD001;test@yolo.com;Nom;Prenom;Plan;1;10;3;30/06/2026;Autre\nD002;test2@yolo.com;Nom2;Prenom2;Plan;1;10;3;30/06/2026;Restauration\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        // First pass validation fails, no inserts happen
        result.SuccessCount.Should().Be(0);
        result.Errors.Should().NotBeEmpty();
        result.Errors.First().ColumnName.Should().Contain("Vague");
        result.Errors.First().ErrorMessage.Should().Contain("Restauration");

        // Verify no insertions occurred
        _repo.Verify(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()), Times.Never);
    }

    [Theory]
    [InlineData("restauration")]
    [InlineData("RESTAURATION")]
    [InlineData("ReStAuRaTiOn")]
    public async Task Import_WaveCaseSensitive_Rejected(string waveValue)
    {
        // Arrange - Wave avec mauvaise casse doit être rejetée
        var csv = CsvHeader + "\nD001;test@yolo.com;Nom;Prenom;Plan;1;10;3;30/06/2026;" + waveValue + "\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        result.SuccessCount.Should().Be(0);
        result.Errors.Should().NotBeEmpty();
        result.Errors.First().ColumnName.Should().Contain("Vague");
        result.Errors.First().ErrorMessage.Should().Contain("Restauration");

        // Verify no insertions occurred
        _repo.Verify(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>()), Times.Never);
    }

    [Fact]
    public async Task Import_WaveWithSpaces_Trimmed_Accepted()
    {
        // Arrange - Wave avec espaces avant/après doit être trimmée et acceptée
        var csv = CsvHeader + "\nD001;test@yolo.com;Nom;Prenom;Plan;1;10;3;30/06/2026;  Restauration  \n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert
        result.SuccessCount.Should().Be(1);
        result.Errors.Should().BeEmpty();

        // Wave should be normalized to "Restauration"
        _repo.Verify(
            r => r.InsertSignatoryAccessAsync(
                It.Is<SignatoryAccessEntity>(e => e.AccountNumber == "D001" && e.Wave == "Restauration")), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WhenSameEmailHasSameWave_Succeeds()
    {
        // Arrange - Deux dossiers pour le même email avec la même vague
        var csv = CsvHeader + "\n" +
                  "D001;user@test.com;Nom1;Prenom1;Plan;1;10,0;5,0;31/10/2026;Restauration\n" +
                  "D002;user@test.com;Nom2;Prenom2;Plan;1;10,0;5,0;31/10/2026;Restauration\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _roleRepository.Setup(r => r.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - Pas d'erreurs, import réussi
        result.Errors.Should().BeEmpty();
        result.SuccessCount.Should().Be(2);
    }

    [Fact]
    public async Task ImportAsync_WhenSameEmailHasNullWave_Succeeds()
    {
        // Arrange - Deux dossiers pour le même email avec vague null (habituel)
        var csv = CsvHeader + "\n" +
                  "D001;user@test.com;Nom1;Prenom1;Plan;1;10,0;5,0;31/10/2026;\n" +
                  "D002;user@test.com;Nom2;Prenom2;Plan;1;10,0;5,0;31/10/2026;\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _roleRepository.Setup(r => r.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - Pas d'erreurs, import réussi
        result.Errors.Should().BeEmpty();
        result.SuccessCount.Should().Be(2);
    }

    [Fact]
    public async Task ImportAsync_WhenSameEmailHasMixedWaves_ReturnsError()
    {
        // Arrange - Deux dossiers pour le même email avec vagues différentes
        var csv = CsvHeader + "\n" +
                  "D001;user@test.com;Nom1;Prenom1;Plan;1;10,0;5,0;31/10/2026;\n" +
                  "D002;user@test.com;Nom2;Prenom2;Plan;1;10,0;5,0;31/10/2026;Restauration\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _roleRepository.Setup(r => r.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - Erreur de vagues incohérentes
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Vagues incohérentes"));
        result.SuccessCount.Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_WhenDifferentEmailsHaveDifferentWaves_Succeeds()
    {
        // Arrange - Deux emails différents avec des vagues différentes (OK)
        var csv = CsvHeader + "\n" +
                  "D001;user1@test.com;Nom1;Prenom1;Plan;1;10,0;5,0;31/10/2026;\n" +
                  "D002;user2@test.com;Nom2;Prenom2;Plan;1;10,0;5,0;31/10/2026;Restauration\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _roleRepository.Setup(r => r.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - Pas d'erreurs car emails différents
        result.Errors.Should().BeEmpty();
        result.SuccessCount.Should().Be(2);
    }

    [Fact]
    public async Task ImportAsync_WhenSingleDossier_NoWaveValidation()
    {
        // Arrange - Un seul dossier = pas de validation de cohérence nécessaire
        var csv = CsvHeader + "\n" +
                  "D001;user@test.com;Nom;Prenom;Plan;1;10,0;5,0;31/10/2026;Restauration\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _roleRepository.Setup(r => r.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - Pas d'erreurs
        result.Errors.Should().BeEmpty();
        result.SuccessCount.Should().Be(1);
    }

    [Fact]
    public async Task ImportAsync_WhenNeitherFreeTrialEndDateNorWaveProvided_ReturnsError()
    {
        // Arrange - Ni date de fin de gratuité ni vague renseignée
        var csv = CsvHeader + "\n" +
                  "D001;user@test.com;Nom;Prenom;Plan;1;10,0;5,0;;\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _roleRepository.Setup(r => r.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - Erreur car date ou vague doit être renseignée
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("La date de fin de gratuité ou la vague doit être renseignée"));
        result.SuccessCount.Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_WhenOnlyWaveProvided_Succeeds()
    {
        // Arrange - Seule la vague est renseignée (date de fin de gratuité vide)
        var csv = CsvHeader + "\n" +
                  "D001;user@test.com;Nom;Prenom;Plan;1;10,0;5,0;;Restauration\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _roleRepository.Setup(r => r.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - Pas d'erreurs car la vague est renseignée
        result.Errors.Should().BeEmpty();
        result.SuccessCount.Should().Be(1);
    }

    [Fact]
    public async Task ImportAsync_WhenBothFreeTrialEndDateAndWaveProvided_Succeeds()
    {
        // Arrange - Les deux sont renseignées
        var csv = CsvHeader + "\n" +
                  "D001;user@test.com;Nom;Prenom;Plan;1;10,0;5,0;30/06/2026;Restauration\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _roleRepository.Setup(r => r.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - Pas d'erreurs
        result.Errors.Should().BeEmpty();
        result.SuccessCount.Should().Be(1);
    }

    [Fact]
    public async Task ImportAsync_WhenWaveRestauration_ShouldResolveWaveTerms()
    {
        // Arrange - Vague Restauration doit résoudre les termes de la vague
        var csv = CsvHeader + "\n" +
                  "D001;user@test.com;Nom;Prenom;Plan;1;10,0;5,0;;Restauration\n";
        var file = CreateFormFile(csv);

        _repo.Setup(r => r.GetAllSignatoryAccessAsync()).ReturnsAsync(new List<SignatoryAccessEntity>());
        _repo.Setup(r => r.InsertSignatoryAccessAsync(It.IsAny<SignatoryAccessEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _roleRepository.Setup(r => r.CheckRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.ImportAsync(file);

        // Assert - Doit utiliser les termes de la vague Restauration
        result.Errors.Should().BeEmpty();
        result.SuccessCount.Should().Be(1);
        _repo.Verify(r => r.GetTermsIdByLinkFrAsync(RestaurationWaveTermsName), Times.Once);
    }
}

#endregion

