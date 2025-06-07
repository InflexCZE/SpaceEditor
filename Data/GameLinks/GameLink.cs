using ClrDebug;
using SpaceEditor.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace SpaceEditor.Data.GameLinks;

public partial class GameLink : IAsyncDisposable
{
    public GameProxy Game { get; }
    
    private readonly SemaphoreSlim SyncLock = new(1, 1);
    private readonly DbgShim DbgShim;

    public GameLink(GameProxy game)
    {
        this.Game = game;

        var lib = NativeLibrary.Load(DbgShimResolver.Resolve());
        this.DbgShim = new(lib);
    }

    public async ValueTask DisposeAsync()
    {
        // Ensure all operations were finished
        // TODO: There can actually be queue and Semaphore is not guaranteed to be fair
        {
            using var __ = await Sync();
        }

        this.SyncLock.Dispose();
    }

    public Task TestConnection()
    {
        return InvokeOperation(_ => Task.CompletedTask);
    }

    public async Task InvokeOperation(Func<Operation, Task> invocation)
    {
        using var __ = await Sync();

        var process = Process.GetProcessesByName(GameFacts.MainDll).FirstOrDefault();
        if (process is null)
        {
            throw new Exception($"{GameFacts.MainDll} is not running. Start the Game first");
        }

        var processId = process.Id;
        var clrs = this.DbgShim.EnumerateCLRs(processId).Items;
        if (clrs.Length == 0)
        {
            throw new Exception($"Could not find CLR");
        }
        
        //Version String is a comma delimited value containing dbiVersion, pidDebuggee, hmodTargetCLR
        var versionStr = this.DbgShim.CreateVersionStringFromModule(processId, clrs[0].Path);

        /* Cordb::CheckCompatibility seems to be the only place where our debugger version is actually used,
         * and it says that if the version is 4, its major version 4. Version 4.5 is treated as an "unrecognized future version"
         * and is assigned major version 5, which is wrong. Cordb::CheckCompatibility then calls CordbProcess::IsCompatibleWith
         * which doesn't actually seem to do anything either, despite what all the docs in it would imply. */
        var cordebug = this.DbgShim.CreateDebuggingInterfaceFromVersionEx(CorDebugInterfaceVersion.CorDebugVersion_4_0, versionStr);

        //Initialize ICorDebug, setup our managed callback and attach to the existing process. We attach while the CLR is blocked waiting for the "continue" event to be called
        cordebug.Initialize();

        var callbacks = new CorDebugManagedCallback();
        callbacks.OnAnyEvent += (s, e) =>
        {
            if (e.Continue)
            {
                e.Controller.Continue(false);
            }
        };

        cordebug.SetManagedHandler(callbacks);
        var debugProcess = cordebug.DebugActiveProcess(processId, win32Attach: false);
        try
        {
            var appDomain = debugProcess.AppDomains.Single();
            var op = new Operation
            {
                Debug = cordebug,
                Process = debugProcess,
                Domain = appDomain,
                Assemblies = appDomain.Assemblies,
                Callbacks = callbacks,
            };

            await Task.Run(() =>
            {
                foreach (var assembly in op.Assemblies)
                {
                    while (assembly.Modules.Length == 0)
                    {
                        // Waiting till CLR announces all modules
                    }
                }
            });

            debugProcess.Stop(default);
            await invocation(op);
        }
        finally
        {
            if (debugProcess.IsRunning)
            {
                debugProcess.Stop(default);
            }

            debugProcess.Detach();
        }
    }

    public Task InvokeOnMainThread(Func<Operation, CorDebugThread, Task> invocation)
    {
        return InvokeOperation(async op =>
        {
            CorDebugThread thread;

            // TODO: VRageCore.Update can get devirtualized and inlined, then it won't hit
            //       Session.Update is large enough to not get inlined, but it's not guaranteed which Scene it will hit on
            // var engineUpdateMethod = op.FindFunction("Keen.VRage.Core.VRageCore", "Update");
            // thread = await op.CatchThreadInFunction(engineUpdateMethod);
            
            var sessionUpdateMethod = op.FindFunction("Keen.VRage.Core.Game.Systems.Session", "Update");
            thread = await op.CatchThreadInFunction(sessionUpdateMethod);

            await invocation(op, thread);
        });
    }

