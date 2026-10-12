// <copyright file="PennylaneCompanyCreationException.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Exceptions;

/// <summary>
/// Exception thrown when Pennylane company creation fails.
/// </summary>
public class PennylaneCompanyCreationException : Exception
{
    public PennylaneCompanyCreationException(string message, string? errorContent = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorContent = errorContent;
    }

    /// <summary>
    /// Gets the raw error content returned by Pennylane.
    /// </summary>
    public string? ErrorContent { get; }
}
