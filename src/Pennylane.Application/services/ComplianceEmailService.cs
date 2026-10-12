using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Commons.WebApi;
using Notifications.Commons.WebApi.QueryParams;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Extensions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Managers;
using Pennylane.Application.Models.Signatory;
using Pennylane.Domain.Entities.Pennylane;
using Pulse.Back.Events.IntegrationEvents;

namespace Pennylane.Application.Services;

public class ComplianceEmailService : IComplianceEmailService
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    private readonly INotificationManager _notificationManager;
    private readonly ISignatoryAccessesImportRepository _importRepository;
    private readonly IBlobStorageManager _blobStorageManager;
    private readonly ComplianceEmailOptions _options;
    private readonly ILogger<ComplianceEmailService> _logger;

    public ComplianceEmailService(
        INotificationManager notificationManager,
        ISignatoryAccessesImportRepository importRepository,
        IBlobStorageManager blobStorageManager,
        IOptions<ComplianceEmailOptions> options,
        ILogger<ComplianceEmailService> logger)
    {
        _notificationManager = notificationManager;
        _importRepository = importRepository;
        _blobStorageManager = blobStorageManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAcceptanceEmailAsync(ComplianceEmailContext context, TermsEntity terms, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[ComplianceEmailService] Envoi email acceptation à {Email} pour {Count} entreprise(s)",
            context.RecipientEmail, context.Companies.Count);

        var variables = BuildAcceptanceVariables(context);
        var emailRequest = BuildEmailRequest(context.RecipientEmail, _options.AcceptanceTemplateName, variables);

        var attachment = await BuildTermsPdfAttachmentAsync(terms, cancellationToken);
        if (attachment is not null)
        {
            emailRequest.Attachments.Add(attachment);
        }

        await PublishEmailAsync(emailRequest, cancellationToken);
    }

    public async Task SendRefusalEmailAsync(ComplianceRefusalContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[ComplianceEmailService] Envoi email refus à {Email}", context.RecipientEmail);

        var variables = BuildRefusalVariables(context);
        var emailRequest = BuildEmailRequest(context.RecipientEmail, _options.RefusalTemplateName, variables);

        await PublishEmailAsync(emailRequest, cancellationToken);
    }

    private Dictionary<string, object> BuildAcceptanceVariables(ComplianceEmailContext context)
    {
        var userName = BuildUserName(context.UserFirstName, context.UserLastName);
        var companies = context.Companies.Select(c => new
        {
            name = c.Name,
            compliancePrice = FormatCompliancePrice(c.CompliancePriceExclTax),
            complianceText = c.CompliancePriceExclTax > 0 ? "- Accompagnement RFE :" : string.Empty,
            subscriptionPrice = FormatSubscriptionPrice(c.SubscriptionPriceExclTax),
        }).ToList();

        return new Dictionary<string, object>
        {
            { "userName", userName },
            { "acceptanceDate", DateTime.Now.ToString("dd/MM/yyyy") },
            { "freeTrialEndDate", context.FreeTrialEndDate },
            { "companies", companies },
            { "isNewComplianceFlow", context.IsNewComplianceFlow },
            { "isFreeTrialExpired", context.IsFreeTrialExpired },
            { "wave", $"{context.Wave}" }
        };
    }

    private Dictionary<string, object> BuildRefusalVariables(ComplianceRefusalContext context)
    {
        return new Dictionary<string, object>
        {
            { "userName", BuildUserName(context.UserFirstName, context.UserLastName) },
            { "loginPage", _options.LoginPage },
            { "freeTrialEndDate", context.FreeTrialEndDate },
            { "isNewComplianceFlow", context.IsNewComplianceFlow },
            { "isFreeTrialExpired", context.IsFreeTrialExpired },
            { "wave", $"{context.Wave}" }
        };
    }

    private EmailRequest BuildEmailRequest(string recipientEmail, string templateName, Dictionary<string, object> variables)
    {
        var request = new EmailRequest
        {
            From = _options.SenderEmail,
            To = new List<string> { recipientEmail },
            TemplateName = templateName,
            Variables = variables
        };

        if (!string.IsNullOrWhiteSpace(_options.BccEmail) && !_options.CustomerRelationRefusalRecipientEmail.Equals(recipientEmail))
        {
            request.Bcc = new List<string> { _options.BccEmail };
        }

        return request;
    }

    private async Task PublishEmailAsync(EmailRequest emailRequest, CancellationToken cancellationToken)
    {
        var emailEvent = new EmailOnlySenderEvent(emailRequest);
        await _notificationManager.PublishAsync(emailEvent, _options.ServiceBusTopic);
    }

    private async Task<MailAttachmentDto?> BuildTermsPdfAttachmentAsync(TermsEntity terms, CancellationToken cancellationToken)
    {
        try
        {
            if (terms is null || string.IsNullOrWhiteSpace(terms.LinkFr))
            {
                _logger.LogWarning("[ComplianceEmailService] Aucun CGU trouvé pour l'attachement");
                return null;
            }

            var fileResult = await _blobStorageManager.GetFileAsync(terms.LinkFr, cancellationToken);
            if (fileResult is null)
            {
                _logger.LogWarning("[ComplianceEmailService] Fichier CGU non trouvé: {FileName}", terms.LinkFr);
                return null;
            }

            using var memoryStream = new MemoryStream();
            await fileResult.FileStream.CopyToAsync(memoryStream, cancellationToken);
            var base64Content = Convert.ToBase64String(memoryStream.ToArray());

            var fileName = Path.GetFileName(terms.LinkFr);

            _logger.LogInformation("[ComplianceEmailService] Attachement CGU ajouté: {FileName}", fileName);

            return new MailAttachmentDto
            {
                FileName = fileName,
                Content = base64Content,
                Type = MailAttachmentType.Pdf
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ComplianceEmailService] Erreur lors de la récupération du PDF CGU");
            return null;
        }
    }

    private static string BuildUserName(string? firstName, string? lastName)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(firstName)) parts.Add(firstName.Trim().ToCapitalize());
        if (!string.IsNullOrWhiteSpace(lastName)) parts.Add(lastName.Trim().ToCapitalize());
        return parts.Count > 0 ? string.Join(" ", parts) : "Client";
    }

    private static string FormatSubscriptionPrice(decimal? price)
    {
        var value = price ?? 0;

        if (value == 0)
        {
            return "Offert";
        }

        return FormatPrice(value) + " / mois";
    }

    private static string FormatCompliancePrice(decimal? price)
    {
        var value = price ?? 0;

        if (value == 0)
        {
            return string.Empty;
        }

        return FormatPrice(value);
    }

    private static string FormatPrice(decimal? price)
    {
        var value = price ?? 0;
        var format = value == decimal.Truncate(value) ? "0" : "0.00";
        return value.ToString(format, FrenchCulture) + "€ HT";
    }

    public async Task SendCustomerRelationRefusalEmailAsync(CustomerRelationRefusalContext context, CancellationToken cancellationToken = default)
    {
        var variables = BuildCustomerRelationRefusalEmailVariables(context);
        var emailRequest = BuildEmailRequest(_options.CustomerRelationRefusalRecipientEmail, _options.CustomerRelationRefusalTemplateName, variables);

        await PublishEmailAsync(emailRequest, cancellationToken);
    }

    private static Dictionary<string, object> BuildCustomerRelationRefusalEmailVariables(CustomerRelationRefusalContext context)
    {
        return new Dictionary<string, object>
        {
            { "clientFullName", BuildUserName(context.UserFirstName, context.UserLastName) },
            { "refusalDate", DateTime.UtcNow.ToString("dd/MM/yyyy") },
            { "folderCodes", string.Join(", ", context.AccountNumbers) },
            { "clientEmail", context.UserEmail }
        };
    }
}
