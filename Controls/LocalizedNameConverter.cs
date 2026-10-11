using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SpaceEditor.Services;

namespace SpaceEditor.Controls;

/// <summary>Translates only the displayed text; SelectedValue stays the original key or game object.</summary>
public sealed class LocalizedNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is not string name)
            return string.Empty;
        if (parameter is "BlockSize")
            return LocalizationManager.TranslateName(name) ?? name;
        return parameter is "Preset" && name == "Current"
            ? LocalizationManager.Text("CurrentPreset")
            : parameter is "Preset" ? name : LocalizationManager.DisplayName(name);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => targetTypes.Select(_ => Binding.DoNothing).ToArray();

    public static DataTemplate Template(string path, object? parameter = null)
    {
        var binding = new MultiBinding { Converter = new LocalizedNameConverter(), Mode = BindingMode.OneWay, ConverterParameter = parameter };
        binding.Bindings.Add(new Binding(path));
        binding.Bindings.Add(new Binding(nameof(LocalizationManager.LanguageCode)) { Source = LocalizationManager.Current });
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, binding);
        return new DataTemplate { VisualTree = text };
    }
}
