using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpaceEditor.Rocks;

public static class StringRocks
{
    public static Stream AsStream(this string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }
}