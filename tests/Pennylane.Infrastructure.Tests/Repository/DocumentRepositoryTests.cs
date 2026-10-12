using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pennylane.Domain;
using Pennylane.Domain.Entities;
using Pennylane.Domain.Entities.Pennylane;
using Pennylane.Infrastructure.Repository;
using Pennylane.Infrastructure.Tests.Utils;
using Pulse.ContactRegistry.Infrastructure.Context;
using SQLitePCL;
using System.Collections.ObjectModel;

namespace Pennylane.Infrastructure.Tests.Repository;

public class DocumentRepositoryTests
{
    private readonly Fixture _fixture;
    private readonly PennylaneContext _context;
    private readonly DocumentRepository _sut;

    public DocumentRepositoryTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        _context = new PennylaneContext(new DbContextOptionsBuilder<PennylaneContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _sut = new DocumentRepository(_context);
    }

    [Theory]
    [InlineData(1, 100, 300)]
    [InlineData(1, 100, 90)]
    [InlineData(2, 100, 300)]
    [InlineData(3, 100, 300)]
    [InlineData(4, 100, 300)]
    public async Task GetDocumentsAsync_Should_Get_PaginatedDocuments(int pageRank, int pageSize, int documentsCount)
    {
        var accountNumber = _fixture.Create<string>();
        var synchroDate = DateTime.Now.AddDays(-10);

        var documents = BuildDocumentEntities(synchroDate, accountNumber, documentsCount);
        var expectedDocuments = GetPageDocuments(documents, pageRank, pageSize);
        var syncStatus = new DocumentsSyncStatusEntity
        {
            AccountId = _fixture.Create<int>(),
            AccountNumber = accountNumber,
            LastSynchro = synchroDate
        };

        await _context.DocumentEntity.AddRangeAsync(documents);
        await _context.DocumentsSyncStatusEntity.AddRangeAsync(syncStatus);

        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var result = await _sut.GetDocumentsAsync(accountNumber, pageRank, pageSize);

        result.Should().BeEquivalentTo(new AccountDocumentsPage(pageRank, pageSize, documentsCount, expectedDocuments, synchroDate));
    }

    [Fact]
    public async Task GetDocumentsAsync_WithoutSynchroData_Should_Return_PaginatedDocuments_With_Null_LastSynchroDate()
    {
        var accountNumber = _fixture.Create<string>();

        var documentsCount = 300;
        var pageSize = 100;
        var pageRank = 1;

        var documents = BuildDocumentEntitiesWithoutSynchroData(accountNumber, documentsCount);
        var expectedDocuments = GetPageDocuments(documents, pageRank, pageSize);
        await _context.DocumentEntity.AddRangeAsync(documents);
        await _context.SaveChangesAsync();

        var result = await _sut.GetDocumentsAsync(accountNumber, pageRank, pageSize);

        result.Should().BeEquivalentTo(new AccountDocumentsPage(pageRank, pageSize, documentsCount, expectedDocuments, null));
    }

    [Fact]
    public async Task GetSyncStatusAsync_Should_Return_SyncStatus_When_Exists()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var synchroDate = DateTime.Now.AddDays(-10);

        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Create();

        var syncStatus = _fixture.Build<DocumentsSyncStatusEntity>()
            .With(s => s.AccountNumber, accountNumber)
            .With(s => s.LastSynchro, synchroDate)
            .With(s => s.Account, account)
            .Create();

