// <copyright file="RevokeAccessRequest.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Pennylane.WebApi.Models
{
    /// <summary>
    /// Request model for revoking Pennylane access from a user.
    /// </summary>
    public class RevokeAccessRequest
    {
        /// <summary>
        /// Gets or sets the account ID.
        /// </summary>
        [Required]
        public int AccountId { get; set; }

        /// <summary>
        /// Gets or sets the contact ID.
        /// </summary>
        [Required]
        public int ContactId { get; set; }
    }
}
