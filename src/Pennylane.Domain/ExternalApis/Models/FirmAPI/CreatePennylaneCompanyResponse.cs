// <copyright file="CreatePennylaneCompanyResponse.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace Pennylane.Domain.ExternalApis.Models.FirmAPI
{
    /// <summary>
    /// Response model after creating a company in Pennylane.
    /// </summary>
    public class CreatePennylaneCompanyResponse
    {
        /// <summary>
        /// Gets or sets the company ID (client code).
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of company creation API call attempt.
    /// </summary>
    public record CreateCompanyApiResult(
        bool Success,
        CreatePennylaneCompanyResponse? Response,
        bool RegNoAlreadyExists,
        string? ErrorContent,
        bool RequiresFirmAssignment = false,
        bool RequiresFirmTransfer = false)
    {
        public static CreateCompanyApiResult Succeeded(CreatePennylaneCompanyResponse response) =>
            new(true, response, false, null);

        public static CreateCompanyApiResult FailedRegNoExists(string errorContent) =>
            new(false, null, true, errorContent);

        public static CreateCompanyApiResult Failed(string errorContent) =>
            new(false, null, false, errorContent);

        public static CreateCompanyApiResult FailedRequiresFirmAssignment(string errorContent) =>
            new(false, null, false, errorContent, RequiresFirmAssignment: true);

        public static CreateCompanyApiResult FailedRequiresFirmTransfer(string errorContent) =>
            new(false, null, false, errorContent, RequiresFirmAssignment: false, RequiresFirmTransfer: true);
    }
}
