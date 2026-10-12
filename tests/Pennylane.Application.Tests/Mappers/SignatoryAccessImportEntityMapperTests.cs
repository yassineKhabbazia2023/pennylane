using AutoFixture;
using Pennylane.Application.Mappers;
using Pennylane.Application.Models.Signatory;
using Pennylane.Domain.Entities.Pennylane;

namespace Pennylane.Application.Tests.Mappers;

public class SignatoryAccessImportEntityMapperTests
{
    private readonly Fixture _fixture;

    public SignatoryAccessImportEntityMapperTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [Fact]
    public void MapToSignatoryAccessModel_When_UserCanAccessButIsNotSignatory_Should_MapCorrectly()
    {
        // Arrange
        var currentEntity = _fixture.Build<SignatoryAccessEntity>()
            .With(s => s.CanAccess, true)
            .With(s => s.Email, "email")
            .With(s => s.FreeTrialEndDate, new DateTime(2026, 10, 31))
            .Without(s => s.Wave)
            .Create();
        var userEntities = _fixture.CreateMany<Tuple<SignatoryAccessEntity, string>>(2).ToList();
        userEntities.Add(Tuple.Create(currentEntity, "accNumber"));
        var terms = _fixture.Create<TermsEntity>();
        var currentUserEmail = "monemail@email.fr";

        // Act
        var result = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(currentEntity, userEntities, currentUserEmail);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.FreeTrialEndDate);
        Assert.True(result.CanAccess);
        Assert.Null(result.IsPennylaneSignatory);
        Assert.Null(result.Details);
        Assert.Null(result.BillingMonth);
    }

    [Fact]
    public void MapToSignatoryAccessModel_When_UserCannotAccessAndIsNotSignatory_Should_MapCorrectly()
    {
        // Arrange
        var currentEntity = _fixture.Build<SignatoryAccessEntity>()
            .With(s => s.CanAccess, false)
            .With(s => s.Email, "email")
            .Without(s => s.Wave)
            .Create();
        var userEntities = _fixture.CreateMany<Tuple<SignatoryAccessEntity, string>>(2).ToList();
        userEntities.Add(Tuple.Create(currentEntity, "accNumber"));
        var terms = _fixture.Create<TermsEntity>();
        var currentUserEmail = "monemail@email.fr";

        // Act
        var result = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(currentEntity, userEntities, currentUserEmail);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.FreeTrialEndDate);
        Assert.False(result.CanAccess);
        Assert.False(result.IsPennylaneSignatory);
        Assert.Null(result.Details);
        Assert.Null(result.BillingMonth);
    }

    [Fact]
    public void MapToSignatoryAccessModel_When_UserCannotAccessAndIsSignatory_Should_MapCorrectly()
    {
        // Arrange
        var currentEntity = _fixture.Build<SignatoryAccessEntity>()
            .With(s => s.CanAccess, false)
            .With(s => s.Email, "monemail@email.fr")
            .With(s => s.FreeTrialEndDate, new DateTime(2026, 10, 31))
            .Without(s => s.Wave)
            .Create();
        var userEntities = _fixture.CreateMany<Tuple<SignatoryAccessEntity, string>>(2).ToList();
        userEntities.Add(Tuple.Create(currentEntity, "accNumber"));
        var terms = _fixture.Create<TermsEntity>();
        var currentUserEmail = "monemail@email.fr";

        // Act
        var result = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(currentEntity, userEntities, currentUserEmail);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.CanAccess);
        Assert.True(result.IsPennylaneSignatory);
        Assert.NotNull(result.Details);
        Assert.Equal(currentEntity.FreeTrialEndDate, result.FreeTrialEndDate);
        Assert.Equal("novembre 2026", result.BillingMonth);
    }

    [Fact]
    public void MapToSignatoryAccessModel_When_EmailsDifferInCase_Should_RecognizeSignatory()
    {
        // Arrange
        var currentEntity = _fixture.Build<SignatoryAccessEntity>()
            .With(s => s.CanAccess, false)
            .With(s => s.Email, "MonEmail@Email.FR")
            .Create();
        var userEntities = _fixture.CreateMany<Tuple<SignatoryAccessEntity, string>>(2).ToList();
        userEntities.Add(Tuple.Create(currentEntity, "accNumber"));
        var terms = _fixture.Create<TermsEntity>();
        var currentUserEmail = "monemail@email.fr";

        // Act
        var result = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(currentEntity, userEntities, currentUserEmail);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.CanAccess);
        Assert.True(result.IsPennylaneSignatory);
        Assert.NotNull(result.Details);
    }

    [Fact]
    public void MapToSignatoryAccessModel_When_EmailsDifferInCase_And_CanAccess_Should_RecognizeSignatoryWithNoDetails()
    {
        // Arrange
        var currentEntity = _fixture.Build<SignatoryAccessEntity>()
            .With(s => s.CanAccess, true)
            .With(s => s.Email, "MonEmail@Email.FR")
            .Create();
        var userEntities = _fixture.CreateMany<Tuple<SignatoryAccessEntity, string>>(2).ToList();
        userEntities.Add(Tuple.Create(currentEntity, "accNumber"));
        var terms = _fixture.Create<TermsEntity>();
        var currentUserEmail = "monemail@email.fr";

        // Act
        var result = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(currentEntity, userEntities, currentUserEmail);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CanAccess);
        Assert.True(result.IsPennylaneSignatory);
        Assert.Null(result.Details);
    }

    [Fact]
    public void MapToSignatoryAccessModel_When_IsCurrentUserWithoutAccess_And_GetDetailsTrue_Should_MapDetailsAndBillingMonth()
    {
        // Arrange
        var currentUserEmail = "monemail@email.fr";
        var freeTrialEndDate = new DateTime(2026, 10, 31);

        var currentEntity = _fixture.Build<SignatoryAccessEntity>()
            .With(s => s.CanAccess, false)
            .With(s => s.Email, "toto@gmail.com")
            .With(s => s.FreeTrialEndDate, freeTrialEndDate)
            .Without(s => s.Wave)
            .Create();

        var userEntities = _fixture.CreateMany<Tuple<SignatoryAccessEntity, string>>(2).ToList();
        userEntities.Add(Tuple.Create(currentEntity, "accNumber"));

        // Act
        var result = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(currentEntity, userEntities, currentUserEmail, getDetails: true);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.CanAccess);
        Assert.False(result.IsPennylaneSignatory);
        Assert.NotNull(result.Details);
        Assert.NotNull(result.Details.Terms);
        Assert.NotNull(result.Details.Accounts);
        Assert.Equal(userEntities.Count, result.Details.Accounts.Count());
        Assert.Equal(freeTrialEndDate, result.FreeTrialEndDate);
        Assert.Null(result.BillingMonth);
    }

    [Fact]
    public void MapToTermsModel_Should_MapCorrectly()
    {
        var terms = _fixture.Create<TermsEntity>();

        var result = SignatoryAccessImportEntityMapper.MapToTermsModel(terms);

        Assert.NotNull(result);
        Assert.Equal(terms.Id, result.Id);
        Assert.Equal(terms.LinkFr, result.LinkFr);
    }

    [Fact]
    public void MapToAccountsModel_Should_MapCorrectly()
    {
        var userEntities = _fixture.CreateMany<Tuple<SignatoryAccessEntity, string>>(2).ToList();

        var result = SignatoryAccessImportEntityMapper.MapToAccountsModel(userEntities);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(userEntities.Count, result.Count());

        for (int i = 0; i < result.Count(); i++)
        {
            var userEntity = userEntities[i];
            var item = result.ElementAt(i);

            Assert.Equal(userEntity.Item1.AccountNumber, item.AccountNumber);
            Assert.Equal(userEntity.Item2, item.LegalName);
            Assert.Equal(userEntity.Item1.PlanName, item.PlanName);
            Assert.Equal(userEntity.Item1.PlanPriceExclTax, item.PlanPriceExclTax);
            Assert.Equal(userEntity.Item1.CompliancePriceExclTax, item.CompliancePriceExclTax);
        }
    }

    [Fact]
    public void MapToAccountsModel_WithEmptyUserEntities_Should_ReturnEmptyList()
    {
        var result = SignatoryAccessImportEntityMapper.MapToAccountsModel([]);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToEntity_Should_Map_All_Properties_Correctly()
    {
        // Arrange
        var date = new DateTime(2026, 10, 31);

        var row = _fixture.Build<SignatoryAccessImportRow>()
            .With(x => x.FreeTrialEndDate, date.ToString("dd/MM/yyyy"))
            .Create();

        // Act
        var entity = SignatoryAccessImportEntityMapper.ToEntity(row, 1);

        // Assert
        Assert.Equal(row.AccountNumber, entity.AccountNumber);
        Assert.Equal(row.Email, entity.Email);
        Assert.Equal(row.LastName, entity.LastName);
        Assert.Equal(row.FirstName, entity.FirstName);
        Assert.Equal(row.PlanName, entity.PlanName);
        Assert.Equal(row.UserCount, entity.UserCount);
        Assert.Equal(row.PlanPriceExclTax, entity.PlanPriceExclTax);
        Assert.Equal(row.CompliancePriceExclTax, entity.CompliancePriceExclTax);
        Assert.Equal(date, entity.FreeTrialEndDate);
        Assert.Null(entity.UpdatedDate);
        Assert.Equal(1, entity.TermsId);
    }

    [Fact]
    public void ToEntity_Should_Throw_When_Date_Format_Is_Invalid()
    {
        // Arrange
        var row = _fixture.Build<SignatoryAccessImportRow>()
            .With(x => x.FreeTrialEndDate, "2026-10-31")
            .Create();

        // Act & Assert
        Assert.Throws<FormatException>(() => SignatoryAccessImportEntityMapper.ToEntity(row, 1));
    }

    [Fact]
    public void ApplyUpdate_Should_Update_All_Fields()
    {
        // Arrange
        var existing = _fixture.Create<SignatoryAccessEntity>();

        var newDate = new DateTime(2026, 12, 31);

        var row = _fixture.Build<SignatoryAccessImportRow>()
            .With(x => x.FreeTrialEndDate, newDate.ToString("dd/MM/yyyy"))
            .Create();

        var updatedDateUtc = DateTime.UtcNow;

        // Act
        SignatoryAccessImportEntityMapper.ApplyUpdate(existing, row, updatedDateUtc, 1);

        // Assert
        Assert.Equal(row.Email, existing.Email);
        Assert.Equal(row.LastName, existing.LastName);
        Assert.Equal(row.FirstName, existing.FirstName);
        Assert.Equal(row.PlanName, existing.PlanName);
        Assert.Equal(row.UserCount, existing.UserCount);
        Assert.Equal(row.PlanPriceExclTax, existing.PlanPriceExclTax);
        Assert.Equal(row.CompliancePriceExclTax, existing.CompliancePriceExclTax);

        Assert.Equal(updatedDateUtc, existing.UpdatedDate);
        Assert.Equal(newDate, existing.FreeTrialEndDate);
        Assert.Equal(1, existing.TermsId);
    }

    [Fact]
    public void ApplyUpdate_Should_Throw_When_Date_Format_Is_Invalid()
    {
        // Arrange
        var existing = _fixture.Create<SignatoryAccessEntity>();

        var row = _fixture.Build<SignatoryAccessImportRow>()
            .With(x => x.FreeTrialEndDate, "31-12-2026") // mauvais format
            .Create();

        var updatedDateUtc = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<FormatException>(() => SignatoryAccessImportEntityMapper.ApplyUpdate(existing, row, updatedDateUtc, 1));
    }

    [Fact]
    public void ToImportRow_Should_MapCorrectly()
    {
        var signatoryAcces = _fixture.Build<SignatoryAccessEntity>()
            .With(e => e.FreeTrialEndDate, new DateTime(2026, 10, 31))
            .Create();

        var result = signatoryAcces.ToImportRow();

        Assert.NotNull(result);
        Assert.Equal(signatoryAcces.AccountNumber, result.AccountNumber);
        Assert.Equal(signatoryAcces.Email, result.Email);
        Assert.Equal(signatoryAcces.LastName, result.LastName);
        Assert.Equal(signatoryAcces.FirstName, result.FirstName);
        Assert.Equal(signatoryAcces.PlanName, result.PlanName);
        Assert.Equal(signatoryAcces.UserCount, result.UserCount);
        Assert.Equal(signatoryAcces.PlanPriceExclTax, result.PlanPriceExclTax);
        Assert.Equal(signatoryAcces.CompliancePriceExclTax, result.CompliancePriceExclTax);
        Assert.Equal(signatoryAcces.FreeTrialEndDate?.ToString("dd/MM/yyyy"), result.FreeTrialEndDate);
    }

    #region Wave Tests

    [Fact]
    public void ToImportRow_WhenWaveIsRestauration_ShouldMapToString()
    {
        var entity = _fixture.Build<SignatoryAccessEntity>()
            .With(e => e.Wave, "Restauration")
            .With(e => e.FreeTrialEndDate, new DateTime(2026, 10, 31))
            .Create();

        var result = entity.ToImportRow();

        Assert.Equal("Restauration", result.Wave);
    }

    [Fact]
    public void ToImportRow_WhenWaveIsNull_ShouldMapToNull()
    {
        var entity = _fixture.Build<SignatoryAccessEntity>()
            .With(e => e.Wave, (string?)null)
            .With(e => e.FreeTrialEndDate, new DateTime(2026, 10, 31))
            .Create();

        var result = entity.ToImportRow();

        Assert.Null(result.Wave);
    }

    [Fact]
    public void ToEntity_WhenWaveIsRestauration_ShouldMapToEnum()
    {
        var row = _fixture.Build<SignatoryAccessImportRow>()
            .With(x => x.FreeTrialEndDate, "31/10/2026")
            .With(x => x.Wave, "Restauration")
            .Create();

        var entity = SignatoryAccessImportEntityMapper.ToEntity(row, 1);

        Assert.Equal("Restauration", entity.Wave);
    }

    [Fact]
    public void ToEntity_WhenWaveIsNull_ShouldMapToNull()
    {
        var row = _fixture.Build<SignatoryAccessImportRow>()
            .With(x => x.FreeTrialEndDate, "31/10/2026")
            .With(x => x.Wave, (string?)null)
            .Create();

        var entity = SignatoryAccessImportEntityMapper.ToEntity(row, 1);

        Assert.Null(entity.Wave);
    }

    [Fact]
    public void ApplyUpdate_WhenWaveIsRestauration_ShouldUpdateWave()
    {
        var existing = _fixture.Build<SignatoryAccessEntity>()
            .With(e => e.Wave, (string?)null)
            .Create();

        var row = _fixture.Build<SignatoryAccessImportRow>()
            .With(x => x.FreeTrialEndDate, "31/10/2026")
            .With(x => x.Wave, "Restauration")
            .Create();

        SignatoryAccessImportEntityMapper.ApplyUpdate(existing, row, DateTime.UtcNow, 1);

        Assert.Equal("Restauration", existing.Wave);
    }

    [Fact]
    public void ApplyUpdate_WhenWaveIsNull_ShouldUpdateToNull()
    {
        var existing = _fixture.Build<SignatoryAccessEntity>()
            .With(e => e.Wave, "Restauration")
            .Create();

        var row = _fixture.Build<SignatoryAccessImportRow>()
            .With(x => x.FreeTrialEndDate, "31/10/2026")
            .With(x => x.Wave, (string?)null)
            .Create();

        SignatoryAccessImportEntityMapper.ApplyUpdate(existing, row, DateTime.UtcNow, 1);

        Assert.Null(existing.Wave);
    }

    [Fact]
    public void MapToSignatoryAccessModel_WhenWaveIsRestauration_ShouldMapToString()
    {
        var currentEntity = _fixture.Build<SignatoryAccessEntity>()
            .With(s => s.CanAccess, true)
            .With(s => s.Email, "email@test.com")
            .With(s => s.Wave, "Restauration")
            .Create();
        var userEntities = new List<Tuple<SignatoryAccessEntity, string>>();

        var result = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(currentEntity, userEntities, "other@test.com");

        Assert.Equal("Restauration", result.Wave);
    }

    [Fact]
    public void MapToSignatoryAccessModel_WhenWaveIsNull_ShouldMapToNull()
    {
        var currentEntity = _fixture.Build<SignatoryAccessEntity>()
            .With(s => s.CanAccess, true)
            .With(s => s.Email, "email@test.com")
            .With(s => s.Wave, (string?)null)
            .Create();
        var userEntities = new List<Tuple<SignatoryAccessEntity, string>>();

        var result = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(currentEntity, userEntities, "other@test.com");

        Assert.Null(result.Wave);
    }

    [Fact]
    public void MapToSignatoryAccessModel_WhenWaveIsNotNull_ShouldNotMapFreeTrialInfo()
    {
        var currentEntity = _fixture.Build<SignatoryAccessEntity>()
            .With(s => s.CanAccess, false)
            .With(s => s.Email, "email@test.com")
            .With(s => s.Wave, "Restauration")
            .Create();
        var userEntities = new List<Tuple<SignatoryAccessEntity, string>>();

        var result = SignatoryAccessImportEntityMapper.MapToSignatoryAccessModel(currentEntity, userEntities, "email@test.com");

        Assert.NotNull(result);
        Assert.Equal("Restauration", result.Wave);
        Assert.Null(result.FreeTrialEndDate);
        Assert.Null(result.BillingMonth);
    }

    #endregion
}
