// <copyright file="SignatoryAccessCsvConfig.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Models.Signatory;

namespace Pennylane.Application.Configuration;

/// <summary>
/// Configuration et validation du format CSV pour les signataires.
/// </summary>
public static class SignatoryAccessCsvConfig
{
    private static readonly string[] RequiredHeaders = new[]
    {
        "Code dossier",
        "Email signataire",
        "Nom signataire",
        "Prénom signataire",
        "Nom du plan Pennylane",
        "Nombre d'utilisateurs Pennylane",
        "Prix HT du plan Pennylane",
        "Prix HT de la mise en conformité",
        "Date de fin de gratuité",
        "Vague",
    };

    /// <summary>
    /// Vérifie si le format CSV est valide.
    /// </summary>
    public static bool IsValidCsvFormat(
        List<(SignatoryAccessImportRow, int, string[])> csvData,
        Type modelType,
        out string messageError)
    {
        messageError = string.Empty;

        if (csvData == null || csvData.Count == 0)
        {
            messageError = "Le fichier CSV ne contient aucune ligne de données.";
            return false;
        }

        var firstRow = csvData.First();
        var headers = firstRow.Item3;

        if (headers == null || headers.Length == 0)
        {
            messageError = "Le fichier CSV ne contient pas de ligne d'en-tête.";
            return false;
        }

        var expectedColumnCount = RequiredHeaders.Length;

        if (headers.Length != expectedColumnCount)
        {
            messageError = string.Format("Header invalide : Nombre de colonnes invalide. Attendu: {0}, Reçu: {1}", expectedColumnCount, headers.Length);
            return false;
        }

        for (int i = 0; i < RequiredHeaders.Length; i++)
        {
            var receivedHeader = headers[i]?.Trim() ?? string.Empty;
            var expectedHeader = RequiredHeaders[i];

            if (!expectedHeader.Equals(receivedHeader, StringComparison.OrdinalIgnoreCase))
            {
                messageError = string.Format("Header invalide : Colonne {0} ('{1}') ne correspond pas à '{2}'", i + 1, receivedHeader, expectedHeader);
                return false;
            }
        }

        for (int i = 0; i < csvData.Count; i++)
        {
            var lineColumns = csvData[i].Item3;
            var lineNumber = csvData[i].Item2;

            if (lineColumns.Length != expectedColumnCount)
            {
                messageError = string.Format("Nombre de colonnes invalide : Ligne {0} a {1} colonnes au lieu de {2}", lineNumber, lineColumns.Length, expectedColumnCount);
                return false;
            }
        }

        return true;
    }
}