        await _context.AccountEntity.AddAsync(account);
        await _context.DocumentsSyncStatusEntity.AddAsync(syncStatus);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetSyncStatusAsync(accountNumber);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(syncStatus, options => options.Excluding(ctx => ctx.Account));
    }

    [Fact]
    public async Task GetSyncStatusAsync_Should_Return_Null_When_NotExists()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();

        // Act
        var result = await _sut.GetSyncStatusAsync(accountNumber);

        // Assert
        result.Should().BeNull();
    }



    [Fact]
    public async Task InsertPennylaneDocumentsAsync_InsertsOnlyNonExistingDocuments()
    {
        var accountNumber = _fixture.Create<string>();
        var accountId = _fixture.Create<int>();
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .With(a => a.AccountId, accountId)
            .Without(a => a.DocumentEntity)
            .Create();
        await _context.AccountEntity.AddAsync(account);
        await _context.SaveChangesAsync();

        // Existing doc in DB
        var docIdExisting = _fixture.Create<long>();
        var existingDoc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.Id, docIdExisting)
            .With(d => d.AccountId, accountId)
            .Create();
        await _context.DocumentEntity.AddAsync(existingDoc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Try to insert 2 docs, one already exists
        var docIdNew = _fixture.Create<long>();
        var newDoc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.Id, docIdNew)
            .With(d => d.AccountId, accountId)
            .Create();
        var docsToInsert = new List<DocumentEntity> { existingDoc, newDoc };

        await _sut.InsertPennylaneDocumentsAsync(accountNumber, docsToInsert);

        // Only newDoc should be inserted
        var docsInDb = _context.DocumentEntity.Where(d => d.AccountId == account.AccountId).ToList();
        docsInDb.Should().ContainSingle(d => d.Id == docIdExisting);
        docsInDb.Should().ContainSingle(d => d.Id == docIdNew);
    }

    [Fact]
    public async Task GetDocumentByIdAsync_ReturnsDocument_WhenDocumentExists()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var documentId = _fixture.Create<long>();
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();
        var expectedDocument = _fixture.Build<DocumentEntity>()
            .With(d => d.Id, documentId)
            .With(d => d.Account, account)
            .Create();

        await _context.AccountEntity.AddAsync(account);
        await _context.DocumentEntity.AddAsync(expectedDocument);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetDocumentByIdAsync(accountNumber, documentId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(expectedDocument.Id);
    }

    [Fact]
    public async Task GetDocumentByIdAsync_ThrowsKeyNotFoundException_WhenDocumentNotFound()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var documentId = _fixture.Create<long>();

        // Assurez-vous qu'il n'y a aucun document dans la base de données avec cet ID
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.GetDocumentByIdAsync(accountNumber, documentId));
    }

    [Fact]
    public async Task GetAllDocumentsByPathAsync_ReturnsOnlyDocumentsForAccountAndPath()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var otherAccountNumber = _fixture.Create<string>();
        var path = Pennylane.Application.StaticValues.Constant.PermanentPublications;

        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        var otherAccount = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, otherAccountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        await _context.AccountEntity.AddRangeAsync(account, otherAccount);

        var shouldMatch1 = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Path, path)
            .Create();

        var shouldMatch2 = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Path, path)
            .Create();

        var differentPath = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Path, "/other")
            .Create();

        var differentAccount = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, otherAccount.AccountId)
            .With(d => d.Path, path)
            .Create();

        await _context.DocumentEntity.AddRangeAsync(shouldMatch1, shouldMatch2, differentPath, differentAccount);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetAllDocumentsByPathAsync(accountNumber, path);

        // Assert
        result.Should().NotBeNull();
        var resultIds = result.Select(d => d.Id).OrderBy(x => x).ToArray();
        var expectedIds = new[] { shouldMatch1.Id, shouldMatch2.Id }.OrderBy(x => x).ToArray();
        resultIds.Should().Equal(expectedIds);

        // All returned docs have the path we asked for
        result.Should().OnlyContain(d => d.Path == path);
    }

    [Fact]
    public async Task GetAllDocumentsByPathAsync_WhenNoMatch_ReturnsEmpty()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var path = Pennylane.Application.StaticValues.Constant.PermanentPublications;

        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        await _context.AccountEntity.AddAsync(account);

        // Add a doc under a different path
        var otherPathDoc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Path, "/somewhere-else")
            .Create();
        await _context.DocumentEntity.AddAsync(otherPathDoc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetAllDocumentsByPathAsync(accountNumber, path);

        // Assert
        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task HardDeleteDocumentsAsync_DeletesOnlySpecifiedIdsForAccount()
    {
        // Arrange (use SQLite in-memory to exercise ExecuteDeleteAsync)
        var options = DbContextMockExtensions
            .CreateSqliteInMemoryOptions(nameof(HardDeleteDocumentsAsync_DeletesOnlySpecifiedIdsForAccount));
        using var sqliteContext = DbContextMockExtensions
            .CreateSqlLitePennylaneContext(options);

        var repo = new DocumentRepository(sqliteContext);

        var acc1 = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, "ACC123")
            .Without(a => a.DocumentEntity)
            .Without(a => a.RoleEntity)
            .Without(a => a.PnlRoleEntity)
            .Create();
        var acc2 = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, "ACC999")
            .Without(a => a.DocumentEntity)
            .Without(a => a.RoleEntity)
            .Without(a => a.PnlRoleEntity)
            .Create();

        await sqliteContext.AccountEntity.AddRangeAsync(acc1, acc2);

        // acc1 docs: 1,2,3,4 — acc2 doc: 100
        var d1 = _fixture.Build<DocumentEntity>().Without(d => d.Account).With(d => d.Id, 1).With(d => d.AccountId, acc1.AccountId).Create();
        var d2 = _fixture.Build<DocumentEntity>().Without(d => d.Account).With(d => d.Id, 2).With(d => d.AccountId, acc1.AccountId).Create();
        var d3 = _fixture.Build<DocumentEntity>().Without(d => d.Account).With(d => d.Id, 3).With(d => d.AccountId, acc1.AccountId).Create();
        var d4 = _fixture.Build<DocumentEntity>().Without(d => d.Account).With(d => d.Id, 4).With(d => d.AccountId, acc1.AccountId).Create();
        var d100 = _fixture.Build<DocumentEntity>().Without(d => d.Account).With(d => d.Id, 100).With(d => d.AccountId, acc2.AccountId).Create();

        await sqliteContext.DocumentEntity.AddRangeAsync(d1, d2, d3, d4, d100);
        await sqliteContext.SaveChangesAsync();
        sqliteContext.ChangeTracker.Clear();

        // Act
        var deleted = await repo.HardDeleteDocumentsAsync(acc1.AccountNumber, new[] { 2L, 4L });

        // Assert
        deleted.Should().Be(2);

        var acc1Remaining = await sqliteContext.Set<DocumentEntity>()
            .Where(d => d.AccountId == acc1.AccountId)
            .OrderBy(d => d.Id)
            .Select(d => d.Id)
            .ToArrayAsync();

        acc1Remaining.Should().Equal(1, 3);

        var acc2Remaining = await sqliteContext.Set<DocumentEntity>()
            .Where(d => d.AccountId == acc2.AccountId)
            .OrderBy(d => d.Id)
            .Select(d => d.Id)
            .ToArrayAsync();

        acc2Remaining.Should().Equal(100);
    }

    [Fact]
    public async Task HardDeleteDocumentsAsync_WithEmptyIds_ReturnsZeroAndDoesNothing()
    {
        // Arrange (SQLite in-memory)
        var options = Pennylane.Infrastructure.Tests.Utils.DbContextMockExtensions
            .CreateSqliteInMemoryOptions(nameof(HardDeleteDocumentsAsync_WithEmptyIds_ReturnsZeroAndDoesNothing));
        using var sqliteContext = Pennylane.Infrastructure.Tests.Utils.DbContextMockExtensions
            .CreateSqlLitePennylaneContext(options);

        var repo = new Pennylane.Infrastructure.Repository.DocumentRepository(sqliteContext);

        var acc = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, "ACC123")
            .Without(a => a.DocumentEntity)
            .Without(a => a.PnlRoleEntity)
            .Without(a => a.RoleEntity)
            .Create();

        await sqliteContext.AccountEntity.AddAsync(acc);

        var d1 = _fixture.Build<DocumentEntity>().Without(d => d.Account).With(d => d.Id, 1).With(d => d.AccountId, acc.AccountId).Create();
        var d2 = _fixture.Build<DocumentEntity>().Without(d => d.Account).With(d => d.Id, 2).With(d => d.AccountId, acc.AccountId).Create();

        await sqliteContext.DocumentEntity.AddRangeAsync(d1, d2);
        await sqliteContext.SaveChangesAsync();

        // Act
        var deleted = await repo.HardDeleteDocumentsAsync(acc.AccountNumber, Array.Empty<long>());

        // Assert
        deleted.Should().Be(0);

        var stillThere = await sqliteContext.Set<DocumentEntity>()
            .Where(d => d.AccountId == acc.AccountId)
            .OrderBy(d => d.Id)
            .Select(d => d.Id)
            .ToArrayAsync();

        stillThere.Should().Equal(1, 2);
    }

    [Fact]
    public async Task GetAllDocumentsByPathAsync_WithEmptyPath_ReturnsAllDocumentsForAccount()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var otherAccountNumber = _fixture.Create<string>();

        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        var otherAccount = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, otherAccountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        await _context.AccountEntity.AddRangeAsync(account, otherAccount);

        // Documents with different paths for the target account
        var doc1 = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Path, "/Dossier permanent/00 - Publications/file1.pdf")
            .Create();

        var doc2 = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Path, "/Dossier annuel/Exercice 2023/FEC/file2.pdf")
            .Create();

        var doc3 = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Path, "/Other/Path/file3.pdf")
            .Create();

        // Document for another account (should NOT be returned)
        var otherAccountDoc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, otherAccount.AccountId)
            .With(d => d.Path, "/Dossier permanent/00 - Publications/file4.pdf")
            .Create();

        await _context.DocumentEntity.AddRangeAsync(doc1, doc2, doc3, otherAccountDoc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act - Call with empty string path
        var result = await _sut.GetAllDocumentsByPathAsync(accountNumber, string.Empty);

        // Assert - Should return all documents for the account, regardless of path
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        var resultIds = result.Select(d => d.Id).OrderBy(x => x).ToArray();
        var expectedIds = new[] { doc1.Id, doc2.Id, doc3.Id }.OrderBy(x => x).ToArray();
        resultIds.Should().Equal(expectedIds);

        // Should NOT include documents from other accounts
        result.Should().NotContain(d => d.Id == otherAccountDoc.Id);
    }

    [Fact]
    public async Task GetAllDocumentsByPathAsync_WithEmptyPath_ReturnsEmptyWhenNoDocuments()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();

        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        await _context.AccountEntity.AddAsync(account);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act - Call with empty string path
        var result = await _sut.GetAllDocumentsByPathAsync(accountNumber, string.Empty);

        // Assert - Should return empty collection
        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetDocumentsAsync_WithSearch_ShouldFilterByName()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var search = "facture";
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        var doc1 = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Name, "facture_2024.pdf")
            .Create();

        var doc2 = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Name, "contrat_client.pdf")
            .Create();

        var doc3 = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Name, "Facture_annuelle.pdf")
            .Create();

        await _context.AccountEntity.AddAsync(account);
        await _context.DocumentEntity.AddRangeAsync(doc1, doc2, doc3);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetDocumentsAsync(accountNumber, 1, 10, search);

        // Assert - doc1 and doc3 contain "facture" (case-insensitive depends on DB collation, InMemory is case-sensitive by default)
        result.Documents.Should().HaveCount(1); // InMemory is case-sensitive, only doc1 matches "facture"
        result.Documents.Should().Contain(d => d.Name.Contains("facture"));
    }

    [Fact]
    public async Task GetDocumentsAsync_WithSearch_ShouldReturnEmpty_WhenNoMatch()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var search = "inexistant";
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        var doc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Name, "facture.pdf")
            .Create();

        await _context.AccountEntity.AddAsync(account);
        await _context.DocumentEntity.AddAsync(doc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetDocumentsAsync(accountNumber, 1, 10, search);

        // Assert
        result.Documents.Should().BeEmpty();
        result.DocumentsCount.Should().Be(0);
    }

    [Fact]
    public async Task GetDocumentsAsync_WithNullSearch_ShouldReturnAllDocuments()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        var doc1 = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .Create();

        var doc2 = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .Create();

        await _context.AccountEntity.AddAsync(account);
        await _context.DocumentEntity.AddRangeAsync(doc1, doc2);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetDocumentsAsync(accountNumber, 1, 10, null);

        // Assert
        result.Documents.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDocumentsAsync_WithSyncStatusButDefaultLastSynchro_ShouldReturn_NullLastSynchroDate()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();

        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Without(a => a.DocumentsSyncStatusEntity)
            .Create();

        await _context.AccountEntity.AddAsync(account);
        await _context.SaveChangesAsync();

        var syncStatus = new DocumentsSyncStatusEntity
        {
            AccountId = account.AccountId,
            AccountNumber = accountNumber,
            LastSynchro = default // explicitly default => repository should return null
        };

        var doc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .Create();

        await _context.DocumentsSyncStatusEntity.AddAsync(syncStatus);
        await _context.DocumentEntity.AddAsync(doc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetDocumentsAsync(accountNumber, 1, 10);

        // Assert
        result.LastSynchroDate.Should().BeNull();
        result.Documents.Should().HaveCount(1);
    }
    [Fact]
    public async Task AddOrUpdateSyncDateAsync_WhenSyncStatusDoesNotExist_ShouldCreateNewSyncStatus()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var accountId = _fixture.Create<int>();

        var beforeUtc = DateTime.UtcNow;

        // Act
        await _sut.AddOrUpdateSyncDateAsync(accountId, accountNumber);

        var afterUtc = DateTime.UtcNow;

        // Assert
        var syncStatus = await _context.DocumentsSyncStatusEntity
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AccountNumber == accountNumber);

        syncStatus.Should().NotBeNull();
        syncStatus!.AccountId.Should().Be(accountId);
        syncStatus.AccountNumber.Should().Be(accountNumber);
        syncStatus.LastSynchro.Should().BeOnOrAfter(beforeUtc).And.BeOnOrBefore(afterUtc);
    }

    [Fact]
    public async Task AddOrUpdateSyncDateAsync_WhenSyncStatusExists_ShouldUpdateLastSynchro()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var accountId = _fixture.Create<int>();
        var oldDate = DateTime.UtcNow.AddDays(-30);

        var existing = new DocumentsSyncStatusEntity
        {
            AccountId = accountId,
            AccountNumber = accountNumber,
            LastSynchro = oldDate
        };
        await _context.DocumentsSyncStatusEntity.AddAsync(existing);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var beforeUtc = DateTime.UtcNow;

        // Act
        await _sut.AddOrUpdateSyncDateAsync(accountId, accountNumber);

        var afterUtc = DateTime.UtcNow;

        // Assert
        var updated = await _context.DocumentsSyncStatusEntity
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AccountNumber == accountNumber);

        updated.Should().NotBeNull();
        updated!.LastSynchro.Should().BeOnOrAfter(beforeUtc).And.BeOnOrBefore(afterUtc);
        updated.LastSynchro.Should().BeAfter(oldDate);

        // Ensure we didn't create a duplicate
        var count = await _context.DocumentsSyncStatusEntity
            .CountAsync(s => s.AccountNumber == accountNumber);
        count.Should().Be(1);
    }

    [Fact]
    public async Task InsertPennylaneDocumentsAsync_WhenAccountDoesNotExist_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var documents = new List<DocumentEntity>
    {
        _fixture.Build<DocumentEntity>().Without(d => d.Account).Create()
    };

        // Act & Assert
        var act = async () => await _sut.InsertPennylaneDocumentsAsync(accountNumber, documents);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Account {accountNumber} not found");
    }

    [Fact]
    public async Task InsertPennylaneDocumentsAsync_WhenAllDocumentsAlreadyExist_ShouldReturnZero()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();
        await _context.AccountEntity.AddAsync(account);
        await _context.SaveChangesAsync();

        var docId = _fixture.Create<long>();
        var existingDoc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.Id, docId)
            .With(d => d.AccountId, account.AccountId)
            .Create();
        await _context.DocumentEntity.AddAsync(existingDoc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var docsToInsert = new List<DocumentEntity> { existingDoc };

        // Act
        var inserted = await _sut.InsertPennylaneDocumentsAsync(accountNumber, docsToInsert);

        // Assert
        inserted.Should().Be(0);
        var docCount = await _context.DocumentEntity.CountAsync(d => d.AccountId == account.AccountId);
        docCount.Should().Be(1);
    }

    [Fact]
    public async Task InsertPennylaneDocumentsAsync_ShouldReturn_NumberOfNewlyInsertedDocuments()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();
        await _context.AccountEntity.AddAsync(account);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var newDocs = Enumerable.Range(1, 3).Select(i =>
            _fixture.Build<DocumentEntity>()
                .Without(d => d.Account)
                .With(d => d.Id, (long)i)
                .With(d => d.AccountId, account.AccountId)
                .Create()).ToList();

        // Act
        var inserted = await _sut.InsertPennylaneDocumentsAsync(accountNumber, newDocs);

        // Assert
        inserted.Should().Be(3);
        var docsInDb = await _context.DocumentEntity
            .Where(d => d.AccountId == account.AccountId)
            .ToListAsync();
        docsInDb.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HardDeleteDocumentsAsync_WithNullOrEmptyAccountNumber_ShouldReturnZero(string? accountNumber)
    {
        // Act
        var result = await _sut.HardDeleteDocumentsAsync(accountNumber!, new[] { 1L, 2L });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task HardDeleteDocumentsAsync_WithNullDocumentIds_ShouldReturnZero()
    {
        // Act
        var result = await _sut.HardDeleteDocumentsAsync("ACC123", null!);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task HardDeleteDocumentsAsync_WhenAccountDoesNotExist_ShouldReturnZero()
    {
        // Arrange
        var options = DbContextMockExtensions
            .CreateSqliteInMemoryOptions(nameof(HardDeleteDocumentsAsync_WhenAccountDoesNotExist_ShouldReturnZero));
        using var sqliteContext = DbContextMockExtensions
            .CreateSqlLitePennylaneContext(options);
        var repo = new DocumentRepository(sqliteContext);

        // Act
        var result = await repo.HardDeleteDocumentsAsync("UNKNOWN_ACCOUNT", new[] { 1L, 2L });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task HardDeleteDocumentsAsync_WithDuplicateIds_ShouldDeduplicateAndDeleteOnce()
    {
        // Arrange
        var options = DbContextMockExtensions
            .CreateSqliteInMemoryOptions(nameof(HardDeleteDocumentsAsync_WithDuplicateIds_ShouldDeduplicateAndDeleteOnce));
        using var sqliteContext = DbContextMockExtensions
            .CreateSqlLitePennylaneContext(options);
        var repo = new DocumentRepository(sqliteContext);

        var acc = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, "ACC123")
            .Without(a => a.DocumentEntity)
            .Without(a => a.RoleEntity)
            .Without(a => a.PnlRoleEntity)
            .Create();
        await sqliteContext.AccountEntity.AddAsync(acc);

        var d1 = _fixture.Build<DocumentEntity>().Without(d => d.Account).With(d => d.Id, 1L).With(d => d.AccountId, acc.AccountId).Create();
        var d2 = _fixture.Build<DocumentEntity>().Without(d => d.Account).With(d => d.Id, 2L).With(d => d.AccountId, acc.AccountId).Create();
        await sqliteContext.DocumentEntity.AddRangeAsync(d1, d2);
        await sqliteContext.SaveChangesAsync();
        sqliteContext.ChangeTracker.Clear();

        // Act — pass duplicates
        var deleted = await repo.HardDeleteDocumentsAsync(acc.AccountNumber, new[] { 1L, 1L, 1L, 2L });

        // Assert
        deleted.Should().Be(2);

        var remaining = await sqliteContext.Set<DocumentEntity>()
            .Where(d => d.AccountId == acc.AccountId)
            .ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAllDocumentsByPathAsync_WithNullOrEmptyAccountNumber_ShouldReturnEmpty(string? accountNumber)
    {
        // Act
        var result = await _sut.GetAllDocumentsByPathAsync(accountNumber!, "/some/path");

        // Assert
        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetDocumentCountsByAccountAsync_ShouldReturn_GroupedCountsWithinDateRange()
    {
        // Arrange
        var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var account1 = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 1)
            .With(a => a.AccountNumber, "ACC001")
            .With(a => a.LegalName, "Company One")
            .Without(a => a.DocumentEntity)
            .Create();

        var account2 = _fixture.Build<AccountEntity>()
            .With(a => a.AccountId, 2)
            .With(a => a.AccountNumber, "ACC002")
            .With(a => a.LegalName, "Company Two")
            .Without(a => a.DocumentEntity)
            .Create();

        await _context.AccountEntity.AddRangeAsync(account1, account2);

        // Two docs for account1 in range
        var doc1A = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, 1)
            .With(d => d.CreatedAt, new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc))
            .Create();
        var doc1B = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, 1)
            .With(d => d.CreatedAt, new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc))
            .Create();

        // One doc for account2 in range
        var doc2A = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, 2)
            .With(d => d.CreatedAt, new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc))
            .Create();

        // Out-of-range doc — should be excluded
        var docOutBefore = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, 1)
            .With(d => d.CreatedAt, new DateTime(2023, 12, 31, 23, 59, 59, DateTimeKind.Utc))
            .Create();

        // Boundary at toUtc — exclusive, must be excluded
        var docOutAfter = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, 2)
            .With(d => d.CreatedAt, toUtc)
            .Create();

        await _context.DocumentEntity.AddRangeAsync(doc1A, doc1B, doc2A, docOutBefore, docOutAfter);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetDocumentCountsByAccountAsync(fromUtc, toUtc);

        // Assert
        result.Should().HaveCount(2);
        result[1].AccountNumber.Should().Be("ACC001");

        result[2].AccountNumber.Should().Be("ACC002");
    }

    [Fact]
    public async Task GetDocumentCountsByAccountAsync_WhenNoDocumentsInRange_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, "ACC001")
            .Without(a => a.DocumentEntity)
            .Create();

        var oldDoc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.CreatedAt, new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc))
            .Create();

        await _context.AccountEntity.AddAsync(account);
        await _context.DocumentEntity.AddAsync(oldDoc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetDocumentCountsByAccountAsync(fromUtc, toUtc);

        // Assert
        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetDocumentsAsync_WithSearch_ShouldReturnCorrect_TotalCount()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        var matchingDocs = Enumerable.Range(1, 5).Select(i =>
            _fixture.Build<DocumentEntity>()
                .Without(d => d.Account)
                .With(d => d.AccountId, account.AccountId)
                .With(d => d.Name, $"facture_{i}.pdf")
                .Create()).ToList();

        var nonMatchingDoc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Name, "contrat.pdf")
            .Create();

        await _context.AccountEntity.AddAsync(account);
        await _context.DocumentEntity.AddRangeAsync(matchingDocs);
        await _context.DocumentEntity.AddAsync(nonMatchingDoc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act — page size smaller than result set
        var result = await _sut.GetDocumentsAsync(accountNumber, 1, 3, "facture");

        // Assert
        result.DocumentsCount.Should().Be(5); // total matching, not page size
        result.Documents.Should().HaveCount(3); // page size respected
    }
    [Fact]
    public async Task GetDocumentsAsync_WithExcludedExtensions_ShouldExcludeMatchingDocumentsFromListAndCount()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        var pdfDoc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Name, "bilan_2024.pdf")
            .Create();

        var ediDoc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Name, "liasse_fiscale.EDI")
            .Create();

        await _context.AccountEntity.AddAsync(account);
        await _context.DocumentEntity.AddRangeAsync(pdfDoc, ediDoc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetDocumentsAsync(accountNumber, 1, 10, search: null, excludedExtensions: new[] { ".EDI" });

        // Assert
        result.DocumentsCount.Should().Be(1);
        result.Documents.Should().ContainSingle(d => d.Name == "bilan_2024.pdf");
        result.Documents.Should().NotContain(d => d.Name == "liasse_fiscale.EDI");
    }

    [Fact]
    public async Task GetDocumentsAsync_WithExcludedExtensions_ShouldExclude_OnRelationalProvider()
    {
        var options = DbContextMockExtensions
            .CreateSqliteInMemoryOptions(nameof(GetDocumentsAsync_WithExcludedExtensions_ShouldExclude_OnRelationalProvider));
        using var sqliteContext = DbContextMockExtensions.CreateSqlLitePennylaneContext(options);
        var repo = new DocumentRepository(sqliteContext);

        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, "ACC123")
            .Without(a => a.DocumentEntity)
            .Without(a => a.RoleEntity)
            .Without(a => a.PnlRoleEntity)
            .Without(a => a.DocumentsSyncStatusEntity)
            .Create();
        await sqliteContext.AccountEntity.AddAsync(account);

        var pdf = _fixture.Build<DocumentEntity>().Without(d => d.Account).With(d => d.Id, 1L).With(d => d.AccountId, account.AccountId).With(d => d.Name, "bilan.pdf").Create();
        var edi = _fixture.Build<DocumentEntity>().Without(d => d.Account).With(d => d.Id, 2L).With(d => d.AccountId, account.AccountId).With(d => d.Name, "liasse.EDI").Create();
        await sqliteContext.DocumentEntity.AddRangeAsync(pdf, edi);
        await sqliteContext.SaveChangesAsync();
        sqliteContext.ChangeTracker.Clear();

        // Act
        var result = await repo.GetDocumentsAsync("ACC123", 1, 10, search: null, excludedExtensions: new[] { ".EDI" });

        // Assert
        result.DocumentsCount.Should().Be(1);
        result.Documents.Should().ContainSingle(d => d.Name == "bilan.pdf");
    }

    [Fact]
    public async Task GetDocumentsAsync_WithoutExcludedExtensions_ShouldReturnAllDocuments()
    {
        // Arrange
        var accountNumber = _fixture.Create<string>();
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentEntity)
            .Create();

        var pdfDoc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Name, "bilan_2024.pdf")
            .Create();

        var ediDoc = _fixture.Build<DocumentEntity>()
            .Without(d => d.Account)
            .With(d => d.AccountId, account.AccountId)
            .With(d => d.Name, "liasse_fiscale.EDI")
            .Create();

        await _context.AccountEntity.AddAsync(account);
        await _context.DocumentEntity.AddRangeAsync(pdfDoc, ediDoc);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _sut.GetDocumentsAsync(accountNumber, 1, 10);

        // Assert
        result.DocumentsCount.Should().Be(2);
        result.Documents.Should().HaveCount(2);
    }

    private static ReadOnlyCollection<Document> GetPageDocuments(DocumentEntity[] documents, int pageRank, int pageSize) =>
        documents
            .OrderByDescending(d => d.UpdatedAt)
            .Skip((pageRank - 1) * pageSize)
            .Take(pageSize)
            .Select(d => ToDocument(d)).ToList().AsReadOnly();

    private DocumentEntity[] BuildDocumentEntities(DateTime synchroDate, string accountNumber, int pageSize)
    {
        var synchro = _fixture
            .Build<DocumentsSyncStatusEntity>()
            .With(e => e.LastSynchro, synchroDate)
            .Without(e => e.Account)
            .Create();

        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .With(a => a.DocumentsSyncStatusEntity, synchro)
            .Without(a => a.DocumentEntity).Create();

        return _fixture.Build<DocumentEntity>()
            .With(d => d.Account, account)
            .CreateMany(pageSize)
            .ToArray();
    }
    private DocumentEntity[] BuildDocumentEntitiesWithoutSynchroData(string accountNumber, int pageSize)
    {
        var account = _fixture.Build<AccountEntity>()
            .With(a => a.AccountNumber, accountNumber)
            .Without(a => a.DocumentsSyncStatusEntity)
            .Without(a => a.DocumentEntity).Create();

        return _fixture.Build<DocumentEntity>()
            .With(d => d.Account, account)
            .CreateMany(pageSize)
            .ToArray();
    }

    private static Document ToDocument(DocumentEntity documentEntity)
    {
        return new Document(documentEntity.Id, documentEntity.Name, documentEntity.Path, documentEntity.Url, documentEntity.CreatedAt, documentEntity.UpdatedAt);
    }
}