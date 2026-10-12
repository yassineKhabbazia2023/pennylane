using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Pennylane.Application.Interfaces;
using Pennylane.Infrastructure.Serialization;
using Pulse.Back.Events.Abstractions;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Infrastructure.Handlers
{
    public class ContactRemovedEventHandler : IEventHandler
    {
        private readonly ILogger<ContactRemovedEventHandler> logger;
        private readonly IContactService contactService;
        private readonly IPennylaneGedPermissionRepository gedPermissionRepository;

        public ContactRemovedEventHandler(
            ILogger<ContactRemovedEventHandler> logger,
            IContactService contactService,
            IPennylaneGedPermissionRepository gedPermissionRepository)
        {
            this.logger = logger;
            this.contactService = contactService;
            this.gedPermissionRepository = gedPermissionRepository;
        }

        public async Task HandleAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var contactEvent = JsonDefaults.TryDeserialize<ContactRemovedEvent>(message);
            logger.LogInformation("Consommation de l'event type: {EventType}, contactId: {ContactId}",
                contactEvent?.EventType,
                contactEvent?.Data?.ContactId);

            if (contactEvent?.Data == null || contactEvent?.Data?.ContactId <= 0)
            {
                logger.LogError($"[Event]: {contactEvent.EventType} Data is null or empty");
                return;
            }

            int? contactId = contactEvent?.Data?.ContactId;

            try
            {
                await contactService.OnRemovedContactEventExecution(contactEvent?.Data);
                await gedPermissionRepository.RemoveAllPermissionsForContactAsync(contactEvent.Data.ContactId);
            }
            catch (Exception ex)
            {
                logger.LogError($"[Event]: {contactEvent.EventType} Something went wrong while processing this request; [Error]:{ex.Message}");
                return;
            }

        }
    }
}
