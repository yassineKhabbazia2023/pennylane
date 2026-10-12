using FluentAssertions;
using Pennylane.WebApi.Models;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Pennylane.WebApi.Tests.Models
{
    public class CreateCompanyRequestTests
    {
        [Fact]
        public void AccountId_WhenSetToPositiveValue_ShouldBeValid()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = new List<int> { 1 }
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().BeEmpty();
        }

        [Fact]
        public void AccountId_WhenSetToZero_ShouldBeInvalid()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 0,
                Contacts = new List<int> { 1 }
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().ContainSingle()
                .Which.ErrorMessage.Should().Be("AccountId must be a positive integer.");
        }

        [Fact]
        public void AccountId_WhenSetToNegativeValue_ShouldBeInvalid()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = -1,
                Contacts = new List<int> { 1 }
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().ContainSingle()
                .Which.ErrorMessage.Should().Be("AccountId must be a positive integer.");
        }

        [Fact]
        public void AccountId_WhenSetToMaxValue_ShouldBeValid()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = int.MaxValue,
                Contacts = new List<int> { 1 }
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().BeEmpty();
        }

        [Fact]
        public void Contacts_WhenNull_ShouldBeInvalid()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = null!
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().ContainSingle()
                .Which.ErrorMessage.Should().Be("The Contacts field is required.");
        }

        [Fact]
        public void Contacts_WhenEmpty_ShouldBeValid()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = new List<int>()
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().BeEmpty();
        }

        [Fact]
        public void Contacts_WhenContainsValidIds_ShouldBeValid()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = new List<int> { 1, 2, 3, 100, int.MaxValue }
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().BeEmpty();
        }

        [Fact]
        public void Contacts_WhenContainsZero_ShouldBeInvalid()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = new List<int> { 1, 0, 3 }
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().ContainSingle()
                .Which.ErrorMessage.Should().Be("Contact ID at index 1 must be a positive integer.");
        }

        [Fact]
        public void Contacts_WhenContainsNegativeValue_ShouldBeInvalid()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = new List<int> { 1, -5, 3 }
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().ContainSingle()
                .Which.ErrorMessage.Should().Be("Contact ID at index 1 must be a positive integer.");
        }

        [Fact]
        public void Contacts_WhenContainsMultipleInvalidValues_ShouldReturnMultipleErrors()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = new List<int> { 0, 1, -1, 2, -5 }
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().HaveCount(3);
            validationResults.Should().Contain(v => v.ErrorMessage == "Contact ID at index 0 must be a positive integer.");
            validationResults.Should().Contain(v => v.ErrorMessage == "Contact ID at index 2 must be a positive integer.");
            validationResults.Should().Contain(v => v.ErrorMessage == "Contact ID at index 4 must be a positive integer.");
        }

        [Fact]
        public void Contacts_WhenExceedsMaxLength_ShouldBeInvalid()
        {
            // Arrange
            var tooManyContacts = Enumerable.Range(1, 101).ToList();
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = tooManyContacts
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().ContainSingle()
                .Which.ErrorMessage.Should().Be("Cannot add more than 100 contacts at once.");
        }

        [Fact]
        public void Contacts_WhenExactly100Items_ShouldBeValid()
        {
            // Arrange
            var contacts = Enumerable.Range(1, 100).ToList();
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = contacts
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().BeEmpty();
        }

        [Fact]
        public void Validate_WhenContactsIsNull_ShouldNotThrowException()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = null!
            };

            var validationContext = new ValidationContext(request);

            // Act
            var act = () => request.Validate(validationContext).ToList();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_WhenCalledDirectly_WithInvalidContactId_ShouldReturnValidationResult()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = new List<int> { 1, 0, 3 }
            };

            var validationContext = new ValidationContext(request);

            // Act
            var results = request.Validate(validationContext).ToList();

            // Assert
            results.Should().ContainSingle();
            results[0].ErrorMessage.Should().Be("Contact ID at index 1 must be a positive integer.");
            results[0].MemberNames.Should().ContainSingle().Which.Should().Be("Contacts[1]");
        }

        [Fact]
        public void Validate_WhenCalledDirectly_WithValidContactIds_ShouldReturnEmptyResults()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = new List<int> { 1, 2, 3 }
            };

            var validationContext = new ValidationContext(request);

            // Act
            var results = request.Validate(validationContext).ToList();

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public void Contacts_DefaultValue_ShouldBeEmptyList()
        {
            // Arrange & Act
            var request = new CreateCompanyRequest();

            // Assert
            request.Contacts.Should().NotBeNull();
            request.Contacts.Should().BeEmpty();
        }

        [Fact]
        public void AccountId_DefaultValue_ShouldBeZero()
        {
            // Arrange & Act
            var request = new CreateCompanyRequest();

            // Assert
            request.AccountId.Should().Be(0);
        }

        [Fact]
        public void CreateCompanyRequest_WhenBothPropertiesAreValid_ShouldPassValidation()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 456,
                Contacts = new List<int> { 10, 20, 30 }
            };

            // Act
            var validationResults = ValidateModel(request);

            // Assert
            validationResults.Should().BeEmpty();
        }

        [Fact]
        public void Validate_WhenContactIdEqualsIntMaxValue_ShouldBeValid()
        {
            // Arrange
            var request = new CreateCompanyRequest
            {
                AccountId = 123,
                Contacts = new List<int> { int.MaxValue }
            };

            var validationContext = new ValidationContext(request);

            // Act
            var results = request.Validate(validationContext).ToList();

            // Assert
            results.Should().BeEmpty();
        }

        private static List<ValidationResult> ValidateModel(object model)
        {
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);
            return validationResults;
        }
    }
}
