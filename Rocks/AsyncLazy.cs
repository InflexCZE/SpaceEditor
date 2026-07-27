using System.Runtime.CompilerServices;

namespace SpaceEditor.Rocks;

public class AsyncLazy<T> : Lazy<Task<T>>
{
    public AsyncLazy(Func<T> valueFactory) :
        base(() => Task.Factory.StartNew(valueFactory))
    { }

    public AsyncLazy(Func<Task<T>> taskFactory) :
        base(() => Task.Factory.StartNew(taskFactory).Unwrap())
    { }

    public TaskAwaiter<T> GetAwaiter()
    {
        return Value.GetAwaiter();
    }

    /// <summary>
    /// Start background computation if needed
    /// </summary>
    public void Poke()
    {
        _ = this.Value;
    }
}