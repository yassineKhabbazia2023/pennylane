using System.Globalization;
using Pennylane.Application.Helpers;
using Pennylane.Application.Models.Signatory;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace Pennylane.Application.Tests.Helpers;

public class CsvFileReaderTests
{
    private const string CsvHeaderFr = 
        "Code dossier;Email signataire;Nom signataire;Prénom signataire;" +
        "Nom du plan Pennylane;Nombre d'utilisateurs Pennylane;" +
        "Prix HT du plan Pennylane;Prix HT de la mise en conformité";

    private Stream GenerateCsvStream(string csvContent)
    {
        var bytes = Encoding.UTF8.GetBytes(csvContent);
        return new MemoryStream(bytes);
    }

    [Fact]
    public void ReadFrenchDecimalComma_Success()
    {
        // Arrange
        var csv = $"{CsvHeaderFr}\nACC1;test@test.com;Nom;Prénom;Plan;5;120,99;50,00";
        using var stream = GenerateCsvStream(csv);

        // Act
        var result = CsvFileReader.ReadStreamAsync<SignatoryAccessImportRow>(stream).ToList();

        // Assert
        Assert.Equal(2, result.Count); // Header (1) + Data (1)
        var dataRow = result[1].Item1;
        Assert.Equal(120.99m, dataRow.PlanPriceExclTax);
        Assert.Equal(50m, dataRow.CompliancePriceExclTax);
    }

    [Fact]
    public void ReadPointDecimal_ThrowsInvalidDataException()
    {
        // Arrange
        var csv = $"{CsvHeaderFr}\nACC2;email@test.com;Nom;Prénom;Plan;5;120.99;50,00"; // POINT
        using var stream = GenerateCsvStream(csv);

        // Act & Assert
        var ex = Assert.Throws<InvalidDataException>(() => 
            CsvFileReader.ReadStreamAsync<SignatoryAccessImportRow>(stream).ToList());
        
        Assert.Contains("séparateur décimal '.' interdit", ex.Message);
        Assert.Contains("colonne 'Prix HT du plan Pennylane'", ex.Message);
        Assert.Contains("Ligne 2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadZeroComma_Success()
    {
        // Arrange
        var csv = $"{CsvHeaderFr}\nACC3;email@test.com;Nom;Prénom;Plan;5;0,00;0,00";
        using var stream = GenerateCsvStream(csv);

        // Act
        var result = CsvFileReader.ReadStreamAsync<SignatoryAccessImportRow>(stream).ToList();

        // Assert
        Assert.Equal(0m, result[1].Item1.PlanPriceExclTax);
        Assert.Equal(0m, result[1].Item1.CompliancePriceExclTax);
    }

    [Fact]
    public void PointInText_Ignored()
    {
        // Arrange
        var csv = $"{CsvHeaderFr}\nACC.4;test.user@example.com;Nom;Prénom;Plan;5;10,00;5,00"; // Point OK texte
        using var stream = GenerateCsvStream(csv);

        // Act
        var result = CsvFileReader.ReadStreamAsync<SignatoryAccessImportRow>(stream).ToList();

        // Assert
        Assert.NotNull(result[1].Item1.Email);
        Assert.Equal("test.user@example.com", result[1].Item1.Email);
        Assert.Equal("ACC.4", result[1].Item1.AccountNumber);
    }

    [Fact]
    public void EmptyDecimal_Zero()
    {
        // Arrange
        var csv = $"{CsvHeaderFr}\nACC5;email@test.com;Nom;Prénom;Plan;5;;";
        using var stream = GenerateCsvStream(csv);

        // Act
        var result = CsvFileReader.ReadStreamAsync<SignatoryAccessImportRow>(stream).ToList();

        // Assert
        Assert.Equal(0m, result[1].Item1.PlanPriceExclTax);
        Assert.Equal(0m, result[1].Item1.CompliancePriceExclTax);
    }

    [Fact]
    public void HeaderValidation_Passes()
    {
        // Arrange
        var csv = $"{CsvHeaderFr}\nACC6;email@test.com;Nom;Prénom;Plan;5;120,99;50,00";
        using var stream = GenerateCsvStream(csv);

        // Act
        var result = CsvFileReader.ReadStreamAsync<SignatoryAccessImportRow>(stream).ToList();

        // Assert
        // result[0] est le header, Item2 doit être 1 (rowNumber)
        Assert.Equal(1, result[0].Item2); 
        Assert.Equal("Code dossier", result[0].Item3[0]);
    }

    [Fact]
    public void ReadRealLifeFormat_Success()
    {
        // Arrange
        var csv = $"{CsvHeaderFr}\n" +
                  "1000232237;NULL;NULL;NULL;;;;\n" +
                  "1000617241;fmanadi@test.com;Manadi;Fares;Essentiel;6;565,67;120";
        using var stream = GenerateCsvStream(csv);

        // Act
        var result = CsvFileReader.ReadStreamAsync<SignatoryAccessImportRow>(stream).ToList();

        // Assert
        Assert.Equal(3, result.Count); // Header + 2 rows
        
        var row1 = result[1].Item1;
        Assert.Equal("1000232237", row1.AccountNumber);
        Assert.Equal(0m, row1.PlanPriceExclTax);

        var row2 = result[2].Item1;
        Assert.Equal("1000617241", row2.AccountNumber);
        Assert.Equal(565.67m, row2.PlanPriceExclTax);
        Assert.Equal(120m, row2.CompliancePriceExclTax);
    }
    
    [Fact]
    public void DebugFrenchDecimalRecords()
    {
        var csvContent = "Label;Valeur\nPlanA;565,67";

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, new CsvConfiguration(new CultureInfo("fr-FR"))
        {
            Delimiter = ";",
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null
        });

        var records = csv.GetRecords<DecimalTestRow>().ToList();
        var value = records[0].Valeur;

        Assert.Equal(565.67m, value);  // doit passer
        Console.WriteLine($"Parsed: {value}");  // affiche 565,67 en FR
    }
    
    private sealed class DecimalTestRow
    {
        public string Label { get; set; } = "";
        public decimal Valeur { get; set; }
    }

}
