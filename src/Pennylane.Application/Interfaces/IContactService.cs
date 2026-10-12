// <copyright file="IContactService.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pulse.Back.Events.IntegrationEvents.EventsData;

namespace Application.Interfaces;

public interface IContactService
{
    Task  OnCreatedContactEventExecution(ContactStateEventData contactStateEventData);
    Task OnUpdatedContactEventExecution(ContactStateEventData contactStateEventData);
    Task OnRemovedContactEventExecution(ContactRemovedEventData contactRemovedData);
}