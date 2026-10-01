using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReflectionMagic;
using SpaceEditor.Rocks;
using SpaceEditor.Services;
using System.ComponentModel;
using System.Windows;

namespace SpaceEditor.Data;

public class PresetVM : VM
{
    public GameProxy Game { get; }
    public InputActions Actions => this.Game.InputActions.Value.Result;
    public InputIds InputIds => this.Game.InputIds.Value.Result;

    private string KeyImpl;
    public string Key
    {
        get => this.KeyImpl;
        set
        {
            if (SetField(ref this.KeyImpl, value))
                OnPropertyChanged(nameof(DisplayKey));
        }
    }

    public string DisplayKey
    {
        get => this.Key == "Current" ? LocalizationManager.Text("CurrentPreset") : this.Key;
        set
        {
            if (this.Key == "Current" && value == LocalizationManager.Text("CurrentPreset"))
                return;
            this.Key = value;
        }
    }

    public string DataString { get; private set; }
    public object RootObject { get; private set; }
    public IDictionary Bindings { get; private set; }
    
    public PresetVM(GameProxy game)
    {
        this.Game = game;
        PropertyChangedEventManager.AddHandler(LocalizationManager.Current, OnLanguageChanged, nameof(LocalizationManager.LanguageCode));
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(DisplayKey));

    public IList? TryGetBindings(Guid id)
    {
        var def = this.Actions.TryGetInputActionInfo(id)?.DefinitionInstanceStub;
        if (def is null)
            return null;
    
        if (this.Bindings.Contains(def) == false)
            return null;
    
        return (IList) this.Bindings[def]!;
    }

    public void LoadDataString(string content)
    {
        using var contentStream = content.AsStream();
        var mappings = this.Game.DeserializeObject(contentStream, this.Game.InputActions.Value.Result.DeserializationContexts);

        this.DataString = content;
        this.RootObject = DynamicHelper.Unwrap(mappings);
        this.Bindings = (IDictionary) DynamicHelper.Unwrap(mappings.ControlsPerAction);
    }
    
    public string ToDataString()
    {
        return this.Game.SerializeObject(this.RootObject);
    }
}
