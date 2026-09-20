using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NetPulse.Models;

namespace NetPulse.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DiagnosticStatus status)
        {
            return status switch
            {
                DiagnosticStatus.Success => (SolidColorBrush)new BrushConverter().ConvertFrom("#10B981")!, // Green
                DiagnosticStatus.Warning => (SolidColorBrush)new BrushConverter().ConvertFrom("#F59E0B")!, // Orange/Amber
                DiagnosticStatus.Danger => (SolidColorBrush)new BrushConverter().ConvertFrom("#EF4444")!,  // Red
                DiagnosticStatus.Running => (SolidColorBrush)new BrushConverter().ConvertFrom("#3B82F6")!, // Blue
                DiagnosticStatus.Info => (SolidColorBrush)new BrushConverter().ConvertFrom("#6366F1")!,    // Indigo
                _ => (SolidColorBrush)new BrushConverter().ConvertFrom("#64748B")!                         // Slate Gray
            };
        }
        return (SolidColorBrush)new BrushConverter().ConvertFrom("#64748B")!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusToBgColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DiagnosticStatus status)
        {
            return status switch
            {
                DiagnosticStatus.Success => (SolidColorBrush)new BrushConverter().ConvertFrom("#142E25")!,
                DiagnosticStatus.Warning => (SolidColorBrush)new BrushConverter().ConvertFrom("#332414")!,
                DiagnosticStatus.Danger => (SolidColorBrush)new BrushConverter().ConvertFrom("#331618")!,
                DiagnosticStatus.Running => (SolidColorBrush)new BrushConverter().ConvertFrom("#142238")!,
                DiagnosticStatus.Info => (SolidColorBrush)new BrushConverter().ConvertFrom("#1E1E38")!,
                _ => (SolidColorBrush)new BrushConverter().ConvertFrom("#1E293B")!
            };
        }
        return (SolidColorBrush)new BrushConverter().ConvertFrom("#1E293B")!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusToBadgeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DiagnosticStatus status)
        {
            return status switch
            {
                DiagnosticStatus.Success => Localization.LocalizationService.Get("StatusBadgeOk"),
                DiagnosticStatus.Warning => Localization.LocalizationService.Get("StatusBadgeWarning"),
                DiagnosticStatus.Danger => Localization.LocalizationService.Get("StatusBadgeDanger"),
                DiagnosticStatus.Running => Localization.LocalizationService.Get("StatusBadgeRunning"),
                DiagnosticStatus.Info => Localization.LocalizationService.Get("StatusBadgeInfo"),
                _ => Localization.LocalizationService.Get("StatusBadgePending")
            };
        }
        return Localization.LocalizationService.Get("StatusBadgeUnknown");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}


public class ScoreToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int score)
        {
            if (score >= 80) return (SolidColorBrush)new BrushConverter().ConvertFrom("#10B981")!;
            if (score >= 50) return (SolidColorBrush)new BrushConverter().ConvertFrom("#F59E0B")!;
            return (SolidColorBrush)new BrushConverter().ConvertFrom("#EF4444")!;
        }
        return (SolidColorBrush)new BrushConverter().ConvertFrom("#64748B")!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        bool invert = Invert || string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase);
        if (invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }
}

public class StringToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasContent = value is string str && !string.IsNullOrWhiteSpace(str);
        if (Invert) hasContent = !hasContent;
        return hasContent ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool notNull = value != null;
        if (Invert) notNull = !notNull;
        return notNull ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class IndexToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && int.TryParse(parameter?.ToString(), out int targetIndex))
        {
            return index == targetIndex;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && int.TryParse(parameter?.ToString(), out int targetIndex))
        {
            return targetIndex;
        }
        return Binding.DoNothing;
    }
}

public class StringEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked)
        {
            if (targetType.IsEnum && parameter != null &&
                Enum.TryParse(targetType, parameter.ToString(), true, out var enumVal))
            {
                return enumVal;
            }
            return parameter?.ToString() ?? string.Empty;
        }
        return Binding.DoNothing;
    }
}

public class IntToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public int Threshold { get; set; } = 0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int count = 0;
        if (value is int i) count = i;
        else if (value != null && int.TryParse(value.ToString(), out int parsed)) count = parsed;

        bool isVisible = count > Threshold;
        if (Invert) isVisible = !isVisible;
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusToQuoteBgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DiagnosticStatus status)
        {
            return status switch
            {
                DiagnosticStatus.Warning => (SolidColorBrush)new BrushConverter().ConvertFrom("#221808")!,
                DiagnosticStatus.Danger => (SolidColorBrush)new BrushConverter().ConvertFrom("#280F13")!,
                _ => (SolidColorBrush)new BrushConverter().ConvertFrom("#0F172A")!
            };
        }
        return (SolidColorBrush)new BrushConverter().ConvertFrom("#0F172A")!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusToQuoteTextColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DiagnosticStatus status)
        {
            return status switch
            {
                DiagnosticStatus.Warning => (SolidColorBrush)new BrushConverter().ConvertFrom("#FEF08A")!,
                DiagnosticStatus.Danger => (SolidColorBrush)new BrushConverter().ConvertFrom("#FCA5A5")!,
                _ => (SolidColorBrush)new BrushConverter().ConvertFrom("#CBD5E1")!
            };
        }
        return (SolidColorBrush)new BrushConverter().ConvertFrom("#CBD5E1")!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusToQuoteIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DiagnosticStatus status)
        {
            return status switch
            {
                DiagnosticStatus.Warning => "⚠️",
                DiagnosticStatus.Danger => "⛔",
                DiagnosticStatus.Success => "✔",
                _ => "ℹ️"
            };
        }
        return "⚠️";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}


