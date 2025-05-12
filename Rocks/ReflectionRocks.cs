using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SpaceEditor.Rocks;

public static class ReflectionRocks
{
    public static Type? TryFindType(string assemblyHintPath, ReadOnlySpan<string> probeAssemblies, string typeName)
    {
        foreach(var assembly in probeAssemblies)
        {
            if (GetLib(assemblyHintPath, assembly).TryFindType(typeName) is {} foundType)
                return foundType;
        }

        return null;
    }

    public static Type? TryFindType(this Assembly assembly, string typeName)
    {
        var candidates = assembly.GetTypes().Where(x =>
        {
            var candidateName = x.FullName;
            if (candidateName!.EndsWith(typeName) == false)
                return false;

            if (candidateName.Length == typeName.Length)
                return true;

            var charBeforeName = candidateName[^(typeName.Length + 1)];
            var isPreciseTypeNameWithoutFurtherPrefix = charBeforeName is ' ' or '.' or '+';
            if (isPreciseTypeNameWithoutFurtherPrefix)
                return true;

            return false;
        });

        return candidates.SingleOrDefault();
    }

    public static IEnumerable<Type> TryFindDerives(this Assembly assembly, Type baseType)
    {
        var assemblyTypes = assembly.GetTypes();
        return assemblyTypes.Where(x =>
        {
            if (x.IsAbstract)
                return false;

            if (x == baseType)
                return false;

            return baseType.IsAssignableFrom(x);
        });
    }

    public static Assembly GetLib(string hintPath, string assembly)
    {
        var app = AppDomain.CurrentDomain;
        if (TryGetPreloadedAssembly(assembly) is {} preloaded)
        {
            return preloaded;
        }

        app.AssemblyResolve += (_, args) =>
        {
            var requestedName = new AssemblyName(args.Name).Name;
            if (TryGetPreloadedAssembly(requestedName) is {} preloaded)
            {
                return preloaded;
            }

            var assemblyFile = Path.Combine(hintPath, $"{requestedName}.dll");
            if (File.Exists(assemblyFile))
            {
                return Assembly.LoadFrom(assemblyFile);
            }

            return null;
        };

        return Assembly.Load(assembly);
    }

    public static Assembly? TryGetPreloadedAssembly(string assemblyName)
    {
        var app = AppDomain.CurrentDomain;
        return app.GetAssemblies().FirstOrDefault(x => x.GetName().Name == assemblyName);
    }

    public static IEnumerable<MemberInfo> GetInstanceMembers(this Type declaringType)
    {
        const BindingFlags INSTANCE = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        while (declaringType != typeof(object))
        {
            foreach (var member in declaringType.GetMembers(INSTANCE))
            {
                yield return member;
            }

            declaringType = declaringType.BaseType!;
        }
    }

    /// <summary>
    /// Take special care needed to allocate new Game ObjectBuilder instances.
    /// Fills default values and makes corrections after typically empty ctor.
    /// </summary>
    public static object AllocateObjectBuilder(this Type obType)
    {
        var instance = Activator.CreateInstance(obType)!;
        
        foreach (var field in obType.GetInstanceMembers().OfType<FieldInfo>())
        {
            if (typeof(ICollection).IsAssignableFrom(field.FieldType) == false)
                continue;

            if (field.GetValue(instance) is not null)
                continue;

            field.SetValue(instance, Activator.CreateInstance(field.FieldType));
        }

        return instance;
    }
}