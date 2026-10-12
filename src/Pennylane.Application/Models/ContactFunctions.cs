// <copyright file="ContactFunctions.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.WebApi.Models
{
    /// <summary>
    /// Represents a contact with a specific function/role in the company.
    /// </summary>
    public class ContactFunctions
    {
        /// <summary>
        /// Gets or sets the contact ID.
        /// </summary>
        public int ContactId { get; set; }

        /// <summary>
        /// Gets or sets the function ID (1 = Accounting Manager, 2 = Accounting Supervisor, 3 = Accountant).
        /// </summary>
        public int FunctionId { get; set; }
    }
}
