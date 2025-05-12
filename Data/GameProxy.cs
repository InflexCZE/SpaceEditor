using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Castle.DynamicProxy;
using ReflectionMagic;
using SpaceEditor.Rocks;

namespace SpaceEditor.Data;

public class InputActions
{
    public class InputActionInfo
    {
        public Guid Id;
        public string DisplayName { get; set; }
        public object DefinitionInstanceStub;
        public object DefinitionObjectBuilder;
    }
    public readonly Dictionary<Guid, InputActionInfo> Actions = new();

    public object[] DeserializationContexts = null!;

    public InputActionInfo? TryGetInputActionInfo(Guid id)
    {
        return this.Actions.GetValueOrDefault(id);
    }

    public InputActionInfo? TryGetInputActionInfo(object inputActionDefinitionStub)
    {
        var id = inputActionDefinitionStub.AsDynamic().Guid;
        return TryGetInputActionInfo((Guid) id);
    }
}

public class InputIds
{
    public readonly List<object> Analogs = new();
    public readonly List<object> Pointers = new();
    public readonly List<object> Digitals = new();

    public readonly List<Type> AnalogBuilders = new();
    public readonly List<Type> PointerBuilders = new();
    public readonly List<Type> DigitalBuilders = new();
    public IEnumerable<Type> Builders => this.AnalogBuilders.Concat(this.PointerBuilders).Concat(this.DigitalBuilders);

    public Dictionary<object, string> InputIdToDisplayName = new();

    public string GetDisplayName(object inputId)
    {
        return this.InputIdToDisplayName.GetValueOrDefault(inputId, "Unknown");
    }

    public IEnumerable<Type> GetBuilderTypes(Type targetType)
    {
        foreach (var builder in this.Builders)
        {
            if (targetType.IsAssignableFrom(builder))
                yield return builder;
        }
    }

    public List<Type> GetBuilderTypes(object kind)
    {
        var s = kind.ToString();
        var analogs = s.LastIndexOf("Analog");
        var pointers = s.LastIndexOf("Pointer");
        var digitals = s.LastIndexOf("Digital");

        var best = Math.Max(Math.Max(analogs, pointers), digitals);
        if (best == analogs)
        {
            return this.AnalogBuilders;
        }

        if (best == pointers)
        {
            return this.PointerBuilders;
        }

        if (best == digitals)
        {
            return this.DigitalBuilders;
        }

        throw new UnreachableException();
    }
}

public class GameProxy
{
    public string BaseGamePath { get; }

    public Assembly MainAssembly { get; }
    
    public AsyncLazy<InputIds> InputIds { get; }
    public AsyncLazy<InputActions> InputActions { get; }

    public GameProxy(string baseGamePath)
    {
        this.BaseGamePath = baseGamePath;

        var se2 = ReflectionRocks.GetLib(GameFacts.GetBinsPath(baseGamePath), GameFacts.MainDll);
        this.MainAssembly = se2;

        var st =  FindType("Keen.VRage.Library.Utils.Singleton");
        var mdt = FindType("Keen.VRage.Library.Reflection.MetadataManager");
        var md = st.AsDynamicType().GetInstance(mdt);
        md.PushContext(new[]{se2});

        this.InputIds = new(LoadInputIds);
        this.InputActions = new(LoadInputActions);
    }

    public Type FindType(string typeName)
    {
        var binsPath = GameFacts.GetBinsPath(this.BaseGamePath);
        return ReflectionRocks.TryFindType(binsPath, GameFacts.WellKnownGameBins, typeName) ??
               throw new Exception($"Type {typeName} not found");
    }

    public dynamic DeserializeFile(string filePath, params object[] services)
    {
        using var fs = new FileStream(filePath, FileMode.Open);
        return DeserializeObject(fs, services);
    }

    public dynamic DeserializeObject(Stream content, params object[] services)
    {
        var typedServices = Array.CreateInstance(FindType("CustomSerializationContext"), services.Length);
        Array.Copy(services, typedServices, services.Length);

        var format = Enum.Parse(FindType("SerializerFormat"), "Json");

        using var sc = (IDisposable) Activator.CreateInstance(FindType("SerializationContext"), content, "NoName.txt", typedServices)!;
        return FindType("SerializationHelper").AsDynamicType().DeserializeAbstract<object>(sc, format);
    }

    public string SerializeObject(object instance, params object[] services)
    {
        var typedServices = Array.CreateInstance(FindType("CustomSerializationContext"), services.Length);
        Array.Copy(services, typedServices, services.Length);

        var format = Enum.Parse(FindType("SerializerFormat"), "Json");

        using var data = new MemoryStream();
        using var sc = (IDisposable) Activator.CreateInstance(FindType("SerializationContext"), data, "NoName.txt", typedServices)!;
        FindType("SerializationHelper").GetMethod("SerializeAbstract")!.MakeGenericMethod(typeof(object)).Invoke(null, [sc, instance, format]);

        return Encoding.UTF8.GetString(data.GetBuffer().AsSpan()[..(int)data.Length]);
    }

    private InputActions LoadInputActions()
    {
        var actions = new InputActions();

        var inputActionDefinitionType = FindType("InputActionDefinition");

        var actionsDir = GameFacts.GetActionsPath(this.BaseGamePath);
        Parallel.ForEach(Directory.EnumerateFiles(actionsDir), file =>
        {
            try
            {
                var def = DeserializeFile(file);

                Guid id = def.Guid;
                lock (actionsDir)
                {
                    actions.Actions.Add(id, new InputActions.InputActionInfo
                    {
                        Id = id,
                        DefinitionInstanceStub = DefinitionRocks.AllocateDefinitionStub(inputActionDefinitionType, id),
                        DefinitionObjectBuilder = DynamicHelper.Unwrap(def),
                        DisplayName = Path.GetFileNameWithoutExtension(file),
                    });
                }
            }
            catch
            { }
        });


        var proxy = new ProxyGenerator();
        var mapGuidToDefinitionInstance = proxy.CreateClassProxy
        (
            FindType("DefinitionSerializationContext"),
            new TryLocateDefinitionInterceptor
            {
                Actions = actions
            }
        );

        actions.DeserializationContexts = [mapGuidToDefinitionInstance];
        return actions;
    }

    private InputIds LoadInputIds()
    {
        var inputIds = new InputIds();
        var builderType = FindType("InputControlBuilder");
        var providerType = FindType("IPredefinedInputProvider");

        foreach (var assembly in GameFacts.WellKnownGameBins)
        {
            foreach (var provider in ReflectionRocks.GetLib(this.BaseGamePath, assembly).TryFindDerives(providerType))
            {
                var dynamicProvider = provider.AsDynamicType();
                foreach (var input in provider.GetFields(BindingFlags.Static | BindingFlags.Public))
                {
                    var kind = input.FieldType.Name switch
                    {
                        "PointerInput" => inputIds.Pointers,
                        "DigitalInput" => inputIds.Digitals,
                        "AnalogInput" => inputIds.Analogs,
                        _ => null
                    };

                    if (kind is null)
                        continue;

                    var inputId = DynamicHelper.Unwrap(input.GetValue(null).AsDynamic().Id);
                    kind.Add(inputId);

                    dynamicProvider.TryGetName(inputId, out string displayName);
                    inputIds.InputIdToDisplayName.Add(inputId, displayName);
                }
            }

            foreach (var builder in ReflectionRocks.GetLib(this.BaseGamePath, assembly).TryFindDerives(builderType))
            {
                inputIds.GetBuilderTypes(builder.Name).Add(builder);
            }
        }

        // Take simpler first
        inputIds.AnalogBuilders.Sort((a, b) => a.Name.Length.CompareTo(b.Name.Length));
        inputIds.PointerBuilders.Sort((a, b) => a.Name.Length.CompareTo(b.Name.Length));
        inputIds.DigitalBuilders.Sort((a, b) => a.Name.Length.CompareTo(b.Name.Length));

        return inputIds;
    }

    public class TryLocateDefinitionInterceptor : IInterceptor
    {
        public InputActions Actions { get; set; }

        public void Intercept(IInvocation invocation)
        {
            var methodName = invocation.Method.Name;

            if (methodName == "TryLocateDefinition" && invocation.Arguments.Length == 3)
            {
                var id = (Guid) invocation.Arguments[0]!;
                var type = (Type) invocation.Arguments[1]!;
                invocation.Arguments[2] = this.Actions.TryGetInputActionInfo(id)?.DefinitionInstanceStub ??
                                          DefinitionRocks.AllocateDefinitionStub(type, id);
                
                invocation.ReturnValue = true;
            }
            else
            {
                invocation.Proceed();
            }
        }
    }
}