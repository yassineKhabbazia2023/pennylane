// <copyright file="ContactServiceTests.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Application.Interfaces;
using Application.Models.Contacts;
using Application.Services;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Interfaces;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Pennylane.Application.Tests.Services
{
    public class ContactServiceTests
    {
        private readonly Fixture _fixture;
        private readonly Mock<ILogger<ContactService>> _loggerMock;
        private readonly Mock<IContactRepository> _repoMock;
        private readonly Mock<IPennylaneApiGroupService> _pnlApiMock;
        private readonly IOptions<PennylaneServiceOptions> _options;

        public ContactServiceTests()
        {
            _fixture = new Fixture();
            // Avoid recursion issues
            _fixture.Behaviors
                .OfType<ThrowingRecursionBehavior>()
                .ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            _loggerMock = new Mock<ILogger<ContactService>>();
            _repoMock = new Mock<IContactRepository>(MockBehavior.Strict);
            _pnlApiMock = new Mock<IPennylaneApiGroupService>(MockBehavior.Strict);

            _options = Options.Create(new PennylaneServiceOptions
            {
                EnableUpdatePennylaneUsersEmails = true,
                ApiUrl = "https://api.fake",
                ApiFirmUrl = "https://firm.fake",
                ApiKey = "fake-key",
                Token = "fake-token",
                AuthenticationMode = "fake-mode",
                CollaboratorAuthenticationMode = "fake-collab-mode",
                DefaultRole = "fake-role",
                SupportedSaasPlans = null // Assuming this is not required for the tests
            });
        }

        #region OnCreatedContactEventExecution

        [Fact]
        public async Task OnCreatedContactEventExecution_CallsAddContact_WithMappedModel()
        {
            // Arrange
            var eventData = _fixture.Create<ContactStateEventData>();
            Contact? captured = null;
            _repoMock
                .Setup(r => r.AddContactAsync(It.IsAny<Contact>()))
                     .Callback<Contact>(c => captured = c)
                     .ReturnsAsync(true);

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            await svc.OnCreatedContactEventExecution(eventData);

            // Assert
            _repoMock.Verify(r => r.AddContactAsync(It.IsAny<Contact>()), Times.Once);
            captured.Should().NotBeNull();
            captured!.ContactId.Should().Be(eventData.ContactId);
            captured.ContactGlobalUniqueId.Should().Be(eventData.ContactGlobalUniqueId);
            captured.Email.Should().Be(eventData.Email);
            captured.FirstName.Should().Be(eventData.FirstName);
            captured.LastName.Should().Be(eventData.LastName);
            captured.Type.Should().Be(eventData.Type);
        }

        [Fact]
        public async Task OnCreatedContactEventExecution_DoesNotThrow_WhenInsertFails()
        {
            // Arrange
            var eventData = _fixture.Create<ContactStateEventData>();
            _repoMock
                .Setup(r => r.AddContactAsync(It.IsAny<Contact>()))
                     .ReturnsAsync(false);

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            Func<Task> act = () => svc.OnCreatedContactEventExecution(eventData);

            // Assert
            await act.Should().NotThrowAsync();
            _repoMock.Verify(r => r.AddContactAsync(It.IsAny<Contact>()), Times.Once);
        }

        [Fact]
        public async Task OnCreatedContactEventExecution_PropagatesException_WhenRepositoryThrows()
        {
            // Arrange
            var eventData = _fixture.Create<ContactStateEventData>();
            _repoMock
                .Setup(r => r.AddContactAsync(It.IsAny<Contact>()))
                     .ThrowsAsync(new InvalidOperationException("boom"));

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            Func<Task> act = () => svc.OnCreatedContactEventExecution(eventData);
            
            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage("boom");
            _repoMock.Verify(r => r.AddContactAsync(It.IsAny<Contact>()), Times.Once);
        }

        #endregion OnCreatedContactEventExecution

        #region OnUpdatedContactEventExecution

        [Fact]
        public async Task OnUpdatedContactEventExecution_CallsUpdateContact_WithMappedModel()
        {
            var eventData = _fixture.Create<ContactStateEventData>();
            Contact? captured = null;
            _repoMock.Setup(r => r.GetContactByGlobalUniqueId(eventData.ContactGlobalUniqueId!.Value))
                     .ReturnsAsync(_fixture.Create<Contact>());
            _repoMock
                .Setup(r => r.UpdateContactAsync(It.IsAny<Contact>()))
                     .Callback<Contact>(c => captured = c)
                     .ReturnsAsync(true);
            _pnlApiMock.Setup(api => api.TryGetUserAsync(It.IsAny<string>()))
                       .ReturnsAsync((PennylaneUsers?)null);

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            await svc.OnUpdatedContactEventExecution(eventData);

            _repoMock.Verify(r => r.UpdateContactAsync(It.IsAny<Contact>()), Times.Once);
            captured.Should().NotBeNull();
            captured!.ContactId.Should().Be(eventData.ContactId);
            captured.ContactGlobalUniqueId.Should().Be(eventData.ContactGlobalUniqueId);
            captured.Email.Should().Be(eventData.Email);
            captured.FirstName.Should().Be(eventData.FirstName);
            captured.LastName.Should().Be(eventData.LastName);
            captured.Type.Should().Be(eventData.Type);
        }

        [Fact]
        public async Task OnUpdatedContactEventExecution_DoesNotThrow_WhenUpdateFails()
        {
            // Arrange
            var eventData = _fixture.Create<ContactStateEventData>();
            _repoMock.Setup(r => r.GetContactByGlobalUniqueId(eventData.ContactGlobalUniqueId!.Value))
                     .ReturnsAsync(_fixture.Create<Contact>());
            _repoMock
                .Setup(r => r.UpdateContactAsync(It.IsAny<Contact>()))
                     .ReturnsAsync(false);
            _pnlApiMock.Setup(api => api.TryGetUserAsync(It.IsAny<string>()))
                       .ReturnsAsync((PennylaneUsers?)null);

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            Func<Task> act = () => svc.OnUpdatedContactEventExecution(eventData);

            // Assert
            await act.Should().NotThrowAsync();
            _repoMock.Verify(r => r.UpdateContactAsync(It.IsAny<Contact>()), Times.Once);
        }

        [Fact]
        public async Task OnUpdatedContactEventExecution_PropagatesException_WhenRepositoryThrows()
        {
            // Arrange
            var eventData = _fixture.Create<ContactStateEventData>();

            var existing = _fixture.Create<Contact>();
 
            _repoMock.Setup(r => r.GetContactByGlobalUniqueId(eventData.ContactGlobalUniqueId!.Value))
                     .ReturnsAsync(existing);

            _repoMock
                .Setup(r => r.UpdateContactAsync(It.IsAny<Contact>()))
                .ThrowsAsync(new InvalidOperationException("fail"));

            var disabledOptions = Options.Create(new PennylaneServiceOptions
            {
                EnableUpdatePennylaneUsersEmails = false,
                ApiUrl = "https://api.fake",
                ApiFirmUrl = "https://firm.fake",
                ApiKey = "fake-key",
                Token = "fake-token",
                AuthenticationMode = "fake-mode",
                CollaboratorAuthenticationMode = "fake-collab-mode",
                DefaultRole = "fake-role",
                SupportedSaasPlans = null
            });

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, disabledOptions);

            // Act
            Func<Task> act = () => svc.OnUpdatedContactEventExecution(eventData);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage("fail");
            _repoMock.Verify(r => r.UpdateContactAsync(It.IsAny<Contact>()), Times.Once);
        }

        #region update email tests

        [Fact]
        public async Task OnUpdatedContactEventExecution_EmailChanged_UserFound_UpdatesEmailInPennylane()
        {
            // Arrange
            var oldEmail = "old@example.com";
            var newEmail = "new@example.com";

            var eventData = _fixture.Build<ContactStateEventData>()
                                    .With(x => x.Email, newEmail)
                                    .With(x => x.ContactGlobalUniqueId, Guid.NewGuid())
                                    .Create();

            var existing = _fixture.Build<Contact>()
                                   .With(c => c.ContactGlobalUniqueId, eventData.ContactGlobalUniqueId)
                                   .With(c => c.Email, oldEmail)
                                   .Create();

            _repoMock.Setup(r => r.GetContactByGlobalUniqueId(eventData.ContactGlobalUniqueId!.Value))
                     .ReturnsAsync(existing);

            var user = _fixture.Build<PennylaneUsers>()
                               .With(u => u.Email, oldEmail)
                               .With(u => u.Id, "USR1")
                               .Create();

            _pnlApiMock.Setup(api => api.TryGetUserAsync(oldEmail))
                       .ReturnsAsync(user);

            _pnlApiMock.Setup(api => api.UpdateUserEmailAsync(
                                It.Is<PennylaneContact>(pc => pc.Id == "USR1"),
                                newEmail))
                       .ReturnsAsync("USR1");

            _repoMock.Setup(r => r.UpdateContactAsync(It.IsAny<Contact>()))
                     .ReturnsAsync(true);

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            await svc.OnUpdatedContactEventExecution(eventData);

            // Assert
            _pnlApiMock.Verify(api => api.TryGetUserAsync(oldEmail), Times.Once);
            _pnlApiMock.Verify(api => api.UpdateUserEmailAsync(
                It.Is<PennylaneContact>(pc => pc.Id == "USR1"), newEmail), Times.Once);
            _repoMock.Verify(r => r.UpdateContactAsync(It.IsAny<Contact>()), Times.Once);
        }

        [Fact]
        public async Task OnUpdatedContactEventExecution_EmailChanged_UserNotFound_SkipsPennylaneUpdate()
        {
            // Arrange
            var oldEmail = "old2@example.com";
            var newEmail = "new2@example.com";

            var eventData = _fixture.Build<ContactStateEventData>()
                                    .With(x => x.Email, newEmail)
                                    .With(x => x.ContactGlobalUniqueId, Guid.NewGuid())
                                    .Create();

            var existing = _fixture.Build<Contact>()
                                   .With(c => c.ContactGlobalUniqueId, eventData.ContactGlobalUniqueId)
                                   .With(c => c.Email, oldEmail)
                                   .Create();

            _repoMock.Setup(r => r.GetContactByGlobalUniqueId(eventData.ContactGlobalUniqueId!.Value))
                     .ReturnsAsync(existing);

            _pnlApiMock.Setup(api => api.TryGetUserAsync(oldEmail))
                       .ReturnsAsync((PennylaneUsers?)null);

            _repoMock.Setup(r => r.UpdateContactAsync(It.IsAny<Contact>()))
                     .ReturnsAsync(true);

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            await svc.OnUpdatedContactEventExecution(eventData);

            // Assert
            _pnlApiMock.Verify(api => api.TryGetUserAsync(oldEmail), Times.Once);
            _pnlApiMock.Verify(api => api.UpdateUserEmailAsync(It.IsAny<PennylaneContact>(), It.IsAny<string>()), Times.Never);
            _repoMock.Verify(r => r.UpdateContactAsync(It.IsAny<Contact>()), Times.Once);
        }

        [Fact]
        public async Task OnUpdatedContactEventExecution_EmailChanged_PennylaneUpdateThrows_BadRequest_IsSwallowed_AndContactStillUpdated()
        {
            // Arrange
            var oldEmail = "old3@example.com";
            var newEmail = "new3@example.com";

            var eventData = _fixture.Build<ContactStateEventData>()
                                    .With(x => x.Email, newEmail)
                                    .With(x => x.ContactGlobalUniqueId, Guid.NewGuid())
                                    .Create();

            var existing = _fixture.Build<Contact>()
                                   .With(c => c.ContactGlobalUniqueId, eventData.ContactGlobalUniqueId)
                                   .With(c => c.Email, oldEmail)
                                   .Create();

            _repoMock.Setup(r => r.GetContactByGlobalUniqueId(eventData.ContactGlobalUniqueId!.Value))
                     .ReturnsAsync(existing);

            var user = _fixture.Build<PennylaneUsers>()
                               .With(u => u.Email, oldEmail)
                               .With(u => u.Id, "USR2")
                               .Create();

            _pnlApiMock.Setup(api => api.TryGetUserAsync(oldEmail))
                       .ReturnsAsync(user);

            _pnlApiMock.Setup(api => api.UpdateUserEmailAsync(
                                It.Is<PennylaneContact>(pc => pc.Id == "USR2"),
                                newEmail))
                       .ThrowsAsync(new Pulse.ExceptionMiddleware.Exceptions.BadRequestException("X", "oops"));

            _repoMock.Setup(r => r.UpdateContactAsync(It.IsAny<Contact>()))
                     .ReturnsAsync(true);

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            Func<Task> act = () => svc.OnUpdatedContactEventExecution(eventData);

            // Assert
            await act.Should().NotThrowAsync(); // exception is caught & logged
            _pnlApiMock.Verify(api => api.TryGetUserAsync(oldEmail), Times.Once);
            _pnlApiMock.Verify(api => api.UpdateUserEmailAsync(
                It.Is<PennylaneContact>(pc => pc.Id == "USR2"), newEmail), Times.Once);
            _repoMock.Verify(r => r.UpdateContactAsync(It.IsAny<Contact>()), Times.Once);
        }

        [Fact]
        public async Task OnUpdatedContactEventExecution_FeatureFlagDisabled_SkipsPennylaneUpdate()
        {
            // Arrange
            var oldEmail = "old4@example.com";
            var newEmail = "new4@example.com";

            var eventData = _fixture.Build<ContactStateEventData>()
                                    .With(x => x.Email, newEmail)
                                    .With(x => x.ContactGlobalUniqueId, Guid.NewGuid())
                                    .Create();

            var existing = _fixture.Build<Contact>()
                                   .With(c => c.ContactGlobalUniqueId, eventData.ContactGlobalUniqueId)
                                   .With(c => c.Email, oldEmail)
                                   .Create();

            _repoMock.Setup(r => r.GetContactByGlobalUniqueId(eventData.ContactGlobalUniqueId!.Value))
                     .ReturnsAsync(existing);

            _repoMock.Setup(r => r.UpdateContactAsync(It.IsAny<Contact>()))
                     .ReturnsAsync(true);

            // Disable the feature flag but keep required option fields
            var disabledOptions = Options.Create(new PennylaneServiceOptions
            {
                EnableUpdatePennylaneUsersEmails = false,
                ApiUrl = "https://api.fake",
                ApiFirmUrl = "https://firm.fake",
                ApiKey = "fake-key",
                Token = "fake-token",
                AuthenticationMode = "fake-mode",
                CollaboratorAuthenticationMode = "fake-collab-mode",
                DefaultRole = "fake-role",
                SupportedSaasPlans = null
            });

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, disabledOptions);

            // Act
            await svc.OnUpdatedContactEventExecution(eventData);

            // Assert: Pennylane calls are skipped, DB update still happens
            _pnlApiMock.Verify(api => api.TryGetUserAsync(It.IsAny<string>()), Times.Never);
            _pnlApiMock.Verify(api => api.UpdateUserEmailAsync(It.IsAny<PennylaneContact>(), It.IsAny<string>()), Times.Never);
            _repoMock.Verify(r => r.UpdateContactAsync(It.IsAny<Contact>()), Times.Once);
        }

        #endregion update email tests

        #endregion OnUpdatedContactEventExecution

        #region OnRemovedContactEventExecution

        [Fact]
        public async Task OnRemovedContactEventExecution_WhenContactExists_DeletesAndReturnsSuccessfully()
        {
            // Arrange
            var removedData = _fixture.Create<ContactRemovedEventData>();
            var existing = _fixture.Build<Contact>()
                                   .With(c => c.ContactId, removedData.ContactId)
                                   .Create();

            _repoMock
                .Setup(r => r.GetContactByEmailOrIdAsync(null, removedData.ContactId))
                     .ReturnsAsync(existing);
            _repoMock
                .Setup(r => r.DeleteContactByIdAsync(removedData.ContactId))
                     .ReturnsAsync(true);

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            await svc.OnRemovedContactEventExecution(removedData);

            // Assert
            _repoMock.Verify(r => r.GetContactByEmailOrIdAsync(null, removedData.ContactId), Times.Once);
            _repoMock.Verify(r => r.DeleteContactByIdAsync(removedData.ContactId), Times.Once);
        }

        [Fact]
        public async Task OnRemovedContactEventExecution_WhenContactDoesNotExist_DoesNotCallDelete()
        {
            // Arrange
            var removedData = _fixture.Create<ContactRemovedEventData>();
            _repoMock
                .Setup(r => r.GetContactByEmailOrIdAsync(null, removedData.ContactId))
                     .ReturnsAsync((Contact?)null);

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            await svc.OnRemovedContactEventExecution(removedData);

            // Assert
            _repoMock.Verify(r => r.GetContactByEmailOrIdAsync(null, removedData.ContactId), Times.Once);
            _repoMock.Verify(r => r.DeleteContactByIdAsync(It.IsAny<int?>()), Times.Never);
        }

        [Fact]
        public async Task OnRemovedContactEventExecution_DoesNotThrow_WhenDeleteFails()
        {
            // Arrange
            var removedData = _fixture.Create<ContactRemovedEventData>();
            var existing = _fixture.Build<Contact>()
                                   .With(c => c.ContactId, removedData.ContactId)
                                   .Create();

            _repoMock
                .Setup(r => r.GetContactByEmailOrIdAsync(null, removedData.ContactId))
                     .ReturnsAsync(existing);
            _repoMock
                .Setup(r => r.DeleteContactByIdAsync(removedData.ContactId))
                     .ReturnsAsync(false);

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            Func<Task> act = () => svc.OnRemovedContactEventExecution(removedData);

            // Assert
            await act.Should().NotThrowAsync();
            _repoMock.Verify(r => r.DeleteContactByIdAsync(removedData.ContactId), Times.Once);
        }

        [Fact]
        public async Task OnRemovedContactEventExecution_PropagatesException_WhenGetThrows()
        {
            // Arrange
            var removedData = _fixture.Create<ContactRemovedEventData>();
            _repoMock
                .Setup(r => r.GetContactByEmailOrIdAsync(null, removedData.ContactId))
                .ThrowsAsync(new InvalidOperationException("whoops"));

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            Func<Task> act = () => svc.OnRemovedContactEventExecution(removedData);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage("whoops");
        }

        [Fact]
        public async Task OnRemovedContactEventExecution_PropagatesException_WhenDeleteThrows()
        {
            // Arrange
            var removedData = _fixture.Create<ContactRemovedEventData>();
            var existing = _fixture.Build<Contact>()
                                   .With(c => c.ContactId, removedData.ContactId)
                                   .Create();

            _repoMock
                .Setup(r => r.GetContactByEmailOrIdAsync(null, removedData.ContactId))
                     .ReturnsAsync(existing);
            _repoMock
                .Setup(r => r.DeleteContactByIdAsync(removedData.ContactId))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var svc = new ContactService(_loggerMock.Object, _repoMock.Object, _pnlApiMock.Object, _options);

            // Act
            Func<Task> act = () => svc.OnRemovedContactEventExecution(removedData);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage("boom");
        }

        #endregion OnRemovedContactEventExecution
    }
}
