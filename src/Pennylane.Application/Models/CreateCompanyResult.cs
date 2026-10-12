// <copyright file="CreateCompanyResult.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Models
{
    /// <summary>
    /// Result of creating a company in Pennylane, including user addition results.
    /// </summary>
    public class CreateCompanyResult
    {
        /// <summary>
        /// Gets or sets the company information.
        /// Null when the company already exists but no ID is returned or when creation failed.
        /// </summary>
        public PennylaneCompany? Company { get; set; }

        /// <summary>
        /// Gets or sets the status of the operation.
        /// - "created": Company was newly created
        /// - "already_exists": Company already existed
        /// - "À créer": Company creation failed and should be retried later
        /// - "partial_success": Company OK but some users failed to be added
        /// </summary>
        public required string Status { get; set; }

        /// <summary>
        /// Gets or sets the results of adding users to the company.
        /// Null if no contacts were provided.
        /// </summary>
        public List<UserAdditionResult>? UserResults { get; set; }
    }

    /// <summary>
    /// Result of adding a single user to a company.
    /// </summary>
    public class UserAdditionResult
    {
        /// <summary>
        /// Gets or sets the contact ID.
        /// </summary>
        public int ContactId { get; set; }

        /// <summary>
        /// Gets or sets the user's email address.
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets whether the user was successfully added.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the addition failed.
        /// Null if Success is true.
        /// </summary>
        public string? Error { get; set; }
    }
}
