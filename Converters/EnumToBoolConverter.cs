using System.Globalization;
using KopioRapido.Models;

namespace KopioRapido.Converters;

public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
        {
            return false;
        }

        var enumValue = value.ToString();
        var paramValue = parameter.ToString();

        return enumValue?.Equals(paramValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter == null)
        {
            return CopyOperationType.Copy;
        }

        if (Enum.TryParse<CopyOperationType>(parameter.ToString(), out var result))
        {
            return result;
        }

        return CopyOperationType.Copy;
    }
}
