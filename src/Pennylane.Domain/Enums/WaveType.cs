// <copyright file="WaveType.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Domain.Enums;

/// <summary>
/// Classification des dossiers par type de vague d'activation.
/// Énumération des types de vagues supportées pour la segmentation des entreprises.
/// Note : null = dossier régulier/habituel (non foodservice)
/// </summary>
public enum WaveType
{
    /// <summary>
    /// Dossier de type restauration/foodservice.
    /// </summary>
    Restauration
}

