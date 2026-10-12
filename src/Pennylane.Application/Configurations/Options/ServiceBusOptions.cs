using System;
using System.ComponentModel.DataAnnotations;

namespace Pennylane.Application.Configurations.Options;

public class ServiceBusOptions
{
    public const string ServiceBus = "SyncDocumentsSetting";

    [Required(AllowEmptyStrings = false, ErrorMessage = "SyncThresholdMinutes is required.")]
    public int SyncThresholdMinutes { get; set; } = 60;
}
