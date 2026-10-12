using Application.Models.Contacts;
using AutoFixture;
using Application.Mappers;
using Pennylane.Domain.Entities;

namespace Pennylane.Application.Tests.Mappers;

public class MapContactsTests
{
    private readonly Fixture _fixture;

    public MapContactsTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    #region MapContactModelToEntity Tests

    [Fact]
    public void MapContactModelToEntity_WhenContactIsValid_ShouldMapCorrectly()
    {
        // Arrange
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 123)
            .With(c => c.ContactGlobalUniqueId, Guid.NewGuid())
            .With(c => c.Email, "test@example.com")
            .With(c => c.Type, "Collaborator")
            .With(c => c.FirstName, "John")
            .With(c => c.LastName, "Doe")
            .Create();

        // Act
        var result = contact.MapContactModelToEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(contact.ContactId, result.ContactId);
        Assert.Equal(contact.ContactGlobalUniqueId, result.ContactGlobalUniqueId);
        Assert.Equal(contact.Email, result.Email);
        Assert.Equal(contact.Type, result.Type);
        Assert.Equal(contact.FirstName, result.FirstName);
        Assert.Equal(contact.LastName, result.LastName);
    }

    [Fact]
    public void MapContactModelToEntity_WhenContactIsNull_ShouldReturnNull()
    {
        // Arrange
        Contact? contact = null;

        // Act
        var result = contact.MapContactModelToEntity();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MapContactModelToEntity_WhenContactGuidIsNull_ShouldMapCorrectly()
    {
        // Arrange
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactGlobalUniqueId, (Guid?)null)
            .Create();

        // Act
        var result = contact.MapContactModelToEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.ContactGlobalUniqueId);
        Assert.Equal(contact.ContactId, result.ContactId);
        Assert.Equal(contact.Email, result.Email);
    }

    [Fact]
    public void MapContactModelToEntity_WhenTypeIsNull_ShouldMapCorrectly()
    {
        // Arrange
        var contact = _fixture.Build<Contact>()
            .With(c => c.Type, (string?)null)
            .Create();

        // Act
        var result = contact.MapContactModelToEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Type);
        Assert.Equal(contact.ContactId, result.ContactId);
        Assert.Equal(contact.Email, result.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Administrator")]
    [InlineData("Collaborator")]
    [InlineData("External")]
    public void MapContactModelToEntity_WithDifferentTypes_ShouldMapCorrectly(string? type)
    {
        // Arrange
        var contact = _fixture.Build<Contact>()
            .With(c => c.Type, type)
            .Create();

        // Act
        var result = contact.MapContactModelToEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(type, result.Type);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("John", "")]
    [InlineData("", "Doe")]
    [InlineData("Jean-Claude", "Van Damme")]
    public void MapContactModelToEntity_WithDifferentNames_ShouldMapCorrectly(string firstName, string lastName)
    {
        // Arrange
        var contact = _fixture.Build<Contact>()
            .With(c => c.FirstName, firstName)
            .With(c => c.LastName, lastName)
            .Create();

        // Act
        var result = contact.MapContactModelToEntity();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(firstName, result.FirstName);
        Assert.Equal(lastName, result.LastName);
    }

    #endregion

    #region MapContactEntityToModel Tests

    [Fact]
    public void MapContactEntityToModel_WhenContactEntityIsValid_ShouldMapCorrectly()
    {
        // Arrange
        var contactEntity = _fixture.Build<ContactEntity>()
            .With(c => c.ContactId, 456)
            .With(c => c.ContactGlobalUniqueId, Guid.NewGuid())
            .With(c => c.Email, "entity@example.com")
            .With(c => c.Type, "Manager")
            .With(c => c.FirstName, "Jane")
            .With(c => c.LastName, "Smith")
            .Create();

        // Act
        var result = contactEntity.MapContactEntityToModel();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(contactEntity.ContactId, result.ContactId);
        Assert.Equal(contactEntity.ContactGlobalUniqueId, result.ContactGlobalUniqueId);
        Assert.Equal(contactEntity.Email, result.Email);
        Assert.Equal(contactEntity.Type, result.Type);
        Assert.Equal(contactEntity.FirstName, result.FirstName);
        Assert.Equal(contactEntity.LastName, result.LastName);
    }

    [Fact]
    public void MapContactEntityToModel_WhenContactEntityIsNull_ShouldReturnNull()
    {
        // Arrange
        ContactEntity? contactEntity = null;

        // Act
        var result = contactEntity.MapContactEntityToModel();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MapContactEntityToModel_WhenContactGuidIsNull_ShouldMapCorrectly()
    {
        // Arrange
        var contactEntity = _fixture.Build<ContactEntity>()
            .With(c => c.ContactGlobalUniqueId, (Guid?)null)
            .Create();

        // Act
        var result = contactEntity.MapContactEntityToModel();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.ContactGlobalUniqueId);
        Assert.Equal(contactEntity.ContactId, result.ContactId);
        Assert.Equal(contactEntity.Email, result.Email);
    }

    [Fact]
    public void MapContactEntityToModel_WhenTypeIsNull_ShouldMapCorrectly()
    {
        // Arrange
        var contactEntity = _fixture.Build<ContactEntity>()
            .With(c => c.Type, (string?)null)
            .Create();

        // Act
        var result = contactEntity.MapContactEntityToModel();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Type);
        Assert.Equal(contactEntity.ContactId, result.ContactId);
        Assert.Equal(contactEntity.Email, result.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Administrator")]
    [InlineData("Collaborator")]
    [InlineData("External")]
    public void MapContactEntityToModel_WithDifferentTypes_ShouldMapCorrectly(string? type)
    {
        // Arrange
        var contactEntity = _fixture.Build<ContactEntity>()
            .With(c => c.Type, type)
            .Create();

        // Act
        var result = contactEntity.MapContactEntityToModel();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(type, result.Type);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Alice", "")]
    [InlineData("", "Johnson")]
    [InlineData("Marie-Claire", "Dubois-Martin")]
    public void MapContactEntityToModel_WithDifferentNames_ShouldMapCorrectly(string firstName, string lastName)
    {
        // Arrange
        var contactEntity = _fixture.Build<ContactEntity>()
            .With(c => c.FirstName, firstName)
            .With(c => c.LastName, lastName)
            .Create();

        // Act
        var result = contactEntity.MapContactEntityToModel();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(firstName, result.FirstName);
        Assert.Equal(lastName, result.LastName);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void MapContacts_RoundTripMapping_ShouldPreserveAllData()
    {
        // Arrange
        var originalContact = _fixture.Build<Contact>()
            .With(c => c.ContactId, 789)
            .With(c => c.ContactGlobalUniqueId, Guid.NewGuid())
            .With(c => c.Email, "roundtrip@example.com")
            .With(c => c.Type, "RoundTripType")
            .With(c => c.FirstName, "Round")
            .With(c => c.LastName, "Trip")
            .Create();

        // Act - Convert Contact -> ContactEntity -> Contact
        var entity = originalContact.MapContactModelToEntity();
        var roundTripContact = entity!.MapContactEntityToModel();

        // Assert - All data should be preserved exactly
        Assert.NotNull(roundTripContact);
        Assert.Equal(originalContact.ContactId, roundTripContact.ContactId);
        Assert.Equal(originalContact.ContactGlobalUniqueId, roundTripContact.ContactGlobalUniqueId);
        Assert.Equal(originalContact.Email, roundTripContact.Email);
        Assert.Equal(originalContact.Type, roundTripContact.Type);
        Assert.Equal(originalContact.FirstName, roundTripContact.FirstName);
        Assert.Equal(originalContact.LastName, roundTripContact.LastName);
    }

    [Fact]
    public void MapContacts_ReverseRoundTripMapping_ShouldPreserveAllData()
    {
        // Arrange
        var originalEntity = _fixture.Build<ContactEntity>()
            .With(c => c.ContactId, 321)
            .With(c => c.ContactGlobalUniqueId, Guid.NewGuid())
            .With(c => c.Email, "reverse@example.com")
            .With(c => c.Type, "ReverseType")
            .With(c => c.FirstName, "Reverse")
            .With(c => c.LastName, "Test")
            .Create();

        // Act - Convert ContactEntity -> Contact -> ContactEntity
        var model = originalEntity.MapContactEntityToModel();
        var roundTripEntity = model!.MapContactModelToEntity();

        // Assert - All data should be preserved exactly
        Assert.NotNull(roundTripEntity);
        Assert.Equal(originalEntity.ContactId, roundTripEntity.ContactId);
        Assert.Equal(originalEntity.ContactGlobalUniqueId, roundTripEntity.ContactGlobalUniqueId);
        Assert.Equal(originalEntity.Email, roundTripEntity.Email);
        Assert.Equal(originalEntity.Type, roundTripEntity.Type);
        Assert.Equal(originalEntity.FirstName, roundTripEntity.FirstName);
        Assert.Equal(originalEntity.LastName, roundTripEntity.LastName);
    }

    [Fact]
    public void MapContacts_WithNullValues_ShouldHandleCorrectly()
    {
        // Arrange
        var contact = _fixture.Build<Contact>()
            .With(c => c.ContactGlobalUniqueId, (Guid?)null)
            .With(c => c.Type, (string?)null)
            .Create();

        // Act
        var entity = contact.MapContactModelToEntity();
        var backToModel = entity!.MapContactEntityToModel();

        // Assert
        Assert.NotNull(entity);
        Assert.NotNull(backToModel);
        Assert.Null(entity.ContactGlobalUniqueId);
        Assert.Null(entity.Type);
        Assert.Null(backToModel.ContactGlobalUniqueId);
        Assert.Null(backToModel.Type);
    }

    [Fact]
    public void MapContacts_MultipleConversions_ShouldBeIdempotent()
    {
        // Arrange
        var originalContact = _fixture.Create<Contact>();

        // Act - Multiple conversions
        var entity1 = originalContact.MapContactModelToEntity();
        var model1 = entity1!.MapContactEntityToModel();
        var entity2 = model1!.MapContactModelToEntity();
        var model2 = entity2!.MapContactEntityToModel();

        // Assert - Results should be identical across all conversions
        Assert.Equal(originalContact.ContactId, model1.ContactId);
        Assert.Equal(originalContact.ContactGlobalUniqueId, model1.ContactGlobalUniqueId);
        Assert.Equal(originalContact.Email, model1.Email);
        Assert.Equal(originalContact.Type, model1.Type);
        Assert.Equal(originalContact.FirstName, model1.FirstName);
        Assert.Equal(originalContact.LastName, model1.LastName);
        
        Assert.Equal(model1.ContactId, model2!.ContactId);
        Assert.Equal(model1.ContactGlobalUniqueId, model2.ContactGlobalUniqueId);
        Assert.Equal(model1.Email, model2.Email);
        Assert.Equal(model1.Type, model2.Type);
        Assert.Equal(model1.FirstName, model2.FirstName);
        Assert.Equal(model1.LastName, model2.LastName);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void MapContacts_WithSpecialCharactersInNames_ShouldHandleCorrectly()
    {
        // Arrange
        var contact = _fixture.Build<Contact>()
            .With(c => c.FirstName, "José-María")
            .With(c => c.LastName, "O'Connor-Müller")
            .With(c => c.Email, "josé.maría@example.com")
            .Create();

        // Act
        var entity = contact.MapContactModelToEntity();
        var backToModel = entity!.MapContactEntityToModel();

        // Assert
        Assert.Equal("José-María", entity.FirstName);
        Assert.Equal("O'Connor-Müller", entity.LastName);
        Assert.Equal("josé.maría@example.com", entity.Email);
        Assert.Equal(contact.FirstName, backToModel!.FirstName);
        Assert.Equal(contact.LastName, backToModel.LastName);
        Assert.Equal(contact.Email, backToModel.Email);
    }

    [Fact]
    public void MapContacts_WithMinMaxIntValues_ShouldHandleCorrectly()
    {
        // Arrange
        var contact = new Contact
        {
            ContactId = int.MaxValue,
            ContactGlobalUniqueId = Guid.Empty,
            Email = "test@example.com",
            Type = "TestType",
            FirstName = "Test",
            LastName = "Max"
        };

        // Act
        var entity = contact.MapContactModelToEntity();
        var backToModel = entity!.MapContactEntityToModel();

        // Assert
        Assert.Equal(int.MaxValue, entity.ContactId);
        Assert.Equal(Guid.Empty, entity.ContactGlobalUniqueId);
        Assert.Equal(int.MaxValue, backToModel!.ContactId);
        Assert.Equal(Guid.Empty, backToModel.ContactGlobalUniqueId);
    }

    [Fact]
    public void MapContacts_WithVeryLongStrings_ShouldMapCorrectly()
    {
        // Arrange
        var longString = new string('A', 1000);
        var contact = _fixture.Build<Contact>()
            .With(c => c.FirstName, longString)
            .With(c => c.LastName, longString)
            .With(c => c.Type, longString)
            .Create();

        // Act
        var entity = contact.MapContactModelToEntity();

        // Assert
        Assert.NotNull(entity);
        Assert.Equal(longString, entity.FirstName);
        Assert.Equal(longString, entity.LastName);
        Assert.Equal(longString, entity.Type);
    }

    [Fact]
    public void MapContacts_WithEmptyStrings_ShouldMapCorrectly()
    {
        // Arrange
        var contact = _fixture.Build<Contact>()
            .With(c => c.FirstName, string.Empty)
            .With(c => c.LastName, string.Empty)
            .With(c => c.Type, string.Empty)
            .Create();

        // Act
        var entity = contact.MapContactModelToEntity();
        var backToModel = entity!.MapContactEntityToModel();

        // Assert
        Assert.Equal(string.Empty, entity.FirstName);
        Assert.Equal(string.Empty, entity.LastName);
        Assert.Equal(string.Empty, entity.Type);
        Assert.Equal(string.Empty, backToModel!.FirstName);
        Assert.Equal(string.Empty, backToModel.LastName);
        Assert.Equal(string.Empty, backToModel.Type);
    }

    #endregion
}
