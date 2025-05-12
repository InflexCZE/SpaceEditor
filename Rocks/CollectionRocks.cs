using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SpaceEditor.Rocks;

public static class CollectionRocks
{
    public static TValue GetOrAdd<TKey, TValue>
    (
        this Dictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TKey, TValue>factory
    )
        where TKey : notnull
    {
        ref var value = ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary, key, out var existed);
        
        if (existed == false)
        {
            value = factory(key);
        }

        return value!;
    }
}