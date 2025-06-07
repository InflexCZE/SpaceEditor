using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            var md = type.Module.GetMetaDataInterface().MetaDataImport;
            var updateMethodToken = md.FindMethod(type.Token, methodName, default, 0);
            return type.Module.GetFunctionFromToken(updateMethodToken);
        }

        public async Task<CorDebugThread> CatchThreadInFunction(CorDebugFunction method, CorDebugThread? thread = null)
        {
            if (this.Process.IsRunning)
            {
                throw new Exception("Should not run");
            }

            var events = new List<object>();
            this.Callbacks.OnAnyEvent += (_, args) =>
            {
                events.Add(args);
            };

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

        public async Task<CorDebugHandleValue> ExecutePreparedEval(CorDebugEval eval, bool expectResult = false)
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

            if (result == fail)
            {
                throw new Exception("Eval failed");
            }

            if (expectResult == false)
            {
                return null!;
            }

            return (CorDebugHandleValue) eval.Result;
        }

        public unsafe CorDebugValue CreatePrimitiveValue<T>(CorDebugEval eval, T value)
            where T : unmanaged
        {
            CorElementType type;
            if (typeof(T) == typeof(bool))
                type = CorElementType.Boolean;
            else if (typeof(T) == typeof(char))
                type = CorElementType.Char;
            else if (typeof(T) == typeof(sbyte))
                type = CorElementType.I1;
            else if (typeof(T) == typeof(byte))
                type = CorElementType.U1;
            else if (typeof(T) == typeof(short))
                type = CorElementType.I2;
            else if (typeof(T) == typeof(ushort))
                type = CorElementType.U2;
            else if (typeof(T) == typeof(int))
                type = CorElementType.I4;
            else if (typeof(T) == typeof(uint))
                type = CorElementType.U4;
            else if (typeof(T) == typeof(long))
                type = CorElementType.I8;
            else if (typeof(T) == typeof(ulong))
                type = CorElementType.U8;
            else if (typeof(T) == typeof(float))
                type = CorElementType.R4;
            else if (typeof(T) == typeof(double))
                type = CorElementType.R8;
            else
                throw new Exception($"Unknown type {typeof(T)}");

            var valueHandle = (CorDebugGenericValue) eval.CreateValue(type, null);
            valueHandle.SetValue((IntPtr) (void*) &value);
            return valueHandle;
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