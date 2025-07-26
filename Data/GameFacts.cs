using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpaceEditor.Data;

public static class GameFacts
{
    public const string MainDll = "SpaceEngineers2";

    public static ReadOnlySpan<string> WellKnownGameBins => new[]
    {
        "VRage.Library",
        "VRage.Input",
        "VRage.Core",
    };

    public static string GetDefaultAppDataPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), MainDll);
    }

    public static string GetBlueprintImportsPath()
    {
        return Path.Combine(GetDefaultAppDataPath(), "AppData", "SE1GridsToImport");
    }

    public static string GetBinsPath(string baseGamePath)
    {
        var exe = MainDll + ".exe";
        var exePath = TryFindTargetPath(baseGamePath, ["Game2"], exe);
        return Path.GetDirectoryName(exePath ?? throw new Exception($"Bins not found in {baseGamePath}"))!;
    }

    public static string GetContentPath(string baseGamePath)
    {
        var content = TryFindTargetPath(baseGamePath, ["GameData", "Vanilla", "Content"]);
        return content ?? throw new Exception($"Content not found in {baseGamePath}");
    }

    public static string GetMappingFile(string baseGamePath)
    {
        var content = GetContentPath(baseGamePath);
        var inputs = TryFindTargetPath(content, ["MainMenuData"], "ActionControlMapping.def");
        return inputs ?? throw new Exception($"Inputs not found in {baseGamePath}");
    }

    public static string GetActionsPath(string baseGamePath)
    {
        var mapping = GetMappingFile(baseGamePath);
        var inputs = TryFindTargetPath(Path.GetDirectoryName(mapping), ["Inputs"]);
        return inputs ?? throw new Exception($"Count not find Inputs subfolder in {baseGamePath}");
    }

    public static string? TryFindTargetPath
    (
        string baseDirectory,
        Span<string> subDirectories,
        string? targetFile = null,
        int remainingSearchDepth = 100
    )
    {
        if (remainingSearchDepth-- < 0)
        {
            // Prevents infinite recursion vai links
            return null;
        }

        if (subDirectories.IsEmpty)
        {
            if (string.IsNullOrEmpty(targetFile))
            {
                return baseDirectory;
            }

            var targetPath = Path.Combine(baseDirectory, targetFile);
            if (File.Exists(targetPath))
                return targetPath;
        }

        if (subDirectories.IsEmpty == false)
        {
            var currentDirToSearchFor = subDirectories[0];
            var testDir = Path.Combine(baseDirectory, currentDirToSearchFor);
            if (Directory.Exists(testDir))
            {
                if (TryFindTargetPath(testDir, subDirectories[1..], targetFile, remainingSearchDepth) is {} result)
                    return result;
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(baseDirectory))
        {
            if (TryFindTargetPath(dir, subDirectories, targetFile, remainingSearchDepth) is {} result)
                return result;
        }

        return null;
    }
}