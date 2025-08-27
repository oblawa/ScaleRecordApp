namespace ScaleRecordApp.Resources.Converters;

public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is not null && value is string s ? !string.IsNullOrWhiteSpace(s) : value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}