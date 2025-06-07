using ClrDebug;

namespace SpaceEditor.Data.GameLinks;

public static class DebuggerCallbacks
{
    public static Task WhenOnBreakpoint(this CorDebugManagedCallback thiz, Func<BreakpointCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<BreakpointCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnStepComplete(this CorDebugManagedCallback thiz, Func<StepCompleteCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<StepCompleteCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnBreak(this CorDebugManagedCallback thiz, Func<BreakCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<BreakCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnException(this CorDebugManagedCallback thiz, Func<ExceptionCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<ExceptionCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnEvalComplete(this CorDebugManagedCallback thiz, Func<EvalCompleteCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<EvalCompleteCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnEvalException(this CorDebugManagedCallback thiz, Func<EvalExceptionCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<EvalExceptionCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnCreateProcess(this CorDebugManagedCallback thiz, Func<CreateProcessCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<CreateProcessCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnExitProcess(this CorDebugManagedCallback thiz, Func<ExitProcessCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<ExitProcessCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnCreateThread(this CorDebugManagedCallback thiz, Func<CreateThreadCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<CreateThreadCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnExitThread(this CorDebugManagedCallback thiz, Func<ExitThreadCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<ExitThreadCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnLoadModule(this CorDebugManagedCallback thiz, Func<LoadModuleCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<LoadModuleCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnUnloadModule(this CorDebugManagedCallback thiz, Func<UnloadModuleCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<UnloadModuleCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnLoadClass(this CorDebugManagedCallback thiz, Func<LoadClassCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<LoadClassCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnUnloadClass(this CorDebugManagedCallback thiz, Func<UnloadClassCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<UnloadClassCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnDebuggerError(this CorDebugManagedCallback thiz, Func<DebuggerErrorCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<DebuggerErrorCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnLogMessage(this CorDebugManagedCallback thiz, Func<LogMessageCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<LogMessageCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnLogSwitch(this CorDebugManagedCallback thiz, Func<LogSwitchCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<LogSwitchCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnCreateAppDomain(this CorDebugManagedCallback thiz, Func<CreateAppDomainCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<CreateAppDomainCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnExitAppDomain(this CorDebugManagedCallback thiz, Func<ExitAppDomainCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<ExitAppDomainCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnLoadAssembly(this CorDebugManagedCallback thiz, Func<LoadAssemblyCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<LoadAssemblyCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnUnloadAssembly(this CorDebugManagedCallback thiz, Func<UnloadAssemblyCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<UnloadAssemblyCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnControlCTrap(this CorDebugManagedCallback thiz, Func<ControlCTrapCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<ControlCTrapCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnNameChange(this CorDebugManagedCallback thiz, Func<NameChangeCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<NameChangeCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnUpdateModuleSymbols(this CorDebugManagedCallback thiz, Func<UpdateModuleSymbolsCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<UpdateModuleSymbolsCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnEditAndContinueRemap(this CorDebugManagedCallback thiz, Func<EditAndContinueRemapCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<EditAndContinueRemapCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnBreakpointSetError(this CorDebugManagedCallback thiz, Func<BreakpointSetErrorCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<BreakpointSetErrorCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnFunctionRemapOpportunity(this CorDebugManagedCallback thiz, Func<FunctionRemapOpportunityCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<FunctionRemapOpportunityCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnCreateConnection(this CorDebugManagedCallback thiz, Func<CreateConnectionCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<CreateConnectionCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnChangeConnection(this CorDebugManagedCallback thiz, Func<ChangeConnectionCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<ChangeConnectionCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnDestroyConnection(this CorDebugManagedCallback thiz, Func<DestroyConnectionCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<DestroyConnectionCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnException2(this CorDebugManagedCallback thiz, Func<Exception2CorDebugManagedCallbackEventArgs, bool> intercept) => new Events<Exception2CorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnExceptionUnwind(this CorDebugManagedCallback thiz, Func<ExceptionUnwindCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<ExceptionUnwindCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnFunctionRemapComplete(this CorDebugManagedCallback thiz, Func<FunctionRemapCompleteCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<FunctionRemapCompleteCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnMDANotification(this CorDebugManagedCallback thiz, Func<MDANotificationCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<MDANotificationCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnCustomNotification(this CorDebugManagedCallback thiz, Func<CustomNotificationCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<CustomNotificationCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnBeforeGarbageCollection(this CorDebugManagedCallback thiz, Func<BeforeGarbageCollectionCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<BeforeGarbageCollectionCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnAfterGarbageCollection(this CorDebugManagedCallback thiz, Func<AfterGarbageCollectionCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<AfterGarbageCollectionCorDebugManagedCallbackEventArgs>(thiz).When(intercept);
    public static Task WhenOnDataBreakpoint(this CorDebugManagedCallback thiz, Func<DataBreakpointCorDebugManagedCallbackEventArgs, bool> intercept) => new Events<DataBreakpointCorDebugManagedCallbackEventArgs>(thiz).When(intercept);

    public struct Events<T>
        where T : CorDebugManagedCallbackEventArgs
    {
        public CorDebugManagedCallback Instance;

        public Events(CorDebugManagedCallback instance)
        {
            this.Instance = instance;
        }

        public Task When(Func<T, bool> intercept)
        {
            var instance = this.Instance;
            var tcs = new TaskCompletionSource();

            bool Impl(T e)
            {
                if (intercept(e))
                {
                    tcs.SetResult();
                    return true;
                }

                return false;
            }

            if (typeof(T) == typeof(BreakpointCorDebugManagedCallbackEventArgs))
            {
                EventHandler<BreakpointCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnBreakpoint -= handler; };
                instance.OnBreakpoint += handler;
            }
            else if (typeof(T) == typeof(StepCompleteCorDebugManagedCallbackEventArgs))
            {
                EventHandler<StepCompleteCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnStepComplete -= handler; };
                instance.OnStepComplete += handler;
            }
            else if (typeof(T) == typeof(BreakCorDebugManagedCallbackEventArgs))
            {
                EventHandler<BreakCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnBreak -= handler; };
                instance.OnBreak += handler;
            }
            else if (typeof(T) == typeof(ExceptionCorDebugManagedCallbackEventArgs))
            {
                EventHandler<ExceptionCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnException -= handler; };
                instance.OnException += handler;
            }
            else if (typeof(T) == typeof(EvalCompleteCorDebugManagedCallbackEventArgs))
            {
                EventHandler<EvalCompleteCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnEvalComplete -= handler; };
                instance.OnEvalComplete += handler;
            }
            else if (typeof(T) == typeof(EvalExceptionCorDebugManagedCallbackEventArgs))
            {
                EventHandler<EvalExceptionCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnEvalException -= handler; };
                instance.OnEvalException += handler;
            }
            else if (typeof(T) == typeof(CreateProcessCorDebugManagedCallbackEventArgs))
            {
                EventHandler<CreateProcessCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnCreateProcess -= handler; };
                instance.OnCreateProcess += handler;
            }
            else if (typeof(T) == typeof(ExitProcessCorDebugManagedCallbackEventArgs))
            {
                EventHandler<ExitProcessCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnExitProcess -= handler; };
                instance.OnExitProcess += handler;
            }
            else if (typeof(T) == typeof(CreateThreadCorDebugManagedCallbackEventArgs))
            {
                EventHandler<CreateThreadCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnCreateThread -= handler; };
                instance.OnCreateThread += handler;
            }
            else if (typeof(T) == typeof(ExitThreadCorDebugManagedCallbackEventArgs))
            {
                EventHandler<ExitThreadCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnExitThread -= handler; };
                instance.OnExitThread += handler;
            }
            else if (typeof(T) == typeof(LoadModuleCorDebugManagedCallbackEventArgs))
            {
                EventHandler<LoadModuleCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnLoadModule -= handler; };
                instance.OnLoadModule += handler;
            }
            else if (typeof(T) == typeof(UnloadModuleCorDebugManagedCallbackEventArgs))
            {
                EventHandler<UnloadModuleCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnUnloadModule -= handler; };
                instance.OnUnloadModule += handler;
            }
            else if (typeof(T) == typeof(LoadClassCorDebugManagedCallbackEventArgs))
            {
                EventHandler<LoadClassCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnLoadClass -= handler; };
                instance.OnLoadClass += handler;
            }
            else if (typeof(T) == typeof(UnloadClassCorDebugManagedCallbackEventArgs))
            {
                EventHandler<UnloadClassCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnUnloadClass -= handler; };
                instance.OnUnloadClass += handler;
            }
            else if (typeof(T) == typeof(DebuggerErrorCorDebugManagedCallbackEventArgs))
            {
                EventHandler<DebuggerErrorCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnDebuggerError -= handler; };
                instance.OnDebuggerError += handler;
            }
            else if (typeof(T) == typeof(LogMessageCorDebugManagedCallbackEventArgs))
            {
                EventHandler<LogMessageCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnLogMessage -= handler; };
                instance.OnLogMessage += handler;
            }
            else if (typeof(T) == typeof(LogSwitchCorDebugManagedCallbackEventArgs))
            {
                EventHandler<LogSwitchCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnLogSwitch -= handler; };
                instance.OnLogSwitch += handler;
            }
            else if (typeof(T) == typeof(CreateAppDomainCorDebugManagedCallbackEventArgs))
            {
                EventHandler<CreateAppDomainCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnCreateAppDomain -= handler; };
                instance.OnCreateAppDomain += handler;
            }
            else if (typeof(T) == typeof(ExitAppDomainCorDebugManagedCallbackEventArgs))
            {
                EventHandler<ExitAppDomainCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnExitAppDomain -= handler; };
                instance.OnExitAppDomain += handler;
            }
            else if (typeof(T) == typeof(LoadAssemblyCorDebugManagedCallbackEventArgs))
            {
                EventHandler<LoadAssemblyCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnLoadAssembly -= handler; };
                instance.OnLoadAssembly += handler;
            }
            else if (typeof(T) == typeof(UnloadAssemblyCorDebugManagedCallbackEventArgs))
            {
                EventHandler<UnloadAssemblyCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnUnloadAssembly -= handler; };
                instance.OnUnloadAssembly += handler;
            }
            else if (typeof(T) == typeof(ControlCTrapCorDebugManagedCallbackEventArgs))
            {
                EventHandler<ControlCTrapCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnControlCTrap -= handler; };
                instance.OnControlCTrap += handler;
            }
            else if (typeof(T) == typeof(NameChangeCorDebugManagedCallbackEventArgs))
            {
                EventHandler<NameChangeCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnNameChange -= handler; };
                instance.OnNameChange += handler;
            }
            else if (typeof(T) == typeof(UpdateModuleSymbolsCorDebugManagedCallbackEventArgs))
            {
                EventHandler<UpdateModuleSymbolsCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnUpdateModuleSymbols -= handler; };
                instance.OnUpdateModuleSymbols += handler;
            }
            else if (typeof(T) == typeof(EditAndContinueRemapCorDebugManagedCallbackEventArgs))
            {
                EventHandler<EditAndContinueRemapCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnEditAndContinueRemap -= handler; };
                instance.OnEditAndContinueRemap += handler;
            }
            else if (typeof(T) == typeof(BreakpointSetErrorCorDebugManagedCallbackEventArgs))
            {
                EventHandler<BreakpointSetErrorCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnBreakpointSetError -= handler; };
                instance.OnBreakpointSetError += handler;
            }
            else if (typeof(T) == typeof(FunctionRemapOpportunityCorDebugManagedCallbackEventArgs))
            {
                EventHandler<FunctionRemapOpportunityCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnFunctionRemapOpportunity -= handler; };
                instance.OnFunctionRemapOpportunity += handler;
            }
            else if (typeof(T) == typeof(CreateConnectionCorDebugManagedCallbackEventArgs))
            {
                EventHandler<CreateConnectionCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnCreateConnection -= handler; };
                instance.OnCreateConnection += handler;
            }
            else if (typeof(T) == typeof(ChangeConnectionCorDebugManagedCallbackEventArgs))
            {
                EventHandler<ChangeConnectionCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnChangeConnection -= handler; };
                instance.OnChangeConnection += handler;
            }
            else if (typeof(T) == typeof(DestroyConnectionCorDebugManagedCallbackEventArgs))
            {
                EventHandler<DestroyConnectionCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnDestroyConnection -= handler; };
                instance.OnDestroyConnection += handler;
            }
            else if (typeof(T) == typeof(Exception2CorDebugManagedCallbackEventArgs))
            {
                EventHandler<Exception2CorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnException2 -= handler; };
                instance.OnException2 += handler;
            }
            else if (typeof(T) == typeof(ExceptionUnwindCorDebugManagedCallbackEventArgs))
            {
                EventHandler<ExceptionUnwindCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnExceptionUnwind -= handler; };
                instance.OnExceptionUnwind += handler;
            }
            else if (typeof(T) == typeof(FunctionRemapCompleteCorDebugManagedCallbackEventArgs))
            {
                EventHandler<FunctionRemapCompleteCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnFunctionRemapComplete -= handler; };
                instance.OnFunctionRemapComplete += handler;
            }
            else if (typeof(T) == typeof(MDANotificationCorDebugManagedCallbackEventArgs))
            {
                EventHandler<MDANotificationCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnMDANotification -= handler; };
                instance.OnMDANotification += handler;
            }
            else if (typeof(T) == typeof(CustomNotificationCorDebugManagedCallbackEventArgs))
            {
                EventHandler<CustomNotificationCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnCustomNotification -= handler; };
                instance.OnCustomNotification += handler;
            }
            else if (typeof(T) == typeof(BeforeGarbageCollectionCorDebugManagedCallbackEventArgs))
            {
                EventHandler<BeforeGarbageCollectionCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnBeforeGarbageCollection -= handler; };
                instance.OnBeforeGarbageCollection += handler;
            }
            else if (typeof(T) == typeof(AfterGarbageCollectionCorDebugManagedCallbackEventArgs))
            {
                EventHandler<AfterGarbageCollectionCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnAfterGarbageCollection -= handler; };
                instance.OnAfterGarbageCollection += handler;
            }
            else if (typeof(T) == typeof(DataBreakpointCorDebugManagedCallbackEventArgs))
            {
                EventHandler<DataBreakpointCorDebugManagedCallbackEventArgs> handler = null!;
                handler = (_, e) => { if (Impl((T) (object) e)) instance.OnDataBreakpoint -= handler; };
                instance.OnDataBreakpoint += handler;
            }

            return tcs.Task;
        }
    }
}