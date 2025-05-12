using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReflectionMagic;
using SpaceEditor.Rocks;

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
        set => SetField(ref this.KeyImpl, value);
    }

    public string DataString { get; private set; }
    public object RootObject { get; private set; }
    public IDictionary Bindings { get; private set; }
    
    public PresetVM(GameProxy game)
    {
        this.Game = game;
    }

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