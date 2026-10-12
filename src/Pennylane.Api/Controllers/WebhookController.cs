// <copyright file="WebhookController.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pennylane.Application.Configurations.Options;
using Pennylane.Application.Consts;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models.Webhook;
using Pulse.ExceptionMiddleware.Exceptions;

namespace Pennylane.WebApi.Controllers;

[ApiController]
[Route("webhooks/pennylane/events")]
/// <summary>
/// Controller for handling incoming Pennylane webhook events.
/// </summary>
/// <remarks>
/// This controller receives and processes webhook events sent by Pennylane.
/// </remarks>
public class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    private readonly IOptions<PennylaneServiceOptions> _options;
    private readonly IWebhookProcessingService _webhookProcessingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookController"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging operations.</param>
    /// <param name="options">The Pennylane service options.</param>
    public WebhookController(ILogger<WebhookController> logger, IOptions<PennylaneServiceOptions> options, IWebhookProcessingService webhookProcessingService)
    {
        _logger = logger;
        _options = options;
        _webhookProcessingService = webhookProcessingService;
    }

    /// <summary>
    /// Handles incoming webhooks from Pennylane.
    /// </summary>
    /// <param name="token">The authorization token.</param>
    /// <param name="payload">The webhook payload.</param>
    /// <returns>An <see cref="IActionResult"/> representing the result of the operation.</returns>
    [HttpPost]
    public async Task<IActionResult> HandleWebhook([FromQuery] string token, [FromBody] WebhookPayload payload)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token) || !_options.Value.Token.Equals(token))
            {
                _logger.LogWarning("Unauthorized webhook call with token: {Token}", token);
                return new UnauthorizedObjectResult("Invalid token.");
            }

            _logger.LogInformation("Received event: {Payload}", payload);

            string normalizedEvent = payload?.Event != null ? payload.Event.Replace("_", ".") : string.Empty;

            if (normalizedEvent != PennylaneEventNames.UserCreatedEvent && normalizedEvent != PennylaneEventNames.UserUpdatedEvent)
            {
                return BadRequest("Event not accepted");
            }

            await _webhookProcessingService.ProcessEvent(normalizedEvent, payload);

            return Ok(new { status = "success" });
        }
        catch (BadRequestException ex)
        {
            _logger.LogError(ex, "BadRequestException processing webhook event: {Event}", payload?.Event);
            return BadRequest(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HttpRequestException occurred while processing webhook event: {Event}", payload?.Event);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Service unavailable, please retry later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing webhook event: {Event}", payload?.Event);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Internal server error." });
        }
    }
}
