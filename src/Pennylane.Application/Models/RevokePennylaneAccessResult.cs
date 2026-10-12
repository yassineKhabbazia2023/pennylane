// <copyright file="RevokePennylaneAccessResult.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Models
{
    /// <summary>
    /// Result of revoking Pennylane access from a user.
    /// </summary>
    public class RevokePennylaneAccessResult : PennylaneAccessResultBase
    {
        /// <summary>
        /// Sets the error and message, then returns this instance for fluent chaining.
        /// </summary>
        public RevokePennylaneAccessResult WithError(string error, string message)
        {
            Error = error;
            Message = message;
            return this;
        }
    }
}
