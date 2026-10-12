using System.Text.Json.Serialization;

namespace Pennylane.Application.Models;

public class PennylaneCompany
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("firm_id")]
    public string FirmId { get; set; } = default!;

    [JsonPropertyName("reg_no")]
    public string RegNo { get; set; } = default!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("trade_name")]
    public string TradeName { get; set; } = default!;

    [JsonPropertyName("address")]
    public string Address { get; set; } = default!;

    [JsonPropertyName("city")]
    public string City { get; set; } = default!;

    [JsonPropertyName("postal_code")]
    public string PostalCode { get; set; } = default!;

    [JsonPropertyName("country_alpha2")]
    public string CountryAlpha2 { get; set; } = default!;

    [JsonPropertyName("saas_plan")]
    public string SaasPlan { get; set; } = default!;

    [JsonPropertyName("fiscal_category")]
    public string FiscalCategory { get; set; } = default!;

    [JsonPropertyName("fiscal_regime")]
    public string FiscalRegime { get; set; } = default!;

    [JsonPropertyName("legal_form_code")]
    public string LegalFormCode { get; set; } = default!;

    [JsonPropertyName("vat_frequency")]
    public string VatFrequency { get; set; } = default!;

    [JsonPropertyName("vat_day_of_month")]
    public int? VatDayOfMonth { get; set; }

    [JsonPropertyName("tos_accepted_at")]
    public DateTime? TosAcceptedAt { get; set; }
    
    [JsonPropertyName("supplier_invoice_transmission_emails")]
    public string[]? SupplierInvoiceTransmissionEmails { get; set; }

    [JsonPropertyName("customer_invoice_transmission_emails")]
    public string[]? CustomerInvoiceTransmissionEmails { get; set; }

    [JsonPropertyName("not_yet_registered")]
    public bool NotYetRegistered { get; set; }

    [JsonPropertyName("accounting_manager_id")]
    public string? AccountingManagerId { get; set; }

    [JsonPropertyName("accounting_supervisor_id")]
    public string? AccountingSupervisorId { get; set; }

    [JsonPropertyName("accountant_id")]
    public string? AccountantId { get; set; }
}