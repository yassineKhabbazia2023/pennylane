// <copyright file="ServiceBusOperationException.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Application.Exceptions
{
    public class ServiceBusOperationException : Exception
    {
        public ServiceBusOperationException()
        {
        }

        public ServiceBusOperationException(string? message) : base(message)
        {
        }

        public ServiceBusOperationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
