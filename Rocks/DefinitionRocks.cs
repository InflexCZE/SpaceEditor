using ReflectionMagic;

namespace SpaceEditor.Rocks;

public static class DefinitionRocks
{
    public static object AllocateDefinitionStub(Type type, Guid id)
    {
        var definitionStub = Activator.CreateInstance(type)!;
        definitionStub.AsDynamic().Guid = id;
        return definitionStub;
    }
}