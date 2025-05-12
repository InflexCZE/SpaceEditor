using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ObservableCollections;
using SpaceEditor.Data;
using SpaceEditor.Rocks;

namespace SpaceEditor.Controls;

/// <summary>
/// Interaction logic for KeyBindsEditor.xaml
/// </summary>
public partial class KeyBindsEditor : UserControl
{
    private const string CurrentPreset = "Current";
    
    public GameProxy Game { get; }
    
    private Settings Settings => Settings.Default;
    public ObservableDictionary<string, string> Presets { get; } = new();
    public INotifyCollectionChanged PresetsView => this.Presets.ToNotifyCollectionChanged();

    public KeyBindsEditor(GameProxy game)
    {
        this.Game = game;

        var mappingsFile = GameFacts.GetMappingFile(this.Game.BaseGamePath);
        var content = File.ReadAllText(mappingsFile);
        this.Presets.Add(CurrentPreset, content);
        
        foreach (var (key, dataString) in this.Settings.NamedPresets)
        {
            this.Presets[key] = dataString;
        }
        
        InitializeComponent();
    }

    private void Save()
    {
        this.Settings.NamedPresets = this.Presets.Where(x => x.Key != CurrentPreset).ToDictionary();
    }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (this.PresetsCombo.IsDropDownOpen == false)
        {
            OnPresetChanged2(default!, default!);
        }
    }

    private void OnPresetChanged2(object? sender, EventArgs e)
    {
        var key = (string?) this.PresetsCombo.SelectedValue;
        if (key is null)
            return;

        var vm = new PresetVM(this.Game);
        vm.Key = key;
        vm.LoadDataString(this.Presets[key]);

        this.SelectedPresetName.DataContext = vm;
        this.InputsEditorControl.DataContext = vm;

        vm.PropertyChanged += OnKeyChanged;

        OnKeyChanged(default!, default!);
        void OnKeyChanged(object? _, PropertyChangedEventArgs __)
        {
            var key = vm.Key;
            var operationsEnabled = string.IsNullOrWhiteSpace(key) == false && key != CurrentPreset;
            this.RemoveButton.IsEnabled = operationsEnabled && this.Presets.ContainsKey(key);
            this.SaveButton.IsEnabled = operationsEnabled;
        }
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        OnCurrentVM(x =>
        {
            var newKey = x.Key;
            this.Presets[newKey] = x.ToDataString();
            this.PresetsCombo.SelectedValue = newKey;
        });
    }

    private void OnRemoveClicked(object sender, RoutedEventArgs e)
    {
        OnCurrentVM(x =>
        {
            this.Presets.Remove(x.Key);
            this.PresetsCombo.SelectedValue = CurrentPreset;
        });
    }

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        OnCurrentVM(x =>
        {
            var newContent = x.ToDataString();
            var backup = this.Presets[CurrentPreset];

            this.Settings.InvokeGameAction(() =>
            {
                this.Presets[$"Backup {DateTime.Now:F}"] = backup;

                var mappingsFile = GameFacts.GetMappingFile(this.Game.BaseGamePath);
                File.WriteAllText(mappingsFile, newContent);
            });

        });
    }

    private void OnCurrentVM(Action<PresetVM> action)
    {
        var vm = (PresetVM?) this.InputsEditorControl.DataContext;
        if (vm is null)
            return;

        action(vm);
        Save();
    }
}