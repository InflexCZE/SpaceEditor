using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceEditor.Rocks;

public class Disposable : IDisposable
{
    public static readonly IDisposable Empty = Disposable.Create(() => { });

    private Action Action;

    public Disposable(Action action)
    {
        this.Action = action;
    }

    public void Dispose()
    {
        this.Action.Invoke();
    }

    public static IDisposable Create(Action action)
    {
        return new Disposable(action);
    }
}