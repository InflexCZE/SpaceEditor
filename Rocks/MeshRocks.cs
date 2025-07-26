using g4;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceEditor.Rocks;

public static class MeshRocks
{
    public static IEnumerable<Triangle3d> EnumerateTriangles(this DMesh3 mesh)
    {
        foreach(var triangle in mesh.Triangles())
        {
            yield return new
            (
                mesh.GetVertex(triangle.a),
                mesh.GetVertex(triangle.b),
                mesh.GetVertex(triangle.c)
            );
        }
    }

    public static void AppendBox(this DMesh3 target, AxisAlignedBox3d box)
    {
        var b = new TrivialBox3Generator();
        b.Box = new Box3d(box);
        b.Generate();

        var boxMesh = b.MakeDMesh();
        target.AppendMesh(boxMesh);
    }

    public static void AppendMesh(this DMesh3 target, DMesh3 source)
    {
        var editor = new MeshEditor(target);
        editor.AppendMesh(source);
    }

    public static void AppendMesh(this DMesh3 target, DMesh3 source, Frame3f transform)
    {
        var editor = new MeshEditor(target);
        editor.AppendMesh(source, out var newVertices);

        foreach(var vertexId in newVertices)
        {
            var position = target.GetVertex(vertexId);
            //position = transform.Multiply(ref position);
            //position = transform.FromFrameP(ref position);
            //position = transform.Rotation.InverseMultiply(ref position) + transform.Origin;
            position = position + transform.Origin;

            //position = transform.ToFrameP(ref position);
            target.SetVertex(vertexId, position);
        }
    }

    public static void AppendSlope(this DMesh3 mesh, AxisAlignedBox3d box, Vector3i baseA, Vector3i baseB)
    {
        Span<Vector3d> vertices =
        [
            // Back lower anchor (-Z)
            new( 1, -1, -1),
            new(-1, -1, -1),

            // Forward lower base (+Z)
            new( 1, -1,  1),
            new(-1, -1,  1),

            // Back up base (+Y)
            new( 1,  1,  -1),
            new(-1,  1,  -1),
        ];

        var slopeNormal = (Vector3f.AxisY + Vector3f.AxisZ).Normalized;
        Span<(Vector3f, int)> normals =
        [
            // Low base (-Y)
            (-Vector3f.AxisY, 0),
            (-Vector3f.AxisY, 1),
            (-Vector3f.AxisY, 2),
            (-Vector3f.AxisY, 3),

            // Back base (-Z)
            (-Vector3f.AxisY, 4),
            (-Vector3f.AxisY, 5),
            (-Vector3f.AxisY, 0),
            (-Vector3f.AxisY, 1),

            // Slope quad
            (slopeNormal, 2),
            (slopeNormal, 3),
            (slopeNormal, 4),
            (slopeNormal, 5),

            // Right triangle
            (Vector3f.AxisX, 0),
            (Vector3f.AxisX, 4),
            (Vector3f.AxisX, 2),

            // Left triangle
            (-Vector3f.AxisX, 3),
            (-Vector3f.AxisX, 1),
            (-Vector3f.AxisX, 5),
        ];


        var baseY = -baseA;
        var baseZ = -baseB;
        var baseX = baseY.Cross(baseZ);

        var origin = box.Center;
        var scale = box.Extents;

        var rotation = new Matrix3d
        (
            baseX.ToVector3d(),
            baseY.ToVector3d(),
            baseZ.ToVector3d(),
            bRows: false
        );

        Span<int> indices = stackalloc int[normals.Length];
        for (int i = 0; i < indices.Length; ++i)
        {
            var (normal, vertexIndex) = normals[i];
            var vertex = rotation.Multiply(ref vertices[vertexIndex]) * scale + origin;

            Vector3d normalD = normal;
            normal = (Vector3f) rotation.Multiply(ref normalD);
            indices[i] = mesh.AppendVertex(new NewVertexInfo(vertex, normal));
        }

        // Left triangle
        mesh.AppendTriangle(indices[16], indices[15], indices[17]);

        // Right triangle
        mesh.AppendTriangle(indices[12], indices[13], indices[14]);

        // Bottom base
        mesh.AppendTriangle(indices[0], indices[3], indices[1]);
        mesh.AppendTriangle(indices[0], indices[2], indices[3]);

        // Back base
        mesh.AppendTriangle(indices[7], indices[5], indices[4]);
        mesh.AppendTriangle(indices[7], indices[4], indices[6]);

        // The slope
        mesh.AppendTriangle(indices[8], indices[10], indices[11]);
        mesh.AppendTriangle(indices[8], indices[11], indices[9]);
    }

    public static int CopyTriangle(this DMesh3 target, DMesh3 source, int triangleId)
    {
        var tri = source.GetTriangle(triangleId);
        return target.AppendTriangle
        (
            target.CopyVertex(source, tri.a),
            target.CopyVertex(source, tri.b),
            target.CopyVertex(source, tri.c)
        );
    }

    public static int CopyVertex(this DMesh3 target, DMesh3 source, int vertexId)
    {
        var vertex = source.GetVertexAll(vertexId);
        return target.AppendVertex(ref vertex);
    }
}