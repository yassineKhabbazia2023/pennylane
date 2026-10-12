using AutoFixture;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Helpers;
using Pennylane.Application.Models.Signatory;
using Pennylane.Domain.Entities.Pennylane;
using System.Globalization;

namespace Pennylane.Application.Tests.Helpers;

public class SignatoryAccessImportHelperTests
{
    private readonly Fixture _fixture;

    public SignatoryAccessImportHelperTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [Fact]
    public void ValidateRow_WhenValidRow_ShouldReturnEmptyList()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "31/10/2026",
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRow_WhenAccountNumberIsNull_ShouldReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = null!,
            Email = "test@example.com",
            FreeTrialEndDate = "31/10/2026",
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Single(errors);
        Assert.Equal("Le Code dossier est une colonne obligatoire.", errors[0].ErrorMessage);
        Assert.Equal("Code dossier", errors[0].ColumnName);
    }

    [Fact]
    public void ValidateRow_WhenAccountNumberIsEmpty_ShouldReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "",
            Email = "test@example.com",
            FreeTrialEndDate = "31/10/2026",
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Single(errors);
        Assert.Equal("Le Code dossier est une colonne obligatoire.", errors[0].ErrorMessage);
    }

    [Fact]
    public void ValidateRow_WhenAccountNumberIsStringNULL_ShouldReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "NULL",
            Email = "test@example.com",
            FreeTrialEndDate = "31/10/2026",
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Single(errors);
        Assert.Equal("Le Code dossier ne peut pas être 'NULL'.", errors[0].ErrorMessage);
    }

    [Theory]
    [InlineData("toto@@test")]
    [InlineData("test.com")]
    [InlineData("test@com")]
    public void ValidateRow_WhenEmailIsInvalid_ShouldReturnError(string invalidEmail)
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = invalidEmail,
            FreeTrialEndDate = "31/10/2026",
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Single(errors);
        Assert.Equal("Email invalide.", errors[0].ErrorMessage);
        Assert.Equal("Email signataire", errors[0].ColumnName);
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("test.user@domain.fr")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NULL")]
    public void ValidateRow_WhenEmailIsValidOrEmpty_ShouldReturnEmptyList(string? validEmail)
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = validEmail,
            FreeTrialEndDate = "31/10/2026",
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRow_WhenPlanPriceExclTaxIsNegative_ShouldReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            PlanPriceExclTax = -10,
            FreeTrialEndDate = "31/10/2026",
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Single(errors);
        Assert.Equal("Le prix HT du plan ne peut pas être négatif.", errors[0].ErrorMessage);
    }

    [Fact]
    public void ValidateRow_WhenAllowedDatesIsNotSet_ShouldReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            PlanPriceExclTax = 10,
            FreeTrialEndDate = "31/10/2026",
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, []);

        // Assert
        Assert.Single(errors);
        Assert.Equal("Date de fin de gratuité", errors[0].ColumnName);
        Assert.Equal("Date de fin de gratuité non correcte.", errors[0].ErrorMessage);
    }

    [Fact]
    public void ValidateRow_WhenDateHasInvalidFormat_ShouldReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            PlanPriceExclTax = 10,
            FreeTrialEndDate = "2026-10-31",
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Single(errors);
        Assert.Equal("Date de fin de gratuité", errors[0].ColumnName);
        Assert.Equal("Date de fin de gratuité non correcte.", errors[0].ErrorMessage);
    }

    [Fact]
    public void ValidateRow_WhenDateIsNotAllowed_ShouldReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            PlanPriceExclTax = 10,
            FreeTrialEndDate = "01/11/2026",
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Single(errors);
        Assert.Equal("Date de fin de gratuité", errors[0].ColumnName);
        Assert.Equal("Date de fin de gratuité non correcte.", errors[0].ErrorMessage);
    }

    [Fact]
    public void AddError_ShouldAddErrorToList()
    {
        // Arrange
        var result = new SignatoryAccessImportResponse();
        var rowData = new SignatoryAccessImportRow { AccountNumber = "ACC001", FreeTrialEndDate = "31/10/2026" };

        // Act
        result.AddError(5, "Test error", "Test column", rowData);

        // Assert
        Assert.Single(result.Errors);
        Assert.Equal(5, result.Errors[0].RowNumber);
        Assert.Equal("Test error", result.Errors[0].ErrorMessage);
        Assert.Equal("Test column", result.Errors[0].ColumnName);
        Assert.Equal("ACC001", result.Errors[0].RowData?.AccountNumber);
    }

    [Fact]
    public void HasChanges_WhenNoChanges_ShouldReturnFalse()
    {
        // Arrange
        var existing = new Domain.Entities.Pennylane.SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = DateTime.ParseExact("31/10/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture)
        };

        var dto = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "31/10/2026",
        };

        // Act
        var hasChanges = SignatoryAccessImportHelper.HasChanges(existing, dto);

        // Assert
        Assert.False(hasChanges);
    }

    public static TheoryData<SignatoryAccessEntity, SignatoryAccessImportRow> ChangedCases
    {
        get
        {
            Fixture fixture = new();
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            var data = new TheoryData<SignatoryAccessEntity, SignatoryAccessImportRow>();
            var existing = fixture.Build<SignatoryAccessEntity>()
                .With(e => e.FreeTrialEndDate, new DateTime(2026, 10, 31))
                .Create();

            SignatoryAccessImportRow BaseDto() => new()
            {
                AccountNumber = existing.AccountNumber,
                Email = existing.Email,
                LastName = existing.LastName,
                FirstName = existing.FirstName,
                PlanName = existing.PlanName,
                UserCount = existing.UserCount,
                PlanPriceExclTax = existing.PlanPriceExclTax,
                CompliancePriceExclTax = existing.CompliancePriceExclTax,
                FreeTrialEndDate = existing.FreeTrialEndDate?.ToString("dd/MM/yyyy")
            };

            // Email modifié
            var emailChanged = BaseDto();
            emailChanged.Email = "different@email.com";
            data.Add(existing, emailChanged);

            // LastName modifié
            var lastNameChanged = BaseDto();
            lastNameChanged.LastName = "Different";
            data.Add(existing, lastNameChanged);

            // FirstName modifié
            var firstNameChanged = BaseDto();
            firstNameChanged.FirstName = "Different";
            data.Add(existing, firstNameChanged);

            // PlanName modifié
            var planNameChanged = BaseDto();
            planNameChanged.PlanName = "DifferentPlan";
            data.Add(existing, planNameChanged);

            // UserCount modifié
            var userCountChanged = BaseDto();
            userCountChanged.UserCount = existing.UserCount + 1;
            data.Add(existing, userCountChanged);

            // PlanPrice modifié
            var planPriceChanged = BaseDto();
            planPriceChanged.PlanPriceExclTax = existing.PlanPriceExclTax + 5;
            data.Add(existing, planPriceChanged);

            // CompliancePrice modifié
            var complianceChanged = BaseDto();
            complianceChanged.CompliancePriceExclTax = existing.CompliancePriceExclTax + 5;
            data.Add(existing, complianceChanged);

            // Date modifiée
            var dateChanged = BaseDto();
            var newDate = existing.FreeTrialEndDate!.Value.AddDays(1);
            dateChanged.FreeTrialEndDate = newDate.ToString("dd/MM/yyyy");
            data.Add(existing, dateChanged);

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ChangedCases))]
    public void HasChanges_Should_Return_True_When_Any_Field_Is_Modified(
        SignatoryAccessEntity existing,
        SignatoryAccessImportRow dto)
    {
        // Act
        var result = SignatoryAccessImportHelper.HasChanges(existing, dto);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateEmailFreeTrialConsistency_Should_Add_Error_When_Same_Email_Has_Different_Dates()
    {
        // Arrange
        var email = "test@email.com";

        var row1 = _fixture.Build<SignatoryAccessImportRow>()
            .With(x => x.Email, email)
            .With(x => x.FreeTrialEndDate, "30/06/2026")
            .Create();

        var row2 = _fixture.Build<SignatoryAccessImportRow>()
            .With(x => x.Email, email)
            .With(x => x.FreeTrialEndDate, "31/12/2026")
            .Create();

        var rows = new List<SignatoryAccessImportRow> { row1, row2 };
        var result = new SignatoryAccessImportResponse();

        // Act
        SignatoryAccessImportHelper.ValidateEmailFreeTrialConsistency(rows, result);

        // Assert
        Assert.NotEmpty(result.Errors);
        Assert.Equal(2, result.Errors.Count);

        Assert.All(result.Errors, e =>
        {
            Assert.Equal("Date de fin de gratuité", e.ColumnName);
            Assert.Contains("même email", e.ErrorMessage);
        });
    }

    [Fact]
    public void ValidateEmailFreeTrialConsistency_Should_Not_Add_Error_When_Dates_Are_Identical()
    {
        var email = "test@email.com";

        var row1 = new SignatoryAccessImportRow
        {
            Email = email,
            FreeTrialEndDate = "30/06/2026"
        };

        var row2 = new SignatoryAccessImportRow
        {
            Email = email,
            FreeTrialEndDate = "30/06/2026"
        };

        var rows = new List<SignatoryAccessImportRow> { row1, row2 };
        var result = new SignatoryAccessImportResponse();

        SignatoryAccessImportHelper.ValidateEmailFreeTrialConsistency(rows, result);

        Assert.Empty(result.Errors);
    }

    private static FreeTrialInfoOptions BuildFreeTrialOptions(string freeTrialEndDate, string effectiveFromUtc)
        => new()
        {
            TermsOverridesAfterDate = new Dictionary<string, TermsOverrideOptions>
            {
                [freeTrialEndDate] = new TermsOverrideOptions
                {
                    EffectiveFromUtc = effectiveFromUtc,
                    LinkFr = "nouvelles-cgu.pdf"
                }
            }
        };

    [Theory]
    [InlineData("01/01/2020")]
    [InlineData("01/01/2099")]
    public void IsFreeTrialExpired_WhenCanAccessIsTrue_ShouldReturnNull(string effectiveFromUtc)
    {
        // Arrange
        var options = BuildFreeTrialOptions("30062026", effectiveFromUtc);

        // Act
        var result = SignatoryAccessImportHelper.IsFreeTrialExpired("30/06/2026", true, options, null);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void IsFreeTrialExpired_WhenAccessNotGranted_AndOverrideEffectiveDatePassed_ShouldReturnTrue(bool? canAccess)
    {
        // Arrange
        var options = BuildFreeTrialOptions("30062026", "01/01/2020");

        // Act
        var result = SignatoryAccessImportHelper.IsFreeTrialExpired("30/06/2026", canAccess, options, null);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void IsFreeTrialExpired_WhenAccessNotGranted_AndOverrideEffectiveDateInFuture_ShouldReturnFalse(bool? canAccess)
    {
        // Arrange
        var options = BuildFreeTrialOptions("30062026", "01/01/2099");

        // Act
        var result = SignatoryAccessImportHelper.IsFreeTrialExpired("30/06/2026", canAccess, options, null);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void IsFreeTrialExpired_WhenAccessNotGranted_AndNoOverrideForDate_ShouldReturnFalse(bool? canAccess)
    {
        // Arrange : un override existe mais pour une autre date
        var options = BuildFreeTrialOptions("30062026", "01/01/2020");

        // Act
        var result = SignatoryAccessImportHelper.IsFreeTrialExpired("31/10/2026", canAccess, options, null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFreeTrialExpired_WhenNoOverridesConfigured_ShouldReturnFalse()
    {
        // Arrange
        var options = new FreeTrialInfoOptions();

        // Act
        var result = SignatoryAccessImportHelper.IsFreeTrialExpired("30/06/2026", false, options, null);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("01/01/2020", true)]
    [InlineData("01/01/2099", false)]
    public void IsFreeTrialExpiredByDate_ShouldReflectOverrideEffectiveDate(string effectiveFromUtc, bool expected)
    {
        // Arrange
        var options = BuildFreeTrialOptions("30062026", effectiveFromUtc);

        // Act
        var result = SignatoryAccessImportHelper.IsFreeTrialExpiredByDate("30/06/2026", options);

        // Assert
        Assert.Equal(expected, result);
    }

    #region Wave (Vague) Validation Tests

    [Theory]
    [InlineData("Restauration")]
    public void ValidateRow_WaveRestauration_Accepted(string wave)
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "D001",
            Email = "test@test.com",
            LastName = "Dupont",
            FirstName = "Jean",
            PlanName = "Plan Pro",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "30/06/2026",
            Wave = wave
        };
        var allowedDates = new[] { DateOnly.ParseExact("30/06/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture) };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, allowedDates);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("restauration")]
    [InlineData("RESTAURATION")]
    [InlineData("ReStAuRaTiOn")]
    [InlineData(" Restauration ")]
    public void ValidateRow_WaveCaseSensitive_Rejected(string wave)
    {
        // Arrange - Wave avec mauvaise casse doit être rejetée
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "D001",
            Email = "test@test.com",
            LastName = "Dupont",
            FirstName = "Jean",
            PlanName = "Plan Pro",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "30/06/2026",
            Wave = wave
        };
        var allowedDates = new[] { DateOnly.ParseExact("30/06/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture) };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, allowedDates);

        // Assert
        Assert.NotEmpty(errors);
        var waveError = errors.FirstOrDefault(e => e.ColumnName.Contains("Vague"));
        Assert.NotNull(waveError);
        Assert.Contains("Restauration", waveError!.ErrorMessage);
    }

    [Fact]
    public void ValidateRow_WaveEmpty_Accepted()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "D001",
            Email = "test@test.com",
            LastName = "Dupont",
            FirstName = "Jean",
            PlanName = "Plan Pro",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "30/06/2026",
            Wave = "Restauration"
        };
        var allowedDates = new[] { DateOnly.ParseExact("30/06/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture) };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, allowedDates);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRow_WaveNull_Accepted()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "D001",
            Email = "test@test.com",
            LastName = "Dupont",
            FirstName = "Jean",
            PlanName = "Plan Pro",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "30/06/2026",
            Wave = null
        };
        var allowedDates = new[] { DateOnly.ParseExact("30/06/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture) };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, allowedDates);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Autre")]
    [InlineData("Commercial")]
    [InlineData("Café")]
    [InlineData("123")]
    public void ValidateRow_WaveInvalid_Rejected(string wave)
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "D001",
            Email = "test@test.com",
            LastName = "Dupont",
            FirstName = "Jean",
            PlanName = "Plan Pro",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "30/06/2026",
            Wave = wave
        };
        var allowedDates = new[] { DateOnly.ParseExact("30/06/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture) };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, allowedDates);

        // Assert
        Assert.NotEmpty(errors);
        var waveError = errors.FirstOrDefault(e => e.ColumnName.Contains("Vague"));
        Assert.NotNull(waveError);
        Assert.Contains("Restauration", waveError!.ErrorMessage);
    }

    #endregion

    #region FreeTrialEndDate Validation Tests

    [Fact]
    public void ValidateRow_WhenFreeTrialEndDateIsProvidedAndNotInAllowedDates_ShouldReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "15/11/2026"
        };
        var allowedDates = new[]
        {
            DateOnly.ParseExact("30/06/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture),
            DateOnly.ParseExact("31/10/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture)
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, allowedDates);

        // Assert
        Assert.Single(errors);
        Assert.Equal("Date de fin de gratuité", errors[0].ColumnName);
        Assert.Equal("Date de fin de gratuité non correcte.", errors[0].ErrorMessage);
        Assert.Equal(2, errors[0].RowNumber);
        Assert.Equal(row, errors[0].RowData);
    }

    [Fact]
    public void ValidateRow_WhenFreeTrialEndDateIsProvidedAndInAllowedDates_ShouldNotReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "31/10/2026"
        };
        var allowedDates = new[]
        {
            DateOnly.ParseExact("30/06/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture),
            DateOnly.ParseExact("31/10/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture)
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, allowedDates);

        // Assert
        Assert.Empty(errors);
    }

    #endregion

    #region ValidateWaveConsistency Tests

    [Fact]
    public void ValidateWaveConsistency_WhenSameEmailHasDifferentWaves_ShouldAddError()
    {
        // Arrange
        var email = "test@email.com";
        var row1 = new SignatoryAccessImportRow
        {
            AccountNumber = "D001",
            Email = email,
            Wave = "Restauration"
        };
        var row2 = new SignatoryAccessImportRow
        {
            AccountNumber = "D002",
            Email = email,
            Wave = null
        };
        var rows = new List<SignatoryAccessImportRow> { row1, row2 };
        var result = new SignatoryAccessImportResponse();

        // Act
        SignatoryAccessImportHelper.ValidateWaveConsistency(rows, result);

        // Assert
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Vagues incohérentes", result.Errors[0].ErrorMessage);
        Assert.Equal("Vague", result.Errors[0].ColumnName);
    }

    [Fact]
    public void ValidateWaveConsistency_WhenSameEmailHasSameWave_ShouldNotAddError()
    {
        // Arrange
        var email = "test@email.com";
        var row1 = new SignatoryAccessImportRow
        {
            AccountNumber = "D001",
            Email = email,
            Wave = "Restauration"
        };
        var row2 = new SignatoryAccessImportRow
        {
            AccountNumber = "D002",
            Email = email,
            Wave = "Restauration"
        };
        var rows = new List<SignatoryAccessImportRow> { row1, row2 };
        var result = new SignatoryAccessImportResponse();

        // Act
        SignatoryAccessImportHelper.ValidateWaveConsistency(rows, result);

        // Assert
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateWaveConsistency_WhenSameEmailHasSameNullWave_ShouldNotAddError()
    {
        // Arrange
        var email = "test@email.com";
        var row1 = new SignatoryAccessImportRow
        {
            AccountNumber = "D001",
            Email = email,
            Wave = null
        };
        var row2 = new SignatoryAccessImportRow
        {
            AccountNumber = "D002",
            Email = email,
            Wave = null
        };
        var rows = new List<SignatoryAccessImportRow> { row1, row2 };
        var result = new SignatoryAccessImportResponse();

        // Act
        SignatoryAccessImportHelper.ValidateWaveConsistency(rows, result);

        // Assert
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateWaveConsistency_WhenSingleRowPerEmail_ShouldNotAddError()
    {
        // Arrange
        var row1 = new SignatoryAccessImportRow
        {
            AccountNumber = "D001",
            Email = "email1@test.com",
            Wave = "Restauration"
        };
        var row2 = new SignatoryAccessImportRow
        {
            AccountNumber = "D002",
            Email = "email2@test.com",
            Wave = null
        };
        var rows = new List<SignatoryAccessImportRow> { row1, row2 };
        var result = new SignatoryAccessImportResponse();

        // Act
        SignatoryAccessImportHelper.ValidateWaveConsistency(rows, result);

        // Assert
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateWaveConsistency_WhenEmailIsEmpty_ShouldIgnoreRow()
    {
        // Arrange
        var row1 = new SignatoryAccessImportRow
        {
            AccountNumber = "D001",
            Email = "",
            Wave = "Restauration"
        };
        var row2 = new SignatoryAccessImportRow
        {
            AccountNumber = "D002",
            Email = null,
            Wave = null
        };
        var rows = new List<SignatoryAccessImportRow> { row1, row2 };
        var result = new SignatoryAccessImportResponse();

        // Act
        SignatoryAccessImportHelper.ValidateWaveConsistency(rows, result);

        // Assert
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateWaveConsistency_WhenEmailCaseDiffers_ShouldGroupCorrectly()
    {
        // Arrange
        var row1 = new SignatoryAccessImportRow
        {
            AccountNumber = "D001",
            Email = "TEST@email.com",
            Wave = "Restauration"
        };
        var row2 = new SignatoryAccessImportRow
        {
            AccountNumber = "D002",
            Email = "test@EMAIL.com",
            Wave = null
        };
        var rows = new List<SignatoryAccessImportRow> { row1, row2 };
        var result = new SignatoryAccessImportResponse();

        // Act
        SignatoryAccessImportHelper.ValidateWaveConsistency(rows, result);

        // Assert
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Vagues incohérentes", result.Errors[0].ErrorMessage);
    }

    #endregion

    #region IsFreeTrialExpired with Wave Tests

    [Theory]
    [InlineData("Restauration")]
    public void IsFreeTrialExpired_WhenWaveIsSet_ShouldReturnNull(string wave)
    {
        // Arrange
        var options = BuildFreeTrialOptions("30062026", "01/01/2020");

        // Act
        var result = SignatoryAccessImportHelper.IsFreeTrialExpired("30/06/2026", false, options, wave);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void IsFreeTrialExpired_WhenWaveIsNullAndCanAccessFalse_ShouldEvaluateNormally()
    {
        // Arrange
        var options = BuildFreeTrialOptions("30062026", "01/01/2020");

        // Act
        var result = SignatoryAccessImportHelper.IsFreeTrialExpired("30/06/2026", false, options, null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsFreeTrialExpired_WhenWaveIsEmptyAndCanAccessFalse_ShouldEvaluateNormally()
    {
        // Arrange
        var options = BuildFreeTrialOptions("30062026", "01/01/2020");

        // Act
        var result = SignatoryAccessImportHelper.IsFreeTrialExpired("30/06/2026", false, options, "");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsFreeTrialExpired_WhenWaveAndCanAccessBothSet_ShouldReturnNull()
    {
        // Arrange
        var options = BuildFreeTrialOptions("30062026", "01/01/2020");

        // Act
        var result = SignatoryAccessImportHelper.IsFreeTrialExpired("30/06/2026", true, options, "Restauration");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region ValidateRow FreeTrialEndDate and Wave Validation Tests

    [Fact]
    public void ValidateRow_WhenBothFreeTrialEndDateAndWaveAreEmpty_ShouldReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            FreeTrialEndDate = null,
            Wave = null
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Single(errors);
        Assert.Equal("Date de fin de gratuité/Vague", errors[0].ColumnName);
        Assert.Contains("La date de fin de gratuité ou la vague doit être renseignée", errors[0].ErrorMessage);
    }

    [Fact]
    public void ValidateRow_WhenOnlyWaveIsSet_ShouldNotReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            FreeTrialEndDate = null,
            Wave = "Restauration"
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRow_WhenOnlyFreeTrialEndDateIsSet_ShouldNotReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            FreeTrialEndDate = "31/10/2026",
            Wave = null
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 10, 31)]);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRow_WhenBothFreeTrialEndDateAndWaveAreSet_ShouldNotReturnError()
    {
        // Arrange
        var row = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            FreeTrialEndDate = "30/06/2026",
            Wave = "Restauration"
        };

        // Act
        var errors = SignatoryAccessImportHelper.ValidateRow(row, 2, [new DateOnly(2026, 6, 30)]);

        // Assert
        Assert.Empty(errors);
    }

    #endregion

    #region HasChanges with Wave Tests

    [Fact]
    public void HasChanges_WhenWaveIsModified_ShouldReturnTrue()
    {
        // Arrange
        var existing = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = DateTime.ParseExact("31/10/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture),
            Wave = null
        };

        var dto = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "31/10/2026",
            Wave = "Restauration"
        };

        // Act
        var hasChanges = SignatoryAccessImportHelper.HasChanges(existing, dto);

        // Assert
        Assert.True(hasChanges);
    }

    [Fact]
    public void HasChanges_WhenWaveIsNotModified_ShouldReturnFalse()
    {
        // Arrange
        var existing = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = DateTime.ParseExact("31/10/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture),
            Wave = "Restauration"
        };

        var dto = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "31/10/2026",
            Wave = "Restauration"
        };

        // Act
        var hasChanges = SignatoryAccessImportHelper.HasChanges(existing, dto);

        // Assert
        Assert.False(hasChanges);
    }

    [Fact]
    public void HasChanges_WhenWaveChangedFromValueToNull_ShouldReturnTrue()
    {
        // Arrange
        var existing = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = DateTime.ParseExact("31/10/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture),
            Wave = "Restauration"
        };

        var dto = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "31/10/2026",
            Wave = null
        };

        // Act
        var hasChanges = SignatoryAccessImportHelper.HasChanges(existing, dto);

        // Assert
        Assert.True(hasChanges);
    }

    [Fact]
    public void HasChanges_WhenBothWavesAreNull_ShouldReturnFalse()
    {
        // Arrange
        var existing = new SignatoryAccessEntity
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = DateTime.ParseExact("31/10/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture),
            Wave = null
        };

        var dto = new SignatoryAccessImportRow
        {
            AccountNumber = "ACC001",
            Email = "test@example.com",
            LastName = "Doe",
            FirstName = "John",
            PlanName = "Premium",
            UserCount = 5,
            PlanPriceExclTax = 100,
            CompliancePriceExclTax = 50,
            FreeTrialEndDate = "31/10/2026",
            Wave = null
        };

        // Act
        var hasChanges = SignatoryAccessImportHelper.HasChanges(existing, dto);

        // Assert
        Assert.False(hasChanges);
    }

    #endregion
}
