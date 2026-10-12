// <copyright file="SaasNames.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.ComponentModel;

namespace Pennylane.Application.Consts.Enums;

public enum SaasNames
{
    [Description("v1_basic")]
    v1_basic,

    [Description("v1_saas_free")]
    v1_saas_free,

    [Description("v1_essential")]
    v1_essential,

    [Description("v1_pro")]
    v1_pro,

    [Description("v1_starter")]
    v1_starter,

    [Description("v1_pro_internalized")]
    v1_pro_internalized,

    [Description("v1_electronic_invoicing")]
    v1_electronic_invoicing,
}
