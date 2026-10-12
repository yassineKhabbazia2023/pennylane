using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Pennylane.Application.Helpers;

public class DecimalFrConverter : DefaultTypeConverter
{
    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        => ((decimal)value).ToString("N2", CultureInfo.GetCultureInfo("fr-FR"));

    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("fr-FR"), out var result))
            return result;
        return 0m;
    }
}
