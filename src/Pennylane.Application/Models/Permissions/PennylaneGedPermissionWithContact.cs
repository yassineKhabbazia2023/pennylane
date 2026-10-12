// <copyright file="PennylaneGedPermissionWithContact.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Models.Permissions;

public record PennylaneGedPermissionWithContact(
    int ContactId,
    int AccountId,
    string Email,
    string? FirstName,
    string? LastName,
    string? ContactType);
