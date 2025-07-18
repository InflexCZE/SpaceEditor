using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using ClrDebug;
using SpaceEditor.Rocks;

namespace SpaceEditor.Data.GameLinks;

public partial class GameLink
{
    public class Operation
    {
        public CorDebug Debug { get; init; }
        public CorDebugProcess Process { get; init; }
        public CorDebugAppDomain Domain { get; init; }
        public CorDebugAssembly[] Assemblies { get; init; }
        
        public CorDebugManagedCallback Callbacks { get; init; }

        public (CorDebugModule Module, mdTypeDef Token) FindType(string typeName)
        {
            var parent = mdTypeDef.Nil;
            CorDebugModule? module = null;
            MetaDataImport? moduleMd = null;

            foreach (var typePart in typeName.Split('+'))
            {
                if (moduleMd is null)
                {
                    foreach (var corDebugAssembly in this.Assemblies)
                    {
                        foreach (var candidateModule in corDebugAssembly.Modules)
                        {
                            var md = candidateModule.GetMetaDataInterface().MetaDataImport;
                            if (md.TryFindTypeDefByName(typePart, parent, out _).IsOk())
                            {
                                moduleMd = md;
                                module = candidateModule;
                                goto Found;
                            }
                        }
                    }

                    if (moduleMd is null)
                    {
                        throw new Exception($"No Module found for type {typePart}");
                    }
                }

                Found:
                if (moduleMd!.TryFindTypeDefByName(typePart, parent, out parent).IsFail())
                {
                    throw new Exception($"Sub type {typePart} not found in {module!.Name}");
                }
            }

            return (module!, parent);
        }

        public CorDebugFunction FindFunction(string typeName, string methodName)
        {
            var type = FindType(typeName);
            return FindFunction(type.Module, type.Token, methodName);
        }

        public CorDebugFunction FindFunction(CorDebugType type, string methodName)
        {
            var clas = type.Class;
            return FindFunction(clas.Module, clas.Token, methodName);
        }

        public CorDebugFunction FindFunction(CorDebugModule module, mdTypeDef type, string methodName)
        {
            var md = module.GetMetaDataInterface().MetaDataImport;
            var updateMethodToken = md.FindMethod(type, methodName, default, 0);
            return module.GetFunctionFromToken(updateMethodToken);
        }

        public async Task<CorDebugThread> CatchThreadInFunction(CorDebugFunction method, CorDebugThread? thread = null)
        {
            if (this.Process.IsRunning)
            {
                throw new Exception("Should not run");
            }

            var bp = method.CreateBreakpoint();
            try
            {
                //TODO: When fail, unregister
                var task = this.Callbacks.WhenOnBreakpoint(e =>
                {
                    if (e.Breakpoint.Equals(bp) == false)
                        return false;

                    if (thread is not null && e.Thread.Equals(thread) == false)
                        return false;

                    e.Continue = false;
                    thread = e.Thread;
                    return true;
                });

                this.Process.Continue(false);

                try
                {
                    await task.WaitAsync(TimeSpan.FromSeconds(1));
                }
                catch
                {
                    this.Process.Stop(default);
                    throw;
                }
            }
            finally
            {
                bp.Activate(false);
            }

            return thread!;
        }

        public async Task ExecutePreparedStep(CorDebugStepper stepper)
        {
            var step = this.Callbacks.WhenOnStepComplete(x =>
            {
                if (stepper.Equals(x.Stepper) == false)
                    return false;

                x.Continue = false;
                return true;
            });

            this.Process.Continue(default);

            try
            {
                await step.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch
            {
                this.Process.Stop(default);
                throw;
            }
        }

        public async Task<(CorDebugHandleValue, T)> ExecutePreparedEval<T>(CorDebugEval eval)
            where T : CorDebugValue
        {
            var result = await ExecutePreparedEval(eval, expectResult: true);

            var handle = result as CorDebugHandleValue;
            
            CorDebugValue value = result;
            if (value is CorDebugReferenceValue ptr)
            {
                value = ptr.Dereference();
            }

            return (handle, value.As<T>());
        }

        public async Task<CorDebugHandleValue> ExecutePreparedEval(CorDebugEval eval, bool expectResult = false)
        {
            var success = await Invoke();

            if (success == false)
            {
                string? errorInfo = null;
                try
                {
                    var exception = eval.Result;
                    var toString = FindFunction("System.Object", "ToString");

                    eval.CallFunction(toString.Raw, 1, [exception.Raw]);
                    
                    var extractedInfo = await Invoke();
                    if (extractedInfo)
                    {
                        var info = eval.Result.As<CorDebugHandleValue>();
                        try
                        {
                            errorInfo = ReadString(info);
                        }
                        finally
                        {
                            info.Dispose();
                        }
                    }
                }
                catch
                { }

                if (errorInfo is null)
                {
                    throw new Exception("Eval failed. No result");
                }
                else
                {
                    throw new Exception($"Eval failed.{Environment.NewLine}{errorInfo}");
                }
            }

            if (expectResult == false)
            {
                return null!;
            }

            return (CorDebugHandleValue) eval.Result;

            async Task<bool> Invoke()
            {
                var success = this.Callbacks.WhenOnEvalComplete(e =>
                {
                    if (e.Eval.Equals(eval) == false)
                        return false;

                    e.Continue = false;
                    return true;
                });

                //TODO: This sub is leaking most of the time
                var fail = this.Callbacks.WhenOnEvalException(e =>
                {
                    if (e.Eval.Equals(eval) == false)
                        return false;

                    e.Continue = false;
                    return true;
                });

                this.Process.Continue(false);

                var result = await Task.WhenAny(success, fail);
                return result == success;
            }
        }

        public Task<CorDebugHandleValue> CreateStringValue(CorDebugEval eval, string value)
        {
            eval.NewString(value);
            return ExecutePreparedEval(eval, expectResult: true);
        }

        public string ReadString(CorDebugValue value, string targetField)
        {
            return ReadString(ReadField(value, targetField));
        }

        public string ReadString(CorDebugValue value)
        {
            if (value is CorDebugReferenceValue ptr)
            {
                value = ptr.Dereference();
            }

            var str = value.As<CorDebugStringValue>();
            return str.GetString(str.Length + 1);
        }

        public unsafe T ReadPrimitiveValue<T>(CorDebugValue value)
            where T : unmanaged
        {
            var expectedType = PrimitiveRuntimeTypeToCorType(typeof(T));

            var genericValue = value.As<CorDebugGenericValue>();
            var actualType = genericValue.Type;

            if (actualType != expectedType)
            {
                throw new Exception($"Type mismatch {actualType} {expectedType} {typeof(T)}");
            }

            T store = default;
            genericValue.GetValue((IntPtr) (void*) &store);
            return store;
        }

        public unsafe CorDebugValue CreatePrimitiveValue<T>(CorDebugEval eval, T value)
            where T : unmanaged
        {
            var type = PrimitiveRuntimeTypeToCorType(typeof(T));
            var valueHandle = (CorDebugGenericValue) eval.CreateValue(type, null);
            valueHandle.SetValue((IntPtr) (void*) &value);
            return valueHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CorElementType PrimitiveRuntimeTypeToCorType(Type runtimeType)
        {
            if (runtimeType == typeof(bool))
                return CorElementType.Boolean;

            if (runtimeType == typeof(char))
                return CorElementType.Char;

            if (runtimeType == typeof(sbyte))
                return CorElementType.I1;

            if (runtimeType == typeof(byte))
                return CorElementType.U1;

            if (runtimeType == typeof(short))
                return CorElementType.I2;

            if (runtimeType == typeof(ushort))
                return CorElementType.U2;

            if (runtimeType == typeof(int))
                return CorElementType.I4;

            if (runtimeType == typeof(uint))
                return CorElementType.U4;

            if (runtimeType == typeof(long))
                return CorElementType.I8;

            if (runtimeType == typeof(ulong))
                return CorElementType.U8;

            if (runtimeType == typeof(float))
                return CorElementType.R4;

            if (runtimeType == typeof(double))
                return CorElementType.R8;

            throw new Exception($"Unknown type {runtimeType}");
        }

        public CorDebugValue ReadField(CorDebugValue value, string fieldName)
        {
            var type = value.ExactType;
            var clas = type.Class;

            var md = clas.Module.GetMetaDataInterface().MetaDataImport;
            var fields = md.EnumFieldsWithName(clas.Token, fieldName);

            if (value is CorDebugReferenceValue ptr)
            {
                value = ptr.Dereference();
            }

            var instance = value.As<CorDebugObjectValue>();
            return instance.GetFieldValue(clas.Raw, fields.Single());
        }

        public CorDebugHandleValue HoldObject(CorDebugValue instance)
        {
            if (instance is CorDebugReferenceValue reference)
            {
                instance = reference.Dereference();
            }

            var heapObject = instance.As<CorDebugHeapValue>();
            return heapObject.CreateHandle(CorDebugHandleType.HANDLE_STRONG);
        }
    }
}