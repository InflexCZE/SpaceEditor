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

    public PCUUnlocker(GameProxy game)
    {
        this.Game = game;
        InitializeComponent();
    }

    private void UnlockPCU(object sender, RoutedEventArgs e)
    {
        var pcuFilePath = GameFacts.TryFindTargetPath(this.Game.ContentPath, [], "PCUSessionComponentConfiguration.def");
        if (pcuFilePath is null)
        {
            MessageBox.Show("Could not find PCU file. Game Content mush have changed.\nUpdate to latest version of the tool");
            return;
        }

        var content = File.ReadAllText(pcuFilePath);
        var newContent = Regex.Replace(content, "(\"MaxGlobalPCU\"\\s*:\\s*)(\\d+)", $"${{1}}{int.MaxValue / 2}");

        Settings.Default.InvokeGameAction(() =>
        {
            File.WriteAllText(pcuFilePath, newContent);
        });
    }
}