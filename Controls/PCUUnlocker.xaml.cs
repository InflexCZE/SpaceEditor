using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using SpaceEditor.Data;

namespace SpaceEditor.Controls;

/// <summary>
/// Interaction logic for PCUUnlocker.xaml
/// </summary>
public partial class PCUUnlocker : UserControl
{
    public GameProxy Game { get; }

    private const int UnlockedPCU = int.MaxValue / 2;

    public PCUUnlocker(GameProxy game)
    {
        this.Game = game;
        InitializeComponent();
    }

    private void UnlockPCU(object sender, RoutedEventArgs e)
    {
        var contents = new Dictionary<string, string>();

        bool success = true;

        if (success)
        {
            success &= UpdateFile
            (
                "PCUSessionComponentConfiguration.def",
                content =>
                {
                    return Regex.Replace(content, "(\"MaxGlobalPCU\"\\s*:\\s*)(\\d+)", $"${{1}}{UnlockedPCU}");
                }
            );
        }

        if (success)
        {
            success &= UpdateFile
            (
                "TrashRemovalConfiguration.def",
                content =>
                {
                    const string PlayergridPCU = "\"PlayerGridPCU\"";
                    const string TargetCleanupLimit = "\"TargetCleanupLimit\"";
                    const string ExecuteCleanupLimit = "\"ExecuteCleanupLimit\"";
                    
                    var gridSectionBegin = content.IndexOf(PlayergridPCU, StringComparison.InvariantCulture);
                    if (gridSectionBegin < 0)
                        return null;

                    var tclBegin = content.IndexOf(TargetCleanupLimit, gridSectionBegin, StringComparison.InvariantCulture);
                    var eclBegin = content.IndexOf(ExecuteCleanupLimit, gridSectionBegin, StringComparison.InvariantCulture);
                    if (tclBegin < 0 || eclBegin < 0)
                        return null;

                    var gridSectionEnd = content.IndexOfAny(['{', '}'], Math.Max(tclBegin, eclBegin));
                    if (gridSectionEnd < 0)
                        return null;

                    var sectionContent = content.Substring(gridSectionBegin, gridSectionEnd - gridSectionBegin);
                    sectionContent = Regex.Replace(sectionContent, $"({TargetCleanupLimit}\\s*:\\s*)(\\d+)", $"${{1}}{UnlockedPCU}");
                    sectionContent = Regex.Replace(sectionContent, $"({ExecuteCleanupLimit}\\s*:\\s*)(\\d+)", $"${{1}}{UnlockedPCU}");

                    return $"{content[..gridSectionBegin]}{sectionContent}{content[gridSectionEnd..]}";
                }
            );
        }

        if (success == false)
        {
            MessageBox.Show("Could not unlock the PCUs. Game Content mush have changed.\nUpdate to latest version of the tool");
        }

        Settings.Default.InvokeGameAction(() =>
        {
            foreach(var (file, newContent) in contents)
            {
                File.WriteAllText(file, newContent);
            }
        });
        
        bool UpdateFile(string fileName, Func<string, string?> updateFunction)
        {
            var filePath = GameFacts.TryFindTargetPath(this.Game.ContentPath, [], fileName);
            if (filePath is null)
            {
                return false;
            }

            var content = contents.GetValueOrDefault(filePath) ?? File.ReadAllText(filePath);
            
            var newContent = updateFunction(content);
            if (newContent is null)
                return false;

            contents[filePath] = newContent;
            return true;
        }
    }
}