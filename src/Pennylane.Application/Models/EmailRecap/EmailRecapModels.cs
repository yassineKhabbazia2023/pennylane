// <copyright file="EmailRecapModels.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace Pennylane.Application.Models.EmailRecap;

public record DocumentCountByAccount(
    int AccountId,
    string AccountNumber,
    string? AccountName,
    int DocumentCount);

public record AuthorizedContact(
    int ContactId,
    string Email,
    string? FirstName,
    string? LastName,
    string? Type);

public class EmailRecapPayload
{
    public bool IsRecipientCustomer { get; set; } = true;
    public required string UserName { get; set; }
    public required string LoginUrl { get; set; }
    public string TitleDocumentsTypeName { get; set; } = "Pennylane";
    public string SummaryType { get; set; } = "GED Pennylane";
    public int DepositedDocumentsCount { get; set; }
    public required string DocumentsDepositDate { get; set; }
    public List<SummaryLine> SummaryLines { get; set; } = new();
}

public record SummaryLine(
    [property: JsonPropertyName("entityName")] string EntityName,
    [property: JsonPropertyName("depositedDocumentsCount")] int DepositedDocumentsCount);

public record EmailRecapGenerationResult(
    int BatchId,
    DateTime RecapDate,
    int TotalItems,
    int TotalDocuments,
    int AccountsWithDocuments,
    bool Success,
    string? ErrorMessage,
    DateTime GeneratedAt)
{
    public static EmailRecapGenerationResult Failed(string error) =>
        new(0, DateTime.UtcNow.Date, 0, 0, 0, false, error, DateTime.UtcNow);

    public static EmailRecapGenerationResult Empty(DateTime recapDate) =>
        new(0, recapDate, 0, 0, 0, true, null, DateTime.UtcNow);
}

public record EmailRecapSendResult(
    int BatchId,
    int TotalItems,
    int SentCount,
    int FailedCount,
    List<string> Errors,
    DateTime StartedAt,
    DateTime CompletedAt)
{
    public static EmailRecapSendResult Empty() =>
        new(0, 0, 0, 0, new List<string>(), DateTime.UtcNow, DateTime.UtcNow);
}
