// pyright file="CsvFileReader.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using CsvHelper;
using CsvHelper.Configuration;
using Pennylane.Application.Models.Signatory;
using System.Globalization;
using System.Text;
using Pennylane.Application.Mappers;

namespace Pennylane.Application.Helpers;

/// <summary>
/// Helper pour lire des fichiers CSV avec CsvHelper.
/// </summary>
public static class CsvFileReader
{
    private static readonly CsvConfiguration csvConfiguration = new CsvConfiguration(new CultureInfo("fr-FR"))
    {
        Delimiter = ";",
        Quote = '"',
        Escape = '"',
        Mode = CsvMode.RFC4180,
        HasHeaderRecord = true,
        TrimOptions = TrimOptions.Trim,
        Encoding = Encoding.UTF8,
        BadDataFound = args =>
        {
            Console.WriteLine($"BadDataFound: Bad entry found at field {args.Field}, \n : {args.RawRecord?.Replace("\"", "'")}");
        },
        HeaderValidated = null,
        MissingFieldFound = null,
        IgnoreBlankLines = true,
    };

    /// <summary>
    /// Lit un stream CSV et retourne les enregistrements avec métadonnées.
    /// </summary>
    /// <typeparam name="T">Type du modèle à mapper.</typeparam>
    /// <param name="stream">Stream du fichier CSV.</param>
    /// <param name="delimeter">Délimiteur de colonnes (par défaut: ';').</param>
    /// <returns>Énumération de tuples (Modèle, NuméroLigne, ColonnesBrutes).</returns>
    public static IEnumerable<(T, int, string[])> ReadStreamAsync<T>(Stream stream, string? delimeter = ";")
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        
        // Configurer le délimiteur
        csvConfiguration.Delimiter = delimeter ?? ";";
        
        using var csv = new CsvReader(reader, csvConfiguration);

        if (typeof(T) == typeof(SignatoryAccessImportRow))
            csv.Context.RegisterClassMap<SignatoryAccessImportMap>();

        // Traiter les chaines vides et "NULL" comme 0 pour les numériques
        var options = new CsvHelper.TypeConversion.TypeConverterOptions { NullValues = { } };
        csv.Context.TypeConverterOptionsCache.AddOptions<decimal>(options);
        csv.Context.TypeConverterOptionsCache.AddOptions<decimal?>(options);
        csv.Context.TypeConverterOptionsCache.AddOptions<int>(options);
        csv.Context.TypeConverterOptionsCache.AddOptions<int?>(options);

        csv.Context.TypeConverterCache.AddConverter<decimal>(new DecimalZeroConverter());
        csv.Context.TypeConverterCache.AddConverter<decimal?>(new DecimalZeroConverter());
        csv.Context.TypeConverterCache.AddConverter<int>(new IntZeroConverter());
        csv.Context.TypeConverterCache.AddConverter<int?>(new IntZeroConverter());

        // Lire et retourner le header en premier
        csv.Read();
        csv.ReadHeader();
        var headerRecord = csv.Parser.Record ?? csv.HeaderRecord ?? Array.Empty<string>();
        
        // Yield header avec Item1 = default(T), Item2 = 1, Item3 = colonnes du header
        yield return (default(T)!, 1, headerRecord);

        // Lire les lignes de données
        int rowNumber = 1;
        while (csv.Read())
        {
            rowNumber++;
            
            //   NOUVELLE VALIDATION : Vérifier les séparateurs décimaux sur CHAQUE ligne
            var currentRow = csv.Parser.Record ?? Array.Empty<string>();
            for (int i = 0; i < currentRow.Length; i++)
            {
                var cell = currentRow[i];
                if (IsInvalidDecimalPoint(cell))
                {
                    var columnName = (headerRecord.Length > i) ? headerRecord[i] : $"Col {i + 1}";
                    throw new InvalidDataException($"Ligne {rowNumber}, colonne '{columnName}' : séparateur décimal '.' interdit. Valeur reçue : '{cell}'");
                }
            }
            
            var record = csv.GetRecord<T>();
            yield return (record, rowNumber, currentRow);
        }
    }

    /// <summary>
    /// Vérifie si une cellule contient un point décimal invalide (culture FR exige virgule).
    /// </summary>
    private static bool IsInvalidDecimalPoint(string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell))
            return false;

        // Si c'est un email ou du texte alphanumérique, on ignore
        if (cell.Contains("@"))
            return false;

        // Contient un point ET ressemble à un nombre avec un point → interdit
        if (cell.Contains('.'))
        {
            return decimal.TryParse(
                cell.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out _);
        }

        return false;
    }
}
