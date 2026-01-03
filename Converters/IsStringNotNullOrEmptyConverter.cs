using System.Globalization;

namespace KopioRapido.Converters;

public class IsStringNotNullOrEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNotEmpty = !string.IsNullOrEmpty(value as string);

        // If parameter is "inverse", return opposite
        if (parameter?.ToString() == "inverse")
        {
            return !isNotEmpty;
        }

        return isNotEmpty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
