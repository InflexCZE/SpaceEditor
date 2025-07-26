using g4;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;

namespace SpaceEditor.Rocks;

public enum IntersectResult
{
    NoIntersection,
    Intersects,
    TriangleInsideBox,
}

public static class MathRocks
{
    public static Quaternionf ForwardUp(Vector3f forward, Vector3f up)
    {
        var baseY = up;
        var baseZ = -forward;
        var baseX = baseY.Cross(baseZ);

        var rotation = new Matrix3f
        (
            baseX,
            baseY,
            baseZ,
            bRows: false
        );

        return rotation.ToQuaternion();
    }

    public static Frame3f ForwardUpTranslate(Vector3f forward, Vector3f up, Vector3f translate)
    {
        return new(translate, ForwardUp(forward, up));
    }

    public static Frame3f ForwardUpTranslate(int forward, int up, Vector3f translate)
    {
        return ForwardUpTranslate
        (
            (Vector3f) Base6Directions.Vectors[forward],
            (Vector3f) Base6Directions.Vectors[up],
            translate
        );
    }

    public static AxisAlignedBox3d ToBox(this Triangle3d triangle)
    {
        var triBox = AxisAlignedBox3d.Empty;
        triBox.Contain(triangle.V0);
        triBox.Contain(triangle.V1);
        triBox.Contain(triangle.V2);
        return triBox;
    }

    public static AxisAlignedBox3d ToBox<T>(this T indexer, Vector3i grid)
        where T : IGridWorldIndexer3
    {
        var min = indexer.FromGrid(grid);
        var max = indexer.FromGrid(grid + 1);
        return new(min, max);
    }

    public static Vector3d ToVector3d(this Vector3i v)
    {
        return new(v.x, v.y, v.z);
    }

    public static Vector3i SignSelect(this Vector3i v, Vector3i positive, Vector3i zero, Vector3i negative)
    {
        return new
        (
            v.x > 0 ? positive.x : v.x == 0 ? zero.x : negative.x,
            v.y > 0 ? positive.y : v.y == 0 ? zero.y : negative.y,
            v.z > 0 ? positive.z : v.z == 0 ? zero.z : negative.z
        );
    }

    public static Vector3d SignSelect(this Vector3d v, Vector3d positive, Vector3d zero, Vector3d negative)
    {
        return new
        (
            v.x > 0 ? positive.x : v.x == 0 ? zero.x : negative.x,
            v.y > 0 ? positive.y : v.y == 0 ? zero.y : negative.y,
            v.z > 0 ? positive.z : v.z == 0 ? zero.z : negative.z
        );
    }

    public static Vector3i Cross(this Vector3i a, Vector3i b)
    {
        return new
        (
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x
        );
    }

    public static int ToLinearIndex(this DenseGrid3i grid, Vector3i index)
    {
        return index.x + grid.ni * (index.y + grid.nj * index.z);
    }

    public static bool IsValidIndex(this DenseGrid3i grid, Vector3i index)
    {
        return index.x >= 0 &&
               index.y >= 0 &&
               index.z >= 0 &&
               index.x < grid.ni &&
               index.y < grid.nj &&
               index.z < grid.nk;
    }

    public static IntersectResult IntersectWithTriangle(this AxisAlignedBox3d box, Triangle3d tri)
    {
        return BoxTriangleIntersection.IntersectBoxWithTriangle(box, tri);
    }
    
    public static Bitmap3 Clone(this Bitmap3 value)
    {
        var copy = new Bitmap3(value.Dimensions);

        foreach (var nz in value.NonZeros())
        {
            copy.Set(nz, true);
        }

        return copy;
    }

    private static class BoxTriangleIntersection
    {
        public static IntersectResult IntersectBoxWithTriangle(AxisAlignedBox3d box, Triangle3d tri)
        {
            Vector3d boxCenter = box.Center;
            Vector3d boxHalfSize = box.Extents;

            // Early out: check if all triangle vertices are inside the box (triangle inside box)
            if (box.Contains(tri.V0) &&
                box.Contains(tri.V1) &&
                box.Contains(tri.V2))
            {
                return IntersectResult.TriangleInsideBox;
            }

            // Translate triangle vertices relative to box center
            Vector3d v0 = tri.V0 - boxCenter;
            Vector3d v1 = tri.V1 - boxCenter;
            Vector3d v2 = tri.V2 - boxCenter;

            // Edges of the triangle
            Vector3d e0 = v1 - v0;
            Vector3d e1 = v2 - v1;
            Vector3d e2 = v0 - v2;

            // --- 1) Test the 9 axes from cross products of edges with box axes ---

            if (!AxisTestX01(e0.z, e0.y, v0, v1, v2, boxHalfSize)) return IntersectResult.NoIntersection;
            if (!AxisTestY02(e0.z, e0.x, v0, v1, v2, boxHalfSize)) return IntersectResult.NoIntersection;
            if (!AxisTestZ12(e0.y, e0.x, v0, v1, v2, boxHalfSize)) return IntersectResult.NoIntersection;

            if (!AxisTestX01(e1.z, e1.y, v0, v1, v2, boxHalfSize)) return IntersectResult.NoIntersection;
            if (!AxisTestY02(e1.z, e1.x, v0, v1, v2, boxHalfSize)) return IntersectResult.NoIntersection;
            if (!AxisTestZ12(e1.y, e1.x, v0, v1, v2, boxHalfSize)) return IntersectResult.NoIntersection;

            if (!AxisTestX01(e2.z, e2.y, v0, v1, v2, boxHalfSize)) return IntersectResult.NoIntersection;
            if (!AxisTestY02(e2.z, e2.x, v0, v1, v2, boxHalfSize)) return IntersectResult.NoIntersection;
            if (!AxisTestZ12(e2.y, e2.x, v0, v1, v2, boxHalfSize)) return IntersectResult.NoIntersection;

            // --- 2) Test overlap in the x, y, and z directions ---
            if (!OverlapInAxis(v0.x, v1.x, v2.x, boxHalfSize.x)) return IntersectResult.NoIntersection;
            if (!OverlapInAxis(v0.y, v1.y, v2.y, boxHalfSize.y)) return IntersectResult.NoIntersection;
            if (!OverlapInAxis(v0.z, v1.z, v2.z, boxHalfSize.z)) return IntersectResult.NoIntersection;

            // --- 3) Test if the plane of the triangle intersects the box ---
            Vector3d normal = Vector3d.Cross(e0, e1);
            if (!PlaneBoxOverlap(normal, v0, boxHalfSize)) return IntersectResult.NoIntersection;

            // If all tests passed, triangle intersects the box
            return IntersectResult.Intersects;
        }

        // Axis tests (3 variants)
        static bool AxisTestX01(double a, double b, Vector3d v0, Vector3d v1, Vector3d v2, Vector3d boxHalfSize)
        {
            double p0 = a * v0.y - b * v0.z;
            double p1 = a * v1.y - b * v1.z;
            double p2 = a * v2.y - b * v2.z;

            double min = Math.Min(p0, Math.Min(p1, p2));
            double max = Math.Max(p0, Math.Max(p1, p2));

            double rad = Math.Abs(a) * boxHalfSize.y + Math.Abs(b) * boxHalfSize.z;

            return !(min > rad || max < -rad);
        }


        static bool AxisTestY02(double a, double b, Vector3d v0, Vector3d v1, Vector3d v2, Vector3d boxHalfSize)
        {
            double p0 = -a * v0.x + b * v0.z;
            double p1 = -a * v1.x + b * v1.z;
            double p2 = -a * v2.x + b * v2.z;

            double min = Math.Min(p0, Math.Min(p1, p2));
            double max = Math.Max(p0, Math.Max(p1, p2));

            double rad = Math.Abs(a) * boxHalfSize.x + Math.Abs(b) * boxHalfSize.z;

            return !(min > rad || max < -rad);
        }

        static bool AxisTestZ12(double a, double b, Vector3d v0, Vector3d v1, Vector3d v2, Vector3d boxHalfSize)
        {
            double p0 = a * v0.x - b * v0.y;
            double p1 = a * v1.x - b * v1.y;
            double p2 = a * v2.x - b * v2.y;

            double min = Math.Min(p0, Math.Min(p1, p2));
            double max = Math.Max(p0, Math.Max(p1, p2));

            double rad = Math.Abs(a) * boxHalfSize.x + Math.Abs(b) * boxHalfSize.y;

            return !(min > rad || max < -rad);
        }

        // Overlap test in a single axis
        static bool OverlapInAxis(double v0, double v1, double v2, double boxHalfSize)
        {
            double min = Math.Min(v0, Math.Min(v1, v2));
            double max = Math.Max(v0, Math.Max(v1, v2));
            if (min > boxHalfSize) return false;
            if (max < -boxHalfSize) return false;
            return true;
        }

        // Plane-box overlap test
        static bool PlaneBoxOverlap(Vector3d normal, Vector3d vert, Vector3d maxBox)
        {
            Vector3d vmin = new Vector3d();
            Vector3d vmax = new Vector3d();

            if (normal.x > 0.0)
            {
                vmin.x = -maxBox.x - vert.x;
                vmax.x = maxBox.x - vert.x;
            }
            else
            {
                vmin.x = maxBox.x - vert.x;
                vmax.x = -maxBox.x - vert.x;
            }

            if (normal.y > 0.0)
            {
                vmin.y = -maxBox.y - vert.y;
                vmax.y = maxBox.y - vert.y;
            }
            else
            {
                vmin.y = maxBox.y - vert.y;
                vmax.y = -maxBox.y - vert.y;
            }

            if (normal.z > 0.0)
            {
                vmin.z = -maxBox.z - vert.z;
                vmax.z = maxBox.z - vert.z;
            }
            else
            {
                vmin.z = maxBox.z - vert.z;
                vmax.z = -maxBox.z - vert.z;
            }

            if (Vector3d.Dot(normal, vmin) > 0.0) return false;
            if (Vector3d.Dot(normal, vmax) >= 0.0) return true;

            return false;
        }
    }
}