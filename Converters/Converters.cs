using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AuraCoursePlanner.Converters;

/// <summary>Converts a 0-100 percentage double into a proportional width, given the
/// ActualWidth of the track passed as ConverterParameter via MultiBinding in XAML instead;
/// here we expose a simple percentage-to-ratio (0-1) converter for use with a ViewBox/ScaleTransform.</summary>
public class PercentageToRatioConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double pct) return Math.Clamp(pct / 100.0, 0, 1);
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Returns the Emerald success brush unless the course is projected to miss its
/// deadline, in which case it returns the Amber warning brush.</summary>
public class MissedDeadlineToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isAtRisk = value is bool b && b;
        var key = isAtRisk ? "WarningBrush" : "SuccessBrush";
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class TimeSpanToHmConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan t) return $"{(int)t.TotalHours}h {t.Minutes}m";
        return "0h 0m";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var b = value is bool v && v;
        var invert = Invert || (parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) ?? false);
        if (invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? parameter : Binding.DoNothing;
}
