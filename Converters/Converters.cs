using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AuraCoursePlanner.Converters;

/// <summary>Converts a 0-100 percentage double into a 0-1 ratio, clamped to that range,
/// for use with a ViewBox/ScaleTransform or a progress bar's ScaleX.</summary>
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
        return System.Windows.Application.Current.TryFindResource(key) as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.White;
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
    {
        // Bug fix: this used to `return parameter` (a raw string like "High"),
        // which WPF cannot assign to an enum-typed source property, so the
        // radio button never actually updated the bound enum. `targetType`
        // in ConvertBack is the *source* property's type, so parse against it.
        if (value is not bool isChecked || !isChecked || parameter is null)
            return System.Windows.Data.Binding.DoNothing;

        return Enum.TryParse(targetType, parameter.ToString(), out var parsed)
            ? parsed
            : System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>Dims an element to 35% opacity when the bound bool is false — used for
/// the 7-day "S M T W T F S" scheduled-days indicator on course cards.</summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? 1.0 : 0.35;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}