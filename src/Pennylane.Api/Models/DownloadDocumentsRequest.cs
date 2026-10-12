// <copyright file="DownloadDocumentsRequest.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Pennylane.WebApi.Models
{
    /// <summary>
    /// Request model for downloading multiple Pennylane documents as a zip archive.
    /// </summary>
    public class DownloadDocumentsRequest
    {
        /// <summary>
        /// Maximum number of documents accepted in a single archive request.
        /// </summary>
        public const int MaxDocumentsPerArchive = 15;

        /// <summary>
        /// Gets or sets the identifiers of the documents to download.
        /// </summary>
        [Required]
        [MinLength(1)]
        [MaxLength(MaxDocumentsPerArchive)]
        public List<long> Ids { get; set; } = new();
    }
}
