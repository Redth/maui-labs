using System.Globalization;

namespace AIAttributes.Sample.Garden.Converters;

/// <summary>
/// Maps <c>true</c> to a dimmed opacity (e.g. for ghosting pending-removal
/// items) and <c>false</c> to fully opaque.
/// </summary>
public sealed class BoolToDimOpacityConverter : IValueConverter
{
    public double DimOpacity { get; set; } = 0.35;
    public double FullOpacity { get; set; } = 1.0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b ? DimOpacity : FullOpacity;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
