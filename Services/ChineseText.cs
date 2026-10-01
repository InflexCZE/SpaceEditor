using System.Text.RegularExpressions;

namespace SpaceEditor.Services;

/// <summary>只转换界面显示名称，保留游戏对象、属性和序列化标识。</summary>
public static class ChineseText
{
    public static string PropertyName(string name) => LocalizationManager.NameTranslation(name) ?? DisplayName(name);

    /// <summary>动态游戏名称附带原文，方便核对，并兼容未收录的新名称。</summary>
    public static string DisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        if (LocalizationManager.NameTranslation(name) is { } exact)
            return exact == name ? name : $"{exact}（{name}）";

        var spaced = Regex.Replace(name, @"([a-z0-9])([A-Z])", "$1 $2");
        spaced = Regex.Replace(spaced, @"([A-Z])([A-Z][a-z])", "$1 $2");
        var translated = Regex.Replace(spaced, @"[A-Za-z]+", match => LocalizationManager.NameTranslation(match.Value) ?? match.Value);
        return translated == spaced ? name : $"{translated}（{name}）";
    }
}
