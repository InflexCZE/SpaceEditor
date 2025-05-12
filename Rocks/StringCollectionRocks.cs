using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEditor.Rocks;

public static class StringCollectionRocks
{
    public static Dictionary<string, string> ToDictionary(this StringCollection? sc)
    {
        var dict = new Dictionary<string, string>();
        if (sc is null)
        {
            return dict;
        }

        if (sc.Count % 2 != 0)
        {
            throw new InvalidDataException("Broken dictionary");
        }

        for (var i = 0; i < sc.Count; i += 2)
        {
            dict.Add(sc[i], sc[i + 1]);
        }
        return dict;
    }

    public static StringCollection ToStringCollection(this Dictionary<string, string> dict)
    {
        var sc = new StringCollection();
        foreach (var d in dict)
        {
            sc.Add(d.Key);
            sc.Add(d.Value);
        }
        return sc;
    }
}