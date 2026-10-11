using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Win32;
using SpaceEditor.Controls;
using SpaceEditor.Data;
using SpaceEditor.Data.GameLinks;
using SpaceEditor.Services;

namespace SpaceEditor;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    private Settings Settings => Settings.Default;
    private GameLink? GameLink;
    private Func<string>? InfoMessage;

    public string GamePath
    {
        get => this.Settings.GamePath;
        set
        {
            this.Settings.GamePath = value;
            OnPropertyChanged();
            Reload(default!, default!);
        }
    }

    public MainWindow()
    {
        this.DataContext = this;
        this.Loaded += Reload;
        InitializeComponent();
        PropertyChangedEventManager.AddHandler(LocalizationManager.Current, OnLanguageChanged, nameof(LocalizationManager.LanguageCode));

        this.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var latest = await VersionChecker.GetLatestVersionInfo();
                if (latest.Published.Date != BuildInfo.BuildTimeUtc.Date)
                {
                    this.UpdateHints.Visibility = Visibility.Visible;
                }
            }
            catch
            { }
        });
    }

    private async void Reload(object sender, RoutedEventArgs e)
    {
        var tabs = this.MainTabs.Items;
        while (tabs.Count > 1)
        {
            tabs.RemoveAt(tabs.Count - 1);
        }

        if (string.IsNullOrWhiteSpace(this.GamePath))
        {
            SetInfo(() => LocalizationManager.Text("ChooseGameFirst"));
            return;
        }

        SetInfo(() => LocalizationManager.Text("LoadingStatus"));
        try
        {
            var game = await Task.Run(async () =>
            {
                var game = new GameProxy(this.GamePath);
                _ = await game.InputActions;
                _ = await game.InputIds;
                return game;
            });

            var propertyGridFactoryKey = "CompositePropertyGridControlFactory";
            this.Resources.Remove(propertyGridFactoryKey);
            this.Resources.Add(propertyGridFactoryKey, new CompositePropertyGridControlFactory
            {
                Factories =
                {
                    new ButtonPropertyGridControlFactory(),
                    new InputIdControlsFactory
                    {
                        InputIds = await game.InputIds
                    }
                }
            });

            tabs.Add(new TabItem
            {
                Header = LocalizationManager.Text("PcuTab"),
                Tag = "PcuTab",
                Content = new PCUUnlocker(game)
            });

            if (this.GameLink is not null)
            {
                await this.GameLink.DisposeAsync();
            }

            this.GameLink = new GameLink(game);

            tabs.Add(new TabItem
            {
                Header = LocalizationManager.Text("CharacterTab"),
                Tag = "CharacterTab",
                Content = new CharacterEditor(game, this.GameLink)
            });

            tabs.Add(new TabItem
            {
                Header = LocalizationManager.Text("BlueprintTab"),
                Tag = "BlueprintTab",
                Content = new BlueprintGenerator()
            });

            SetInfo(() => GameInfo(game));
        }
        catch (Exception ex)
        {
            SetInfo(() => LocalizationManager.Text("LoadingError") + Environment.NewLine + ex);
        }
    }

    private static string GameInfo(GameProxy game)
    {
        var sb = new StringBuilder();
        sb.AppendLine(LocalizationManager.Text("LoadingComplete"));
        sb.AppendLine();

        sb.AppendLine(LocalizationManager.Text("MainAssembly"));
        var gameExe = game.MainAssembly;
        sb.AppendLine($"{gameExe.GetName().Name}");
        sb.AppendLine($"{gameExe.GetName().Version}");
        sb.AppendLine($"{gameExe.Location}");

        sb.AppendLine();
        sb.AppendLine(LocalizationManager.Text("FeatureHint"));

        return sb.ToString();
    }

    private void SetInfo(Func<string> message)
    {
        this.InfoMessage = message;
        this.InfoText.Text = message();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var tab in this.MainTabs.Items.OfType<TabItem>())
            if (tab.Tag is string key)
                tab.Header = LocalizationManager.Text(key);
        if (this.InfoMessage is not null)
            this.InfoText.Text = this.InfoMessage();
    }

    private void OnLocateGame(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = LocalizationManager.Text("ChooseGameFolder"),
            Multiselect = false,
        };

        var found = dialog.ShowDialog(this);
        if (found != true)
            return;

        this.GamePath = dialog.FolderName;
    }

    private void CheckOutUpdate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
        {
            UseShellExecute = true
        });
        e.Handled = true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
