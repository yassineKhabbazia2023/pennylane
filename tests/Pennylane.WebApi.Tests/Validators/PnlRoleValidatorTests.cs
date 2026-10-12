using FluentAssertions;
using FluentValidation.TestHelper;
using Pennylane.Application.Models.Pennylane;
using Pennylane.WebApi.Validators;

namespace Pennylane.WebApi.Tests.Validators;

public class PnlRoleValidatorTests
{
    private readonly PnlRoleValidator _validator;

    public PnlRoleValidatorTests()
    {
        _validator = new PnlRoleValidator();
    }

    #region AccountId Tests

    [Fact]
    public void Validate_WhenAccountIdIsZero_ShouldHaveError()
    {
        // Arrange
        var model = new PnlRole { AccountId = 0, ContactId = 1, Role = "employee" };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AccountId)
            .WithErrorMessage("AccountId must be greater than 0.");
    }

    [Fact]
    public void Validate_WhenAccountIdIsNegative_ShouldHaveError()
    {
        // Arrange
        var model = new PnlRole { AccountId = -1, ContactId = 1, Role = "employee" };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AccountId)
            .WithErrorMessage("AccountId must be greater than 0.");
    }

    [Fact]
    public void Validate_WhenAccountIdIsPositive_ShouldNotHaveError()
    {
        // Arrange
        var model = new PnlRole { AccountId = 1, ContactId = 1, Role = "employee" };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.AccountId);
    }

    #endregion

    #region ContactId Tests

    [Fact]
    public void Validate_WhenContactIdIsZero_ShouldHaveError()
    {
        // Arrange
        var model = new PnlRole { AccountId = 1, ContactId = 0, Role = "employee" };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId)
            .WithErrorMessage("ContactId must be greater than 0.");
    }

    [Fact]
    public void Validate_WhenContactIdIsNegative_ShouldHaveError()
    {
        // Arrange
        var model = new PnlRole { AccountId = 1, ContactId = -5, Role = "employee" };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId)
            .WithErrorMessage("ContactId must be greater than 0.");
    }

    [Fact]
    public void Validate_WhenContactIdIsPositive_ShouldNotHaveError()
    {
        // Arrange
        var model = new PnlRole { AccountId = 1, ContactId = 123, Role = "employee" };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ContactId);
    }

    #endregion

    #region Role Tests

    [Theory]
    [InlineData("employee")]
    [InlineData("executive")]
    [InlineData("internal_accountant")]
    [InlineData("invoicing_manager")]
    [InlineData("purchasing_manager")]
    [InlineData("sales_manager")]
    public void Validate_WhenRoleIsValid_ShouldNotHaveError(string role)
    {
        // Arrange
        var model = new PnlRole { AccountId = 1, ContactId = 1, Role = role };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Role);
    }

    [Theory]
    [InlineData("EMPLOYEE")]
    [InlineData("Executive")]
    [InlineData("INTERNAL_ACCOUNTANT")]
    public void Validate_WhenRoleIsValidWithDifferentCase_ShouldNotHaveError(string role)
    {
        // Arrange
        var model = new PnlRole { AccountId = 1, ContactId = 1, Role = role };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Role);
    }

    [Theory]
    [InlineData("invalid_role")]
    [InlineData("admin")]
    [InlineData("client")]
    [InlineData("accountant")]
    [InlineData("manager")]
    public void Validate_WhenRoleIsInvalid_ShouldHaveError(string role)
    {
        // Arrange
        var model = new PnlRole { AccountId = 1, ContactId = 1, Role = role };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Role);
        result.Errors
            .Where(e => e.PropertyName == "Role")
            .Should().Contain(e => e.ErrorMessage.Contains($"The role '{role}' is not valid"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WhenRoleIsNullOrEmpty_ShouldHaveError(string? role)
    {
        // Arrange
        var model = new PnlRole { AccountId = 1, ContactId = 1, Role = role! };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }

    #endregion

    #region Combined Validation Tests

    [Fact]
    public void Validate_WhenAllFieldsAreValid_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var model = new PnlRole { AccountId = 123, ContactId = 456, Role = "executive" };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenAllFieldsAreInvalid_ShouldHaveMultipleErrors()
    {
        // Arrange
        var model = new PnlRole { AccountId = 0, ContactId = -1, Role = "invalid" };

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
        result.ShouldHaveValidationErrorFor(x => x.AccountId);
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }

    #endregion
}
