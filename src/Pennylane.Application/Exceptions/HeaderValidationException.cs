// pyright file="HeaderValidationException.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Exceptions;

/// <summary>
/// Exception levée lorsque le header CSV est invalide.
/// </summary>
public class HeaderValidationException : Exception
{
    public HeaderValidationException() : base("Le fichier CSV ne contient pas de ligne d'en-tête valide.")
    {
    }

    public HeaderValidationException(string message) : base(message)
    {
    }

    public HeaderValidationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
