using System.Globalization;

namespace KopioRapido.Converters;

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            // If parameter is "inverse", return true when count is 0
            if (parameter?.ToString() == "inverse")
            {
                return count == 0;
            }
            // Default: return true when count > 0
            return count > 0;
        }
        
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
