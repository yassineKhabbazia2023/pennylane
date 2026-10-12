// <copyright file="PennylaneAccessResultBase.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Models
{
    /// <summary>
    /// Base class for Pennylane access operation results.
    /// </summary>
    public abstract class PennylaneAccessResultBase
    {
        /// <summary>
        /// Gets or sets the status of the operation.
        /// </summary>
        public required string Status { get; set; }

        /// <summary>
        /// Gets or sets a user-friendly message describing the result.
        /// </summary>
        public required string Message { get; set; }

        /// <summary>
        /// Gets or sets the contact ID.
        /// </summary>
        public int ContactId { get; set; }

        /// <summary>
        /// Gets or sets the account ID.
        /// </summary>
        public int AccountId { get; set; }

        /// <summary>
        /// Gets or sets the Pennylane user ID if available.
        /// </summary>
        public string? PennylaneUserId { get; set; }

        /// <summary>
        /// Gets or sets the Pennylane company ID if available.
        /// </summary>
        public string? PennylaneCompanyId { get; set; }

        /// <summary>
        /// Gets or sets the error details if the operation failed.
        /// Null if Status is not "failed".
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the list of steps that were executed during the operation.
        /// </summary>
        public List<string>? Steps { get; set; }
    }
}
