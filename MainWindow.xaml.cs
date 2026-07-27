using Microsoft.Win32;
using SpaceEditor.Controls;
using SpaceEditor.Data;
using SpaceEditor.Data.GameLinks;
using SpaceEditor.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SpaceEditor;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private Settings Settings => Settings.Default;
    private GameLink? GameLink;

    // Token to ensure overlapping reloads are safely aborted
    private CancellationTokenSource? _reloadCts;

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

        this.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var latest = await VersionChecker.GetLatestVersionInfo();
                if (latest.Published.Date > BuildInfo.BuildTimeUtc.Date)
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
        // Abort any currently running background initialization
        _reloadCts?.Cancel();
        _reloadCts = new CancellationTokenSource();
        var token = _reloadCts.Token;

        var tabs = this.MainTabs.Items;
        while (tabs.Count > 1)
        {
            tabs.RemoveAt(tabs.Count - 1);
        }

        if (string.IsNullOrWhiteSpace(this.GamePath))
        {
            this.InfoText.Text = "First, please show me where is the Game installed";
            return;
        }

        this.InfoText.Text = "Loading Engine and Game Data in background... (This may take a moment)";

        try
        {
            // Offload directory searching and math to the background thread
            var game = await Task.Run(async () =>
            {
                // 1. Initialize GameProxy (Searches directory, loads assemblies)
                var proxy = new GameProxy(this.GamePath);
                _ = await proxy.InputActions;
                _ = await proxy.InputIds;

                token.ThrowIfCancellationRequested();

                // 2. Pre-warm the Blueprint Generator's heavy static meshes in the background.
                _ = Algorithms.GridShaper.ShapeDB.LargeShapes;
                _ = Algorithms.GridShaper.ShapeDB.MidShapes;

                // 3. Pre-compile the ILGPU Kernel in the background
                _ = Algorithms.GridShaper.GpuSetup.Accelerator;

                return proxy;
            }, token);

            // Double check cancellation before touching the main thread engine or UI
            if (token.IsCancellationRequested) return;

            // 3. Initialize the VRage Engine connection ON THE MAIN THREAD.
            // Game engines crash (Exit Code 1) if initialized on a background thread.
            var newGameLink = new GameLink(game);
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
                Header = "Key Binds",
                Content = new KeyBindsEditor(game)
            });

            tabs.Add(new TabItem
            {
                Header = "PCU Unlocker",
                Content = new PCUUnlocker(game)
            });

            if (this.GameLink is not null)
            {
                await this.GameLink.DisposeAsync();
            }

            this.GameLink = newGameLink;

            tabs.Add(new TabItem
            {
                Header = "Character",
                Content = new CharacterEditor(game, this.GameLink)
            });

            tabs.Add(new TabItem
            {
                Header = "Blueprint Generator",
                Content = new BlueprintGenerator()
            });

            var sb = new StringBuilder();
            sb.AppendLine("Loading finished");
            sb.AppendLine();

            sb.AppendLine("Main Assembly:");
            var gameExe = game.MainAssembly;
            sb.AppendLine($"{gameExe.GetName().Name}");
            sb.AppendLine($"{gameExe.GetName().Version}");
            sb.AppendLine($"{gameExe.Location}");

            sb.AppendLine();
            sb.AppendLine("Use Tabs above to access individual features");

            this.InfoText.Text = sb.ToString();
        }
        catch (OperationCanceledException)
        {
            // Silently handle task cancellation if the user changes the path rapidly
        }
        catch (Exception ex)
        {
            this.InfoText.Text = "Exception happened during initial loading:" + Environment.NewLine + ex;
        }
    }

    private void OnLocateGame(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
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