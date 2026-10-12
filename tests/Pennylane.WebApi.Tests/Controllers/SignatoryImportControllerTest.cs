using System.Text;
using AutoFixture;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.Signatory;
using Pennylane.Application.Requests;
using Pennylane.WebApi.Controllers;

namespace Pennylane.WebApi.Tests.Controllers;

public class SignatoryImportControllerTests
{
    private readonly Mock<ISignatoryAccessesImportService> _mockService;
    private readonly Mock<ILogger<SignatoryImportController>> _mockLogger;
    private readonly SignatoryImportController _controller;
    private readonly Fixture _fixture;

    public SignatoryImportControllerTests()
    {
        _mockService = new Mock<ISignatoryAccessesImportService>();
        _mockLogger = new Mock<ILogger<SignatoryImportController>>();
        _controller = new SignatoryImportController(_mockService.Object, _mockLogger.Object);
        _fixture = new Fixture();
    }
    
    [Fact]
    public async Task ImportSignatairesAsync_ReturnsBadRequest_WhenFileIsNull()
    {
        // Act
        var result = await _controller.ImportSignatoriesAsync(null!);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Le fichier est obligatoire.", badRequest.Value);
        _mockService.Verify(s => s.ImportAsync(It.IsAny<IFormFile>()), Times.Never);
        
        // Vérifier que le warning a été loggé
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tentative d'upload sans fichier")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportSignatairesAsync_ReturnsBadRequest_WhenFileIsEmpty()
    {
        // Arrange
        var emptyFile = new FormFile(Stream.Null, 0, 0, "file", "empty.csv");

        // Act
        var result = await _controller.ImportSignatoriesAsync(emptyFile);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Le fichier est obligatoire.", badRequest.Value);
        _mockService.Verify(s => s.ImportAsync(It.IsAny<IFormFile>()), Times.Never);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenValidFileWithSuccessfulImport_ShouldReturnOk()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n" +
                         "ACC001;john@test.com;Doe;John;Premium;5;100;120;50;60";
        var file = CreateMockFormFile(csvContent, csvContent.Length);

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 1,
            SuccessCount = 1,
            Errors = new List<SignatoryAccessImportErrorDto>()
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportSignatoriesAsync(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedResult = Assert.IsType<SignatoryAccessImportResponse>(okResult.Value);
        Assert.Equal(1, returnedResult.TotalRows);
        Assert.Equal(1, returnedResult.SuccessCount);
        Assert.Equal(0, returnedResult.ErrorCount);
        
        // Vérifier que l'information a été loggée
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Réception fichier")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenImportHasErrors_ShouldReturnOkWithErrors()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n" +
                         "ACC001;;Doe;John;Premium;5;100;120;50;60";
        var file = CreateMockFormFile(csvContent, csvContent.Length);

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 1,
            SuccessCount = 0,
            Errors = new List<SignatoryAccessImportErrorDto>
            {
                new SignatoryAccessImportErrorDto
                {
                    RowNumber = 2,
                    ErrorMessage = "Email requis pour un nouveau dossier",
                    RowData = new SignatoryAccessImportRow
                    {
                        AccountNumber = "ACC001"
                    }
                }
            }
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportSignatoriesAsync(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedResult = Assert.IsType<SignatoryAccessImportResponse>(okResult.Value);
        Assert.Equal(1, returnedResult.TotalRows);
        Assert.Equal(0, returnedResult.SuccessCount);
        Assert.Equal(1, returnedResult.ErrorCount);
        Assert.Single(returnedResult.Errors);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenPartialSuccess_ShouldReturnOkWithMixedResults()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n" +
                         "ACC001;john@test.com;Doe;John;Premium;5;100;120;50;60\n" +
                         "ACC002;;Smith;Jane;Basic;3;80;96;40;48";
        var file = CreateMockFormFile(csvContent, csvContent.Length);

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 2,
            SuccessCount = 1,
            Errors = new List<SignatoryAccessImportErrorDto>
            {
                new SignatoryAccessImportErrorDto
                {
                    RowNumber = 3,
                    ErrorMessage = "Email requis pour un nouveau dossier",
                    RowData = new SignatoryAccessImportRow
                    {
                        AccountNumber = "ACC002"
                    }
                }
            }
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportSignatoriesAsync(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedResult = Assert.IsType<SignatoryAccessImportResponse>(okResult.Value);
        Assert.Equal(2, returnedResult.TotalRows);
        Assert.Equal(1, returnedResult.SuccessCount);
        Assert.Equal(1, returnedResult.ErrorCount);
        Assert.Single(returnedResult.Errors);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenFileExtensionIsCsvUppercase_ShouldAccept()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n" +
                         "ACC001;john@test.com;Doe;John;Premium;5;100;120;50;60";
        var file = CreateMockFormFile(csvContent, csvContent.Length, "test.CSV");

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 1,
            SuccessCount = 1,
            Errors = new List<SignatoryAccessImportErrorDto>()
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportSignatoriesAsync(file);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenLargeFile_ShouldProcessSuccessfully()
    {
        // Arrange
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité");

        for (int i = 1; i <= 1000; i++)
        {
            csvBuilder.AppendLine($"ACC{i:D6};user{i}@test.com;Test{i};User{i};Premium;5;100;120;50;60");
        }

        var csvContent = csvBuilder.ToString();
        var file = CreateMockFormFile(csvContent, csvContent.Length);

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 1000,
            SuccessCount = 1000,
            Errors = new List<SignatoryAccessImportErrorDto>()
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportSignatoriesAsync(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedResult = Assert.IsType<SignatoryAccessImportResponse>(okResult.Value);
        Assert.Equal(1000, returnedResult.TotalRows);
        Assert.Equal(1000, returnedResult.SuccessCount);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenValidFile_ShouldCallServiceOnce()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n" +
                         "ACC001;john@test.com;Doe;John;Premium;5;100;120;50;60";
        var file = CreateMockFormFile(csvContent, csvContent.Length);

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 1,
            SuccessCount = 1,
            Errors = new List<SignatoryAccessImportErrorDto>()
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        await _controller.ImportSignatoriesAsync(file);

        // Assert
        _mockService.Verify(s => s.ImportAsync(It.IsAny<IFormFile>()), Times.Once);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenServiceThrowsException_ShouldPropagateException()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n" +
                         "ACC001;john@test.com;Doe;John;Premium;5;100;120;50;60";
        var file = CreateMockFormFile(csvContent, csvContent.Length);

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ThrowsAsync(new Exception("Database connection error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(
            () => _controller.ImportSignatoriesAsync(file));
    }

     [Fact]
    public async Task ImportSignatairesAsync_WhenInvalidHeaderFormat_ShouldReturnErrorsInResult()
    {
        // Arrange
        var csvContent = "Invalid;Header;Format\n" +
                         "ACC001;john@test.com;Doe";
        var file = CreateMockFormFile(csvContent, csvContent.Length);

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 0,
            SuccessCount = 0,
            Errors = new List<SignatoryAccessImportErrorDto>
            {
                new SignatoryAccessImportErrorDto
                {
                    RowNumber = 0,
                    ErrorMessage = "Format CSV invalide: Colonne 1 invalide. Attendu: 'Code dossier', Reçu: 'Invalid'",
                    RowData = null
                }
            }
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportSignatoriesAsync(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedResult = Assert.IsType<SignatoryAccessImportResponse>(okResult.Value);
        Assert.Equal(0, returnedResult.TotalRows);
        Assert.Equal(0, returnedResult.SuccessCount);
        Assert.Equal(1, returnedResult.ErrorCount);
        Assert.Single(returnedResult.Errors);
        Assert.Contains("Format CSV invalide", returnedResult.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenAllRowsFail_ShouldReturnAllErrors()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n" +
                         "ACC001;;Doe;John;Premium;5;100;120;50;60\n" +
                         "ACC002;;Smith;Jane;Basic;3;80;96;40;48\n" +
                         "ACC003;;Brown;Bob;Standard;2;70;84;30;36";
        var file = CreateMockFormFile(csvContent, csvContent.Length);

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 3,
            SuccessCount = 0,
            Errors = new List<SignatoryAccessImportErrorDto>
            {
                new SignatoryAccessImportErrorDto
                {
                    RowNumber = 2,
                    ErrorMessage = "Email requis pour un nouveau dossier",
                    RowData = new SignatoryAccessImportRow { AccountNumber = "ACC001" }
                },
                new SignatoryAccessImportErrorDto
                {
                    RowNumber = 3,
                    ErrorMessage = "Email requis pour un nouveau dossier",
                    RowData = new SignatoryAccessImportRow { AccountNumber = "ACC002" }
                },
                new SignatoryAccessImportErrorDto
                {
                    RowNumber = 4,
                    ErrorMessage = "Email requis pour un nouveau dossier",
                    RowData = new SignatoryAccessImportRow { AccountNumber = "ACC003" }
                }
            }
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportSignatoriesAsync(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedResult = Assert.IsType<SignatoryAccessImportResponse>(okResult.Value);
        Assert.Equal(3, returnedResult.TotalRows);
        Assert.Equal(0, returnedResult.SuccessCount);
        Assert.Equal(3, returnedResult.ErrorCount);
        Assert.Equal(3, returnedResult.Errors.Count);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenFileNameIsValid_ShouldLogFileName()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n" +
                         "ACC001;john@test.com;Doe;John;Premium;5;100;120;50;60";
        var file = CreateMockFormFile(csvContent, csvContent.Length, "import_signataires_2025.csv");

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 1,
            SuccessCount = 1,
            Errors = new List<SignatoryAccessImportErrorDto>()
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        await _controller.ImportSignatoriesAsync(file);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("import_signataires_2025.csv")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenFileSizeIsLarge_ShouldLogSize()
    {
        // Arrange
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité");
        
        for (int i = 1; i <= 5000; i++)
        {
            csvBuilder.AppendLine($"ACC{i:D6};user{i}@test.com;Test{i};User{i};Premium;5;100;120;50;60");
        }
        
        var csvContent = csvBuilder.ToString();
        var file = CreateMockFormFile(csvContent, csvContent.Length, "large_import.csv");

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 5000,
            SuccessCount = 5000,
            Errors = new List<SignatoryAccessImportErrorDto>()
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        await _controller.ImportSignatoriesAsync(file);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("bytes")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenDuplicateAccountNumbers_ShouldReturnErrors()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n" +
                         "ACC001;john@test.com;Doe;John;Premium;5;100;120;50;60\n" +
                         "ACC001;jane@test.com;Smith;Jane;Basic;3;80;96;40;48";
        var file = CreateMockFormFile(csvContent, csvContent.Length);

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 2,
            SuccessCount = 1,
            Errors = new List<SignatoryAccessImportErrorDto>
            {
                new SignatoryAccessImportErrorDto
                {
                    RowNumber = 3,
                    ErrorMessage = "Code dossier dupliqué dans le fichier. Un seul enregistrement par dossier est autorisé.",
                    RowData = new SignatoryAccessImportRow { AccountNumber = "ACC001" }
                }
            }
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportSignatoriesAsync(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedResult = Assert.IsType<SignatoryAccessImportResponse>(okResult.Value);
        Assert.Equal(2, returnedResult.TotalRows);
        Assert.Equal(1, returnedResult.SuccessCount);
        Assert.Equal(1, returnedResult.ErrorCount);
        Assert.Contains("dupliqué", returnedResult.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenNegativePrices_ShouldReturnValidationErrors()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n" +
                         "ACC001;john@test.com;Doe;John;Premium;5;-100;120;50;60";
        var file = CreateMockFormFile(csvContent, csvContent.Length);

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 1,
            SuccessCount = 0,
            Errors = new List<SignatoryAccessImportErrorDto>
            {
                new SignatoryAccessImportErrorDto
                {
                    RowNumber = 2,
                    ErrorMessage = "Le prix HT du plan ne peut pas être négatif.",
                    RowData = new SignatoryAccessImportRow { AccountNumber = "ACC001" }
                }
            }
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportSignatoriesAsync(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedResult = Assert.IsType<SignatoryAccessImportResponse>(okResult.Value);
        Assert.Equal(1, returnedResult.ErrorCount);
        Assert.Contains("négatif", returnedResult.Errors[0].ErrorMessage);
    }

    [Theory]
    [InlineData("test.csv", "text/csv")]
    [InlineData("import.CSV", "text/csv")]
    [InlineData("data.csv", "application/csv")]
    public async Task ImportSignatairesAsync_WithVariousFileFormats_ShouldAcceptAll(
        string fileName, 
        string contentType)
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n" +
                         "ACC001;john@test.com;Doe;John;Premium;5;100;120;50;60";
        var file = CreateMockFormFile(csvContent, csvContent.Length, fileName, contentType);

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 1,
            SuccessCount = 1,
            Errors = new List<SignatoryAccessImportErrorDto>()
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportSignatoriesAsync(file);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ImportSignatairesAsync_WhenResultIsEmpty_ShouldReturnOkWithZeroCounts()
    {
        // Arrange
        var csvContent = "Code dossier;Email signataire;Nom signataire;Prénom signataire;Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;Prix HT du plan Pennylane;Prix TTC du plan Pennylane;Prix HT de la mise en conformité;Prix TTC de la mise en conformité\n";
        var file = CreateMockFormFile(csvContent, csvContent.Length);

        var importResult = new SignatoryAccessImportResponse
        {
            TotalRows = 0,
            SuccessCount = 0,
            Errors = new List<SignatoryAccessImportErrorDto>()
        };

        _mockService.Setup(s => s.ImportAsync(It.IsAny<IFormFile>()))
            .ReturnsAsync(importResult);

        // Act
        var result = await _controller.ImportSignatoriesAsync(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedResult = Assert.IsType<SignatoryAccessImportResponse>(okResult.Value);
        Assert.Equal(0, returnedResult.TotalRows);
        Assert.Equal(0, returnedResult.SuccessCount);
        Assert.Equal(0, returnedResult.ErrorCount);
    }

    /// <summary>
    /// Helper method pour créer un IFormFile mocké
    /// </summary>
    private static IFormFile CreateMockFormFile(
        string content,
        long length,
        string fileName = "test.csv",
        string contentType = "text/csv")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.Length).Returns(length);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns(contentType);

        return fileMock.Object;
    }

    [Fact]
    public async Task CheckPennylaneAccessAsync_Nominal()
    {
        _mockService.Setup(x => x.CheckPennylaneAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(new SignatoryAccess());

        var result = await _controller.CheckPennylaneAccessAsync("userEmail", "userType", 1);

        Assert.NotNull(result);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);

        _mockService.Verify(x => x.CheckPennylaneAccessAsync("userEmail", "userType", 1), Times.Once);
    }

    [Fact]
    public async Task CheckPennylaneAccessAsync_WithNullAccountId_Nominal()
    {
        _mockService.Setup(x => x.CheckPennylaneAccessAsync(It.IsAny<string>(), It.IsAny<string>(), null)).ReturnsAsync(new SignatoryAccess());

        var result = await _controller.CheckPennylaneAccessAsync("userEmail", "userType");

        Assert.NotNull(result);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);

        _mockService.Verify(x => x.CheckPennylaneAccessAsync("userEmail", "userType", null), Times.Once);
    }

    [Fact]
    public async Task ValidatePennylaneTermsAsync_Nominal()
    {
        var history = _fixture.CreateMany<SignatoryValidationHistory>(3);
        _mockService.Setup(x => x.ValidatePennylaneTermsAsync(It.IsAny<string>(), It.IsAny<PennylaneSignatoryValidationRequest>())).ReturnsAsync(history);

        var result = await _controller.ValidatePennylaneTermsAsync("userEmail", It.IsAny<PennylaneSignatoryValidationRequest>());

        Assert.NotNull(result);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsAssignableFrom<IEnumerable<SignatoryValidationHistory>>(okResult.Value);

        Assert.Equal(history, value);

        _mockService.Verify(x => x.ValidatePennylaneTermsAsync(It.IsAny<string>(), It.IsAny<PennylaneSignatoryValidationRequest>()), Times.Once);
    }

    [Fact]
    public async Task GetLastestTermsContentAsync_Nominal()
    {
        using MemoryStream stream = new([0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64]);
        var expected = new FileStreamResult(stream, "application/json");

        // Act
        _mockService.Setup(t => t.GetLatestTermsContentAsync(It.IsAny<string>())).ReturnsAsync(expected);

        var actionResult = await _controller.GetLastestTermsContentAsync("user@email.com");

        // Assert
        var result = actionResult as FileStreamResult;
        
        Assert.NotNull(result);
        Assert.Equal(result!.FileStream, expected.FileStream);

        _mockService.Verify(x => x.GetLatestTermsContentAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CheckPennylaneAccessAsync_DossierRestauration_WaveRestauration()
    {
        // Arrange
        var signatoryAccess = new SignatoryAccess
        {
            CanAccess = true,
            Wave = "Restauration"
        };
        _mockService.Setup(x => x.CheckPennylaneAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(signatoryAccess);

        // Act
        var result = await _controller.CheckPennylaneAccessAsync("user@test.com", "contact", 1);

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedAccess = Assert.IsType<SignatoryAccess>(okResult.Value);
        
        Assert.Equal("Restauration", returnedAccess.Wave);
        _mockService.Verify(x => x.CheckPennylaneAccessAsync("user@test.com", "contact", 1), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task CheckPennylaneAccessAsync_DossierHabituel_WaveNull(bool? canAccess)
    {
        // Arrange
        var signatoryAccess = new SignatoryAccess
        {
            CanAccess = canAccess ?? false,
            Wave = null
        };
        _mockService.Setup(x => x.CheckPennylaneAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(signatoryAccess);

        // Act
        var result = await _controller.CheckPennylaneAccessAsync("user@test.com", "contact", 2);

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedAccess = Assert.IsType<SignatoryAccess>(okResult.Value);
        
        Assert.Null(returnedAccess.Wave);
        _mockService.Verify(x => x.CheckPennylaneAccessAsync("user@test.com", "contact", 2), Times.Once);
    }
}