    public Task InvokeOnSession(Func<Operation, CorDebugThread, CorDebugHandleValue, Task> invocation)
    {
        return InvokeOnMainThread(async (op, thread) =>
        {
            var sessionUpdateMethod = op.FindFunction("Keen.VRage.Core.Game.Systems.Session", "Update");
            await op.CatchThreadInFunction(sessionUpdateMethod, thread);

            var session = op.HoldObject(((CorDebugILFrame)thread.ActiveFrame).Arguments[0]);
            try
            {
                await invocation(op, thread, session);
            }
            finally
            {
                session.Dispose();
            }
        });
    }

    public Task InvokeOnComponents(string typeName, Func<Operation, CorDebugThread, List<CorDebugHandleValue>, Task> invocation)
    {
        return InvokeOnSession(async (op, thread, session) =>
        {
            var targetTypeInfo = op.FindType(typeName);
            var targetClass = targetTypeInfo.Module.GetClassFromToken(targetTypeInfo.Token);
            var targetType = targetClass.GetParameterizedType(CorElementType.Class, 0, []);

            // HashSet<Entity>
            var entities = op.ReadField(session, "_activeEntities");
            var entityType = entities.ExactType.FirstTypeParameter;
            
            var toArrayMethod = op.FindFunction("System.Linq.Enumerable", "ToArray");
            
            var eval = thread.CreateEval();
            eval.CallParameterizedFunction(toArrayMethod.Raw, 1, [entityType.Raw], 1, [entities.Raw]);
            var entitiesAsArrayHandle = await op.ExecutePreparedEval(eval, expectResult: true);
            try
            {
                var entitiesAsArray = (CorDebugArrayValue) entitiesAsArrayHandle.Dereference();
                var entitiesCount = entitiesAsArray.Count;
                
                List<CorDebugHandleValue> componentHandles = new();
                try
                {
                    for (int i = 0; i < entitiesCount; i++)
                    {
                        var entity = entitiesAsArray.GetElementAtPosition(i).As<CorDebugReferenceValue>().Dereference();
                        var entityComponentsImmutableArray = op.ReadField(entity, "Components");
                        var entityComponents = (CorDebugArrayValue) op.ReadField(entityComponentsImmutableArray, "array").As<CorDebugReferenceValue>().Dereference();

                        CorDebugValue? componentHit = null;
                        var cc = entityComponents.Count;
                        for (int q = 0; q < cc; q++)
                        {
                            var c3 = entityComponents.GetElementAtPosition(q);
                            if (c3.ExactType.Equals(targetType))
                            {
                                componentHit = c3;
                                break;
                            }
                        }

                        if (componentHit is null)
                            continue;

                        componentHandles.Add(op.HoldObject(componentHit));

                        //TODO: Probing arrays one by one element is expensive af
                        //      For now, it's sufficient to take first match
                        //      Later will prolly have to link Roslyn and sideload probing Assembly to be in-process
                        break;
                    }

                    await invocation(op, thread, componentHandles);
                }
                finally
                {
                    foreach (var skin in componentHandles)
                    {
                        skin.Dispose();
                    }
                }
            }
            finally
            {
                entitiesAsArrayHandle.Dispose();
            }
        });
    }

    public Task PaintCharacters(uint color)
    {
        var skinComponentTypeName = "Keen.Game2.Simulation.WorldObjects.Characters.SkinComponent";
        return InvokeOnComponents
        (
            skinComponentTypeName,
            async (op, thread, characters) =>
            {
                var suitColorSetter = op.FindFunction(skinComponentTypeName, "set_SuitColor");
                var fromARGBMethod = op.FindFunction("Keen.VRage.Library.Mathematics.ColorSRGB", "FromARGB");

                var eval = thread.CreateEval();
                var colorValue = op.CreatePrimitiveValue(eval, color);
                eval.CallFunction(fromARGBMethod.Raw, 1, [colorValue.Raw]);
                
                var colorHandle = await op.ExecutePreparedEval(eval, expectResult: true);
                try
                {
                    foreach(var characterRef in characters)
                    {
                        eval.CallFunction(suitColorSetter.Raw, 2, [characterRef.Raw, colorHandle.Raw]);
                        await op.ExecutePreparedEval(eval);
                    }
                }
                finally
                {
                    colorHandle.Dispose();
                }
            }
        );
    }

    private async ValueTask<SyncToken> Sync()
    {
        await this.SyncLock.WaitAsync();
        return new(this.SyncLock);
    }

    private struct SyncToken : IDisposable
    {
        public SemaphoreSlim? Sync;

        public SyncToken(SemaphoreSlim? sync)
        {
            this.Sync = sync;
        }

        public void Dispose()
        {
            this.Sync?.Release();
        }
    }
}