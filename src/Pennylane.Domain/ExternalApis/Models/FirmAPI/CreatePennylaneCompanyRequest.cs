// <copyright file="CreatePennylaneCompanyRequest.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace Pennylane.Domain.ExternalApis.Models.FirmAPI;

/// <summary>
/// Request model for creating a company in Pennylane.
/// </summary>
public class CreatePennylaneCompanyRequest
{
    /// <summary>
    /// Gets or sets the country code (ISO 3166-1 alpha-2).
    /// </summary>
    [JsonPropertyName("country_alpha2")]
    public string CountryAlpha2 { get; set; } = "FR";

    /// <summary>
    /// Gets or sets the firm ID in Pennylane.
    /// </summary>
    [JsonPropertyName("firm_id")]
    public string FirmId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the company name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the registration number (SIREN - 9 digits for French companies).
    /// </summary>
    [JsonPropertyName("reg_no")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RegNo { get; set; }

    /// <summary>
    /// Gets or sets the client code (account number).
    /// CRITICAL: This must be the client code (accountNumber) to maintain the direct link with the company
    /// for Pennylane DMS synchronization or role assignment.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the accounting manager ID.
    /// </summary>
    [JsonPropertyName("accounting_manager_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccountingManagerId { get; set; }

    /// <summary>
    /// Gets or sets the accounting supervisor ID.
    /// </summary>
    [JsonPropertyName("accounting_supervisor_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccountingSupervisorId { get; set; }

    /// <summary>
    /// Gets or sets the accountant ID.
    /// </summary>
    [JsonPropertyName("accountant_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccountantId { get; set; }

    /// <summary>
    /// Gets or sets the fiscal regime (e.g., "fr_rsi", "fr_is", "fr_ir", etc.).
    /// </summary>
    [JsonPropertyName("fiscal_regime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FiscalRegime { get; set; }

    /// <summary>
    /// get or sets a value indicating whether the company is not yet registered.
    /// </summary>
    [JsonPropertyName("not_yet_registered")]
    public bool NotYetRegistered { get; set; } = true;

    /// <summary>
    /// get or sets a value indicating the accounting type  of the company.
    /// </summary>
    [JsonPropertyName("accounting_type")]
    public string? AccountingType { get; set; }

    /// <summary>
    /// The Pennylane SaaS plan for the company.
    /// </summary>
    [JsonPropertyName("saas_plan")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SaasPlan { get; set; }

    /// <summary>
    /// Maximum number of users for the company.
    /// </summary>
    [JsonPropertyName("number_of_users")]
    public string? NumberOfUsers { get; set; }

    /// <summary>
    /// Number of employees.
    /// The field presence is mandatory when the saas_plan of the company is v1_basic, v1_essential, or v1_pro.
    /// </summary>
    [JsonPropertyName("number_of_employees")]
    public int? NumberOfEmployees { get; set; }
}
