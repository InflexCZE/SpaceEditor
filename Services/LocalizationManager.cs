using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace SpaceEditor.Services;

/// <summary>切换界面资源，不改变游戏数据或进程的序列化区域设置。</summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Current { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    private string languageCode = "en";
    private ResourceDictionary? languageResources;
    private Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);

    public string LanguageCode
    {
        get => this.languageCode;
        set
        {
            if (value is not ("en" or "zh-CN") || value == this.languageCode)
                return;

            ResourceDictionary? dictionary = null;
            var translatedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (value == "zh-CN")
            {
                // 外部语言包优先；缺失时使用随程序集附带的翻译。
                using var pack = LoadPack();
                dictionary = new ResourceDictionary();
                if (pack.RootElement.TryGetProperty("strings", out var strings) && strings.ValueKind == JsonValueKind.Object)
                    foreach (var item in strings.EnumerateObject())
                        if (item.Value.ValueKind == JsonValueKind.String)
                            dictionary[item.Name] = item.Value.GetString()!;
                if (pack.RootElement.TryGetProperty("names", out var nameEntries) && nameEntries.ValueKind == JsonValueKind.Object)
                    foreach (var item in nameEntries.EnumerateObject())
                        if (item.Value.ValueKind == JsonValueKind.String)
                            translatedNames[item.Name] = item.Value.GetString()!;
            }

            var resources = Application.Current.Resources.MergedDictionaries;
            if (this.languageResources is not null)
                resources.Remove(this.languageResources);
            if (dictionary is not null)
                resources.Add(dictionary);
            this.languageResources = dictionary;
            this.names = translatedNames;
            this.languageCode = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageCode)));
        }
    }

    public static string Text(string key, params object[] args)
    {
        var text = Application.Current.TryFindResource(key) as string ?? key;
        return args.Length == 0 ? text : string.Format(text, args);
    }

    public static string DisplayName(string name) => Current.LanguageCode == "zh-CN" ? ChineseText.DisplayName(name) : name;
    public static string PropertyName(string name) => Current.LanguageCode == "zh-CN" ? ChineseText.PropertyName(name) : name;
    internal static string? NameTranslation(string name) => Current.names.GetValueOrDefault(name);

    private static JsonDocument LoadPack()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Localization", "zh-CN.json");
        try
        {
            if (File.Exists(path))
            {
                using var external = File.OpenRead(path);
                var document = JsonDocument.Parse(external);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                    return document;
                document.Dispose();
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }

        using var embedded = typeof(LocalizationManager).Assembly.GetManifestResourceStream("SpaceEditor.Localization.zh-CN.json")!;
        return JsonDocument.Parse(embedded);
    }
}
