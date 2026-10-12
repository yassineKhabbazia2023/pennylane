// <copyright file="User.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace Pennylane.Application.Models.Webhook;

public class User
{
    public required string Id { get; set; }
   
    public required string Email { get; set; }
    
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    public string? Role { get; set; }

    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("assigned_to_firm")]
    public bool AssignedToFirm { get; set; }

    [JsonPropertyName("company_ids")]
    public List<string>? CompanyIds { get; set; }

    [JsonPropertyName("sub_role")]
    public string? SubRole { get; set; }
}
