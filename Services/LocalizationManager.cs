using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;

namespace SpaceEditor.Services;

public sealed record Language(string Code, string ResourcePath)
{
    public ResourceDictionary Resources { get; } = new() { Source = new Uri("/SpaceEditor;component/" + ResourcePath, UriKind.Relative) };
    public string DisplayName => (string) this.Resources["LanguageName"];
}

public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static IReadOnlyList<Language> Languages { get; } =
    [
        new("en", "Localization/Strings.en.xaml"),
        new("zh-CN", "Localization/Strings.zh-CN.xaml"),
    ];

    public static LocalizationManager Current { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    private Language language = Languages[0];

    public string LanguageCode
    {
        get => this.language.Code;
        set
        {
            var next = Languages.FirstOrDefault(x => x.Code == value);
            if (next is null || ReferenceEquals(next, this.language))
                return;

            // English stays merged from App.xaml underneath, so keys missing in the selected language fall back to it
            var resources = Application.Current.Resources.MergedDictionaries;
            resources.Remove(this.language.Resources);
            resources.Add(next.Resources);
            this.language = next;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageCode)));
        }
    }

    public static string Text(string key, params object[] args)
    {
        var text = Application.Current.TryFindResource(key) as string ?? key;
        return args.Length == 0 ? text : string.Format(text, args);
    }

    public static string? TranslateName(string name) => Application.Current.TryFindResource("Name." + name) as string;

    public static string PropertyName(string name) => TranslateName(name) ?? DisplayName(name);

    public static string DisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        if (TranslateName(name) is { } exact)
            return exact == name ? name : Text("NameWithOriginal", exact, name);

        var spaced = Regex.Replace(name, @"([a-z0-9])([A-Z])", "$1 $2");
        spaced = Regex.Replace(spaced, @"([A-Z])([A-Z][a-z])", "$1 $2");
        var translated = Regex.Replace(spaced, @"[A-Za-z]+", match => TranslateName(match.Value) ?? match.Value);
        return translated == spaced ? name : Text("NameWithOriginal", translated, name);
    }
}
