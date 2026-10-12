// <copyright file="GrantPennylaneAccessResult.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Models
{
    /// <summary>
    /// Result of granting Pennylane access to a user.
    /// </summary>
    public class GrantPennylaneAccessResult : PennylaneAccessResultBase
    {
        /// <summary>
        /// Gets or sets the role assigned to the user.
        /// </summary>
        public string? Role { get; set; }

        /// <summary>
        /// Sets the error and message, then returns this instance for fluent chaining.
        /// </summary>
        public GrantPennylaneAccessResult WithError(string error, string message)
        {
            Error = error;
            Message = message;
            return this;
        }
    }
}
