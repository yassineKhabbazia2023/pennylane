using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models;
using Pennylane.Application.Services;
using Pennylane.Application.StaticValues;
using Pennylane.Domain;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.Application.Tests.Services
{
    public class DocumentServiceTests
    {
        private readonly Mock<IDocumentRepository> _repositoryMock;
        private readonly Fixture _fixture;
        private readonly DocumentService _sut;
        private readonly Mock<IPennylaneApiFirmService> _pennylaneApiFirmServiceMock;
        private readonly Mock<IPennylaneApiGroupService> _pennylaneApiGroupServiceMock;
        private readonly Mock<IDocumentStreamService> _mockDocumentStreamService;
        private readonly Mock<IPennylaneService> _mockPennylaneService;
        private readonly IOptions<DocumentFilteringOptions> _documentFilteringOptions;
        private readonly HttpClient _httpClient;

        public DocumentServiceTests()
        {
            _fixture = new Fixture();
            // Prevent cycles in our fixture graph
            _fixture.Behaviors
                .OfType<ThrowingRecursionBehavior>()
                .ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            _repositoryMock = new Mock<IDocumentRepository>(MockBehavior.Strict);
            _pennylaneApiFirmServiceMock = new Mock<IPennylaneApiFirmService>(MockBehavior.Strict);
            _pennylaneApiGroupServiceMock = new Mock<IPennylaneApiGroupService>(MockBehavior.Strict);
            _mockDocumentStreamService = new Mock<IDocumentStreamService>(MockBehavior.Strict);
            _mockPennylaneService = new Mock<IPennylaneService>(MockBehavior.Strict);
            _documentFilteringOptions = Options.Create(new DocumentFilteringOptions
            {
                ExcludedExtensions = new List<string> { ".EDI" }
            });
            _httpClient = new HttpClient();
            _sut = new DocumentService(
                _repositoryMock.Object,
                _pennylaneApiFirmServiceMock.Object,
                _pennylaneApiGroupServiceMock.Object,
                _mockDocumentStreamService.Object,
                _mockPennylaneService.Object,
                _documentFilteringOptions
            );
        }

        [Fact]
        public async Task GetDocumentsAsync_WhenNoDocumentsExists_ReturnsEmptyPagination()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var pageRank = 1;
            var pageSize = 1;
            _repositoryMock.Setup(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, null, It.IsAny<IReadOnlyCollection<string>>()))
                .ReturnsAsync(new AccountDocumentsPage(pageRank, pageSize, 0, [], null));

            // Act
            var result = await _sut.GetDocumentsAsync(accountNumber, pageRank, pageSize);

            // Assert
            result.Should().BeEquivalentTo(new AccountDocumentsPage(pageRank, pageSize, 0, [], null));
            _repositoryMock.Verify(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, null, It.IsAny<IReadOnlyCollection<string>>()), Times.Once);
        }

        [Fact]
        public async Task GetDocumentsAsync_WhenDocumentsExists_ReturnsPagination()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var documentsCount = 200;
            var documents = _fixture.CreateMany<Document>(documentsCount).ToList();
            var pageSize = 100;
            var expectedDocuments = documents.OrderByDescending(d => d.UpdatedAt).Take(pageSize).ToList();
            var pageRank = 1;
            _repositoryMock.Setup(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, null, It.IsAny<IReadOnlyCollection<string>>()))
                .ReturnsAsync(new AccountDocumentsPage(pageRank, pageSize, documentsCount, expectedDocuments, null));

            // Act
            var result = await _sut.GetDocumentsAsync(accountNumber, pageRank, pageSize);

            // Assert
            result.Should().BeEquivalentTo(new AccountDocumentsPage(pageRank, pageSize, documentsCount, expectedDocuments, null));
            _repositoryMock.Verify(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, null, It.IsAny<IReadOnlyCollection<string>>()), Times.Once);
        }

        [Fact]
        public async Task GetDocumentsAsync_WithSearch_ShouldPassSearchToRepository()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var search = "facture";
            var pageRank = 1;
            var pageSize = 10;
            var documents = _fixture.CreateMany<Document>(3).ToList();

            _repositoryMock
                .Setup(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, search, It.IsAny<IReadOnlyCollection<string>>()))
                .ReturnsAsync(new AccountDocumentsPage(pageRank, pageSize, 3, documents, null));

            // Act
            var result = await _sut.GetDocumentsAsync(accountNumber, pageRank, pageSize, search);

            // Assert
            result.Documents.Should().HaveCount(3);
            _repositoryMock.Verify(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, search, It.IsAny<IReadOnlyCollection<string>>()), Times.Once);
        }

        [Fact]
        public async Task GetDocumentsAsync_WithNullSearch_ShouldPassNullToRepository()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var pageRank = 1;
            var pageSize = 10;

            _repositoryMock
                .Setup(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, null, It.IsAny<IReadOnlyCollection<string>>()))
                .ReturnsAsync(new AccountDocumentsPage(pageRank, pageSize, 0, [], null));

            // Act
            var result = await _sut.GetDocumentsAsync(accountNumber, pageRank, pageSize, null);

            // Assert
            result.Documents.Should().BeEmpty();
            _repositoryMock.Verify(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, null, It.IsAny<IReadOnlyCollection<string>>()), Times.Once);
        }

        [Fact]
        public async Task InsertPennylaneDocumentsAsync_InsertsOnlyNonExistingDocuments()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var dmsFiles = _fixture.Build<PennylaneDmsFile>()
                .With(f => f.Id, 1)
                .With(f => f.Name, "doc1")
                .CreateMany(1)
                .ToList();
            dmsFiles.Add(_fixture.Build<PennylaneDmsFile>()
                .With(f => f.Id, 2)
                .With(f => f.Name, "doc2")
                .Create());
            var expectedEntities = dmsFiles.Select(f => new DocumentEntity
            {
                Id = f.Id,
                Name = f.Name,
                Path = f.Path,
                Url = f.Url,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            }).ToList();
            _repositoryMock.Setup(r => r.InsertPennylaneDocumentsAsync(accountNumber, It.Is<List<DocumentEntity>>(l => l.Count == 2))).ReturnsAsync(2).Verifiable();

            // Act
            await _sut.InsertPennylaneDocumentsAsync(accountNumber, dmsFiles);

            // Assert
            _repositoryMock.Verify();
        }

        [Fact]
        public async Task GetDocumentsAsync_ShouldPassConfiguredExcludedExtensions_ToRepository()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var pageRank = 1;
            var pageSize = 10;
            _repositoryMock
                .Setup(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, null,
                    It.Is<IReadOnlyCollection<string>>(e => e.Contains(".EDI"))))
                .ReturnsAsync(new AccountDocumentsPage(pageRank, pageSize, 0, [], null));

            // Act
            await _sut.GetDocumentsAsync(accountNumber, pageRank, pageSize);

            // Assert
            _repositoryMock.Verify(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, null,
                It.Is<IReadOnlyCollection<string>>(e => e.Contains(".EDI"))), Times.Once);
        }

        [Fact]
        public async Task GetDocumentsAsync_WithApplyExtensionFilterFalse_ShouldNotPassExclusionsToRepository()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var pageRank = 1;
            var pageSize = 10;
            _repositoryMock
                .Setup(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, null, null))
                .ReturnsAsync(new AccountDocumentsPage(pageRank, pageSize, 0, [], null));

            // Act
            await _sut.GetDocumentsAsync(accountNumber, pageRank, pageSize, search: null, applyExtensionFilter: false);

            // Assert
            _repositoryMock.Verify(r => r.GetDocumentsAsync(accountNumber, pageRank, pageSize, null, null), Times.Once);
        }

        [Fact]
        public async Task InsertPennylaneDocumentsAsync_ShouldExcludeFilesWithExcludedExtension()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var keptFile = _fixture.Build<PennylaneDmsFile>()
                .With(f => f.Id, 1)
                .With(f => f.Name, "bilan.pdf")
                .Create();
            var ediFile = _fixture.Build<PennylaneDmsFile>()
                .With(f => f.Id, 2)
                .With(f => f.Name, "liasse_fiscale.EDI")
                .Create();
            var dmsFiles = new List<PennylaneDmsFile> { keptFile, ediFile };

            _repositoryMock
                .Setup(r => r.InsertPennylaneDocumentsAsync(accountNumber,
                    It.Is<List<DocumentEntity>>(l => l.Count == 1 && l.All(d => d.Name == "bilan.pdf"))))
                .ReturnsAsync(1)
                .Verifiable();

            // Act
            var inserted = await _sut.InsertPennylaneDocumentsAsync(accountNumber, dmsFiles);

            // Assert
            inserted.Should().Be(1);
            _repositoryMock.Verify();
        }

        [Theory]
        [InlineData("liasse_fiscale.edi")]
        [InlineData("liasse_fiscale.Edi")]
        [InlineData("liasse_fiscale.EDI")]
        public async Task InsertPennylaneDocumentsAsync_ShouldExcludeExtension_CaseInsensitive(string ediFileName)
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var ediFile = _fixture.Build<PennylaneDmsFile>()
                .With(f => f.Id, 1)
                .With(f => f.Name, ediFileName)
                .Create();
            var dmsFiles = new List<PennylaneDmsFile> { ediFile };

            _repositoryMock
                .Setup(r => r.InsertPennylaneDocumentsAsync(accountNumber,
                    It.Is<List<DocumentEntity>>(l => l.Count == 0)))
                .ReturnsAsync(0)
                .Verifiable();

            // Act
            var inserted = await _sut.InsertPennylaneDocumentsAsync(accountNumber, dmsFiles);

            // Assert
            inserted.Should().Be(0);
            _repositoryMock.Verify();
        }

        [Fact]
        public async Task GetDocumentContentStreamAsync_WhenDocumentDoesNotExist_ReturnsEmptyStream()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var documentId = 1;

            _repositoryMock.Setup(r => r.GetDocumentByIdAsync(accountNumber, documentId))
                .ReturnsAsync((Document)null);

            // Act
            var result = await _sut.GetDocumentContentStreamAsync(accountNumber, documentId);

            // Assert
            Assert.Equal(Stream.Null, result);
            result.Length.Should().Be(0);
        }

        [Fact]
        public async Task GetDocumentContentStreamAsync_WhenCompanyDoesNotExist_ReturnsEmptyStream()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var documentId = 1;
            var document = _fixture.Create<Document>();

            _repositoryMock.Setup(r => r.GetDocumentByIdAsync(accountNumber, documentId))
                .ReturnsAsync(document);

            _pennylaneApiGroupServiceMock.Setup(s => s.TryGetCompanyByIdAsync(accountNumber))
                .ReturnsAsync((PennylaneCompany)null);

            // Act
            var result = await _sut.GetDocumentContentStreamAsync(accountNumber, documentId);

            // Assert
            Assert.Equal(Stream.Null, result);
            result.Length.Should().Be(0);
        }

        [Fact]
        public async Task GetDocumentContentStreamAsync_WhenPennylaneCompanyInternalIdDoesNotExist_ReturnsEmptyStream()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var documentId = 1;
            var document = _fixture.Create<Document>();
            var company = _fixture.Create<PennylaneCompany>();

            _repositoryMock.Setup(r => r.GetDocumentByIdAsync(accountNumber, documentId))
                .ReturnsAsync(document);

            _pennylaneApiGroupServiceMock.Setup(s => s.TryGetCompanyByIdAsync(accountNumber))
                .ReturnsAsync(company);

            _mockPennylaneService.Setup(s => s.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                .ReturnsAsync((int?)null);

            // Act
            var result = await _sut.GetDocumentContentStreamAsync(accountNumber, documentId);

            // Assert
            Assert.Equal(Stream.Null, result);
            result.Length.Should().Be(0);
        }

        [Fact]
        public async Task GetDocumentContentStreamAsync_WhenDmsFilesDoNotExist_ReturnsEmptyStream()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var documentId = 1;
            var document = _fixture.Create<Document>();
            var company = _fixture.Create<PennylaneCompany>();
            var pennylaneCompanyInternalId = _fixture.Create<int>();

            _repositoryMock.Setup(r => r.GetDocumentByIdAsync(accountNumber, documentId))
                .ReturnsAsync(document);

            _pennylaneApiGroupServiceMock.Setup(s => s.TryGetCompanyByIdAsync(accountNumber))
                .ReturnsAsync(company);

            _mockPennylaneService.Setup(s => s.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                .ReturnsAsync(pennylaneCompanyInternalId);

            _pennylaneApiFirmServiceMock.Setup(s => s.GetCompanyDmsFilesAsync(company.FirmId, pennylaneCompanyInternalId, documentId.ToString()))
                .ReturnsAsync(new List<PennylaneDmsFile>());

            // Act
            var result = await _sut.GetDocumentContentStreamAsync(accountNumber, documentId);

            // Assert
            Assert.Equal(Stream.Null, result);
            result.Length.Should().Be(0);
        }

        [Fact]
        public async Task GetDocumentContentStreamAsync_WhenFileExists_ReturnsStream()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var documentId = 1;
            var document = _fixture.Create<Document>();
            var company = _fixture.Create<PennylaneCompany>();
            var pennylaneCompanyInternalId = _fixture.Create<int>();
            var dmsFiles = new List<PennylaneDmsFile>
            {
                new PennylaneDmsFile { Id = 1, Url = "http://example.com/file1" }
            };
            byte[] expectedBytes = { 1, 2, 3, 4 };
            var expectedStream = new MemoryStream(expectedBytes);

            // Configuration des mocks
            _repositoryMock.Setup(r => r.GetDocumentByIdAsync(accountNumber, documentId))
                .ReturnsAsync(document);

            _pennylaneApiGroupServiceMock.Setup(s => s.TryGetCompanyByIdAsync(accountNumber))
                .ReturnsAsync(company);

            _mockPennylaneService.Setup(s => s.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                .ReturnsAsync(pennylaneCompanyInternalId);

            _pennylaneApiFirmServiceMock.Setup(s => s.GetCompanyDmsFilesAsync(company.FirmId, pennylaneCompanyInternalId, documentId.ToString()))
                .ReturnsAsync(dmsFiles);

            // Configuration du mock pour DocumentStreamService
            _mockDocumentStreamService.Setup(s => s.GetStreamFromUrlAsync(dmsFiles.First().Url))
                .ReturnsAsync(expectedStream);

            // Assurez-vous que DocumentService utilise le mock de IDocumentStreamService
            var sut = new DocumentService(
                _repositoryMock.Object,
                _pennylaneApiFirmServiceMock.Object,
                _pennylaneApiGroupServiceMock.Object,
                _mockDocumentStreamService.Object, // Injection du mock
                _mockPennylaneService.Object,
                _documentFilteringOptions
            );

            // Act
            var resultStream = await sut.GetDocumentContentStreamAsync(accountNumber, documentId);
            var memoryStream = new MemoryStream();
            await resultStream.CopyToAsync(memoryStream);

            // Assert
            Assert.Equal(expectedBytes, memoryStream.ToArray());
        }

        [Fact]
        public async Task GetAllDocumentsByPathAsync_WhenRepositoryReturnsDocuments_ReturnsSameList()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var path = Constant.PermanentPublications;
            var expected = _fixture.CreateMany<Document>(5).ToList().AsReadOnly();

            _repositoryMock
                .Setup(r => r.GetAllDocumentsByPathAsync(accountNumber, path))
                .ReturnsAsync(expected);

            // Act
            var result = await _sut.GetAllDocumentsByPathAsync(accountNumber, path);

            // Assert
            result.Should().BeEquivalentTo(expected);
            _repositoryMock.Verify(r => r.GetAllDocumentsByPathAsync(accountNumber, path), Times.Once);
        }

        [Fact]
        public async Task GetAllDocumentsByPathAsync_WhenRepositoryReturnsEmpty_ReturnsEmpty()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var path = Constant.PermanentPublications;

            _repositoryMock
                .Setup(r => r.GetAllDocumentsByPathAsync(accountNumber, path))
                .ReturnsAsync(Array.Empty<Document>());

            // Act
            var result = await _sut.GetAllDocumentsByPathAsync(accountNumber, path);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _repositoryMock.Verify(r => r.GetAllDocumentsByPathAsync(accountNumber, path), Times.Once);
        }

        [Fact]
        public async Task HardDeleteDocumentsAsync_WhenIdsProvided_CallsRepositoryAndReturnsDeletedCount()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var ids = _fixture.CreateMany<long>(5).ToArray();
            var expectedDeleted = ids.Length;

            _repositoryMock
                .Setup(r => r.HardDeleteDocumentsAsync(
                    accountNumber,
                    It.Is<IEnumerable<long>>(e => e.SequenceEqual(ids))))
                .ReturnsAsync(expectedDeleted);

            // Act
            var result = await _sut.HardDeleteDocumentsAsync(accountNumber, ids);

            // Assert
            result.Should().Be(expectedDeleted);
            _repositoryMock.Verify(r =>
                r.HardDeleteDocumentsAsync(
                    accountNumber,
                    It.Is<IEnumerable<long>>(e => e.SequenceEqual(ids))), Times.Once);
        }

        [Fact]
        public async Task HardDeleteDocumentsAsync_WhenRepositoryReturnsZero_PropagatesZero()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var ids = _fixture.CreateMany<long>(3).ToArray();

            _repositoryMock
                .Setup(r => r.HardDeleteDocumentsAsync(accountNumber, It.IsAny<IEnumerable<long>>()))
                .ReturnsAsync(0);

            // Act
            var result = await _sut.HardDeleteDocumentsAsync(accountNumber, ids);

            // Assert
            result.Should().Be(0);
            _repositoryMock.Verify(r =>
                r.HardDeleteDocumentsAsync(
                    accountNumber,
                    It.Is<IEnumerable<long>>(e => e.SequenceEqual(ids))), Times.Once);
        }

        [Fact]
        public async Task GetDocumentsArchiveStreamAsync_WhenDocumentsExist_BuildsZipWithEntries()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var company = _fixture.Create<PennylaneCompany>();
            var pennylaneCompanyInternalId = _fixture.Create<int>();

            var documents = new List<Document>
            {
                new(1L, "invoice.pdf", "/path", "http://example.com/1", DateTime.UtcNow, DateTime.UtcNow),
                new(2L, "report.xlsx", "/path", "http://example.com/2", DateTime.UtcNow, DateTime.UtcNow),
            };
            var ids = documents.Select(d => d.Id).ToList();

            _repositoryMock.Setup(r => r.GetDocumentsByIdsAsync(accountNumber, It.IsAny<IReadOnlyCollection<long>>()))
                .ReturnsAsync(documents);
            _pennylaneApiGroupServiceMock.Setup(s => s.TryGetCompanyByIdAsync(accountNumber))
                .ReturnsAsync(company);
            _mockPennylaneService.Setup(s => s.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                .ReturnsAsync(pennylaneCompanyInternalId);

            foreach (var document in documents)
            {
                var dmsFiles = new List<PennylaneDmsFile> { new() { Id = (int)document.Id, Url = $"http://example.com/{document.Id}" } };
                _pennylaneApiFirmServiceMock.Setup(s => s.GetCompanyDmsFilesAsync(company.FirmId, pennylaneCompanyInternalId, document.Id.ToString()))
                    .ReturnsAsync(dmsFiles);
                _mockDocumentStreamService.Setup(s => s.GetStreamFromUrlAsync(dmsFiles.First().Url))
                    .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
            }

            // Act
            var result = await _sut.GetDocumentsArchiveStreamAsync(accountNumber, ids);

            // Assert
            result.Position.Should().Be(0);
            using var archive = new System.IO.Compression.ZipArchive(result, System.IO.Compression.ZipArchiveMode.Read);
            archive.Entries.Select(e => e.Name).Should().BeEquivalentTo("invoice.pdf", "report.xlsx");
        }

        [Fact]
        public async Task GetDocumentsArchiveStreamAsync_WhenNoDocumentsFound_ReturnsEmptyStream()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();

            _repositoryMock.Setup(r => r.GetDocumentsByIdsAsync(accountNumber, It.IsAny<IReadOnlyCollection<long>>()))
                .ReturnsAsync(Array.Empty<Document>());

            // Act
            var result = await _sut.GetDocumentsArchiveStreamAsync(accountNumber, new List<long> { 1L });

            // Assert: the service exposes the "nothing found" case as Stream.Null; the controller
            // turns it into a 404 rather than shipping a 0-byte archive.
            result.Should().BeSameAs(Stream.Null);
        }

        [Fact]
        public async Task GetDocumentsArchiveStreamAsync_WhenNameContainsPathSeparators_KeepsOnlyTheFileName()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var company = _fixture.Create<PennylaneCompany>();
            var pennylaneCompanyInternalId = _fixture.Create<int>();

            var documents = new List<Document>
            {
                new(1L, "../../etc/passwd", "/path", "http://example.com/1", DateTime.UtcNow, DateTime.UtcNow),
                new(2L, @"C:\Windows\System32\evil.pdf", "/path", "http://example.com/2", DateTime.UtcNow, DateTime.UtcNow),
            };
            var ids = documents.Select(d => d.Id).ToList();

            _repositoryMock.Setup(r => r.GetDocumentsByIdsAsync(accountNumber, It.IsAny<IReadOnlyCollection<long>>()))
                .ReturnsAsync(documents);
            _pennylaneApiGroupServiceMock.Setup(s => s.TryGetCompanyByIdAsync(accountNumber))
                .ReturnsAsync(company);
            _mockPennylaneService.Setup(s => s.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                .ReturnsAsync(pennylaneCompanyInternalId);

            foreach (var document in documents)
            {
                var dmsFiles = new List<PennylaneDmsFile> { new() { Id = (int)document.Id, Url = $"http://example.com/{document.Id}" } };
                _pennylaneApiFirmServiceMock.Setup(s => s.GetCompanyDmsFilesAsync(company.FirmId, pennylaneCompanyInternalId, document.Id.ToString()))
                    .ReturnsAsync(dmsFiles);
                _mockDocumentStreamService.Setup(s => s.GetStreamFromUrlAsync(dmsFiles.First().Url))
                    .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
            }

            // Act
            var result = await _sut.GetDocumentsArchiveStreamAsync(accountNumber, ids);

            // Assert
            using var archive = new System.IO.Compression.ZipArchive(result, System.IO.Compression.ZipArchiveMode.Read);
            archive.Entries.Select(e => e.FullName).Should().BeEquivalentTo("passwd", "evil.pdf");
        }

        [Fact]
        public async Task GetDocumentsArchiveStreamAsync_WhenCompanyResolutionFails_ThrowsExplicitly()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var documents = new List<Document>
            {
                new(1L, "invoice.pdf", "/path", "http://example.com/1", DateTime.UtcNow, DateTime.UtcNow),
            };

            _repositoryMock.Setup(r => r.GetDocumentsByIdsAsync(accountNumber, It.IsAny<IReadOnlyCollection<long>>()))
                .ReturnsAsync(documents);
            _pennylaneApiGroupServiceMock.Setup(s => s.TryGetCompanyByIdAsync(accountNumber))
                .ReturnsAsync((PennylaneCompany)null);

            // Act
            var act = async () => await _sut.GetDocumentsArchiveStreamAsync(accountNumber, new List<long> { 1L });

            // Assert
            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task GetDocumentsArchiveStreamAsync_WhenInternalIdResolutionFails_ThrowsExplicitly()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var company = _fixture.Create<PennylaneCompany>();
            var documents = new List<Document>
            {
                new(1L, "invoice.pdf", "/path", "http://example.com/1", DateTime.UtcNow, DateTime.UtcNow),
            };

            _repositoryMock.Setup(r => r.GetDocumentsByIdsAsync(accountNumber, It.IsAny<IReadOnlyCollection<long>>()))
                .ReturnsAsync(documents);
            _pennylaneApiGroupServiceMock.Setup(s => s.TryGetCompanyByIdAsync(accountNumber))
                .ReturnsAsync(company);
            _mockPennylaneService.Setup(s => s.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                .ReturnsAsync((int?)null);

            // Act
            var act = async () => await _sut.GetDocumentsArchiveStreamAsync(accountNumber, new List<long> { 1L });

            // Assert
            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task GetDocumentsArchiveStreamAsync_WhenDmsFilesMissing_SkipsEntry()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var company = _fixture.Create<PennylaneCompany>();
            var pennylaneCompanyInternalId = _fixture.Create<int>();
            var documents = new List<Document>
            {
                new(1L, "missing.pdf", "/path", "http://example.com/1", DateTime.UtcNow, DateTime.UtcNow),
            };

            _repositoryMock.Setup(r => r.GetDocumentsByIdsAsync(accountNumber, It.IsAny<IReadOnlyCollection<long>>()))
                .ReturnsAsync(documents);
            _pennylaneApiGroupServiceMock.Setup(s => s.TryGetCompanyByIdAsync(accountNumber))
                .ReturnsAsync(company);
            _mockPennylaneService.Setup(s => s.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                .ReturnsAsync(pennylaneCompanyInternalId);
            _pennylaneApiFirmServiceMock.Setup(s => s.GetCompanyDmsFilesAsync(company.FirmId, pennylaneCompanyInternalId, "1"))
                .ReturnsAsync(new List<PennylaneDmsFile>());

            // Act
            var result = await _sut.GetDocumentsArchiveStreamAsync(accountNumber, new List<long> { 1L });

            // Assert
            using var archive = new System.IO.Compression.ZipArchive(result, System.IO.Compression.ZipArchiveMode.Read);
            archive.Entries.Should().BeEmpty();
        }

        [Fact]
        public async Task GetDocumentsArchiveStreamAsync_WhenDuplicateNames_MakesEntryNamesUnique()
        {
            // Arrange
            var accountNumber = _fixture.Create<string>();
            var company = _fixture.Create<PennylaneCompany>();
            var pennylaneCompanyInternalId = _fixture.Create<int>();
            var documents = new List<Document>
            {
                new(1L, "same.pdf", "/path", "http://example.com/1", DateTime.UtcNow, DateTime.UtcNow),
                new(2L, "same.pdf", "/path", "http://example.com/2", DateTime.UtcNow, DateTime.UtcNow),
            };
            var ids = documents.Select(d => d.Id).ToList();

            _repositoryMock.Setup(r => r.GetDocumentsByIdsAsync(accountNumber, It.IsAny<IReadOnlyCollection<long>>()))
                .ReturnsAsync(documents);
            _pennylaneApiGroupServiceMock.Setup(s => s.TryGetCompanyByIdAsync(accountNumber))
                .ReturnsAsync(company);
            _mockPennylaneService.Setup(s => s.GetPennylaneInternalIdByAccountNumberAsync(company.FirmId, accountNumber, It.IsAny<bool>()))
                .ReturnsAsync(pennylaneCompanyInternalId);

            foreach (var document in documents)
            {
                var dmsFiles = new List<PennylaneDmsFile> { new() { Id = (int)document.Id, Url = $"http://example.com/{document.Id}" } };
                _pennylaneApiFirmServiceMock.Setup(s => s.GetCompanyDmsFilesAsync(company.FirmId, pennylaneCompanyInternalId, document.Id.ToString()))
                    .ReturnsAsync(dmsFiles);
                _mockDocumentStreamService.Setup(s => s.GetStreamFromUrlAsync(dmsFiles.First().Url))
                    .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
            }

            // Act
            var result = await _sut.GetDocumentsArchiveStreamAsync(accountNumber, ids);

            // Assert
            using var archive = new System.IO.Compression.ZipArchive(result, System.IO.Compression.ZipArchiveMode.Read);
            archive.Entries.Select(e => e.Name).Should().BeEquivalentTo("same.pdf", "same (1).pdf");
        }
    }
}