// pyright file="SignatoryAccessImportMap.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using CsvHelper.Configuration;
using Pennylane.Application.Helpers;
using Pennylane.Application.Models.Signatory;

namespace Pennylane.Application.Mappers;

/// <summary>
/// Mapping CsvHelper pour SignatoryAccessImportRowDto.
/// </summary>
public sealed class SignatoryAccessImportMap : ClassMap<SignatoryAccessImportRow>
{
    public SignatoryAccessImportMap()
    {
        Map(m => m.AccountNumber).Name("Code dossier");
        Map(m => m.Email).Name("Email signataire");
        Map(m => m.LastName).Name("Nom signataire");
        Map(m => m.FirstName).Name("Prénom signataire");
        Map(m => m.PlanName).Name("Nom du plan Pennylane");
        Map(m => m.UserCount).Name("Nombre d'utilisateurs Pennylane").TypeConverter<IntZeroConverter>();
        Map(m => m.PlanPriceExclTax).Name("Prix HT du plan Pennylane").TypeConverter<DecimalZeroConverter>();
        Map(m => m.CompliancePriceExclTax).Name("Prix HT de la mise en conformité").TypeConverter<DecimalZeroConverter>();
        Map(m => m.FreeTrialEndDate).Name("Date de fin de gratuité");
        Map(m => m.Wave).Name("Vague").Optional();
    }
}