using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g4;

namespace SpaceEditor.Rocks;

public static class Enumerators
{
    public static IEnumerable<Vector3i> VolumeMaxInclusive(AxisAlignedBox3i box)
    {
        return BoxRange(box.Min, box.Max);
    }

    public static IEnumerable<Vector3i> BoxRange(AxisAlignedBox3d box, ShiftGridIndexer3 converter)
    {
        var min = converter.ToGrid(box.Min);
        var max = converter.ToGrid(box.Max);
        return BoxRange(min, max);
    }

    public static IEnumerable<Vector3i> BoxRange(Vector3i minInclusive, Vector3i maxInclusive)
    {
        for (int z = minInclusive.z; z <= maxInclusive.z; ++z)
        for (int y = minInclusive.y; y <= maxInclusive.y; ++y)
        for (int x = minInclusive.x; x <= maxInclusive.x; ++x)
        {
            yield return new(x, y, z);
        }
    }
}