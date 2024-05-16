using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
namespace SlowCow.Setup.UI.Converters;

public static class EqualConverter
{
    public static SameConverter Same { get; } = new ();
    public static NotSameConverter NotSame { get; } = new ();

    public class SameConverter : IValueConverter
    {
        public virtual object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // try cast parameter to value type
            if (parameter is not null && value is not null)
            {
                try
                {
                    var typedParameter = System.Convert.ChangeType(parameter, value.GetType(), culture);
                    var result = value.Equals(typedParameter);

                    return result;
                }
                catch
                {
                    if (Design.IsDesignMode)
                    {
                        Console.WriteLine($"Cannot convert {parameter} to {value.GetType()}");
                    }

                    return false;
                }
            }

            return value is null && parameter is null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public class NotSameConverter : SameConverter
    {
        public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => !(bool) base.Convert(value, targetType, parameter, culture)!;
    }
}