using Assimp;
using g4;
using SpaceEditor.Rocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpaceEditor.Algorithms;

public class GridShaper
{
    public DMesh3 Mesh { get; }
    public DMeshAABBTree3 Tree { get; }

    public GridShaper(DMesh3 mesh, DMeshAABBTree3 tree)
    {
        this.Mesh = mesh;
        this.Tree = tree;
    }

    public class GeneratorSettings
    {
        public bool SlopesUpper { get; set; } = true;
        public bool SlopesLower { get; set; } = true;
        public bool SlopesSides { get; set; } = true; 
    }

    public BlueprintMesh Generate(GeneratorSettings settings, CancellationToken ct)
    {
        var blockSize = 2.5;
        var boundingBox = new AxisAlignedBox3d(new Vector3d(0), blockSize / 2);
        while (boundingBox.Contains(this.Tree.Bounds) == false)
        {
            //TODO: Fix block offset
            boundingBox.Scale(2, 2, 2);
        }

        var cellCount = (int) Math.Ceiling(boundingBox.MaxDim / blockSize);
        var indexer = new ShiftGridIndexer3(boundingBox.Min, blockSize);

        var bmp = new DenseGrid3i(cellCount, cellCount, cellCount, BlueprintMesh.NoContent);
        
        foreach (var triangle in this.Mesh.EnumerateTriangles())
        {
            var triBox = triangle.ToBox();
            foreach (var cell in Enumerators.BoxRange(triBox, indexer))
            {
                var cellBox = indexer.ToBox(cell);
                if (cellBox.IntersectWithTriangle(triangle) != IntersectResult.NoIntersection)
                {
                    bmp[cell] = 0; //Cube
                }
            }
        }

        if (settings.SlopesUpper)
        {
            ExecSlopes(1);
            ExecSlopes(2);
            ExecSlopes(3);
            ExecSlopes(4);
        }

        if (settings.SlopesLower)
        {
            ExecSlopes(5);
            ExecSlopes(6);
            ExecSlopes(7);
            ExecSlopes(8);
        }

        if (settings.SlopesSides)
        {
            ExecSlopes(9);
            ExecSlopes(10);
            ExecSlopes(11);
            ExecSlopes(12);
        }

        void ExecSlopes(int content)
        {
            var shapeInfo = ShapeDB.Instance.Shapes[content];
            var probeA = -Base6Directions.Vectors[shapeInfo.Forward];
            var probeB = Base6Directions.Vectors[shapeInfo.Up];

            foreach (var g in bmp.Indices())
            {
                if (bmp[g] != 0)
                    continue;

                Vector3i b = default;
                bool valid = Offset(probeA, out var a) &&
                             Offset(probeB, out b);

                if (valid == false)
                    continue;

                if (bmp[a] != BlueprintMesh.NoContent || bmp[b] != BlueprintMesh.NoContent)
                    continue;

                bmp[g] = content;

                bool Offset(Vector3i offset, out Vector3i value)
                {
                    value = g + offset;
                    return bmp.IsValidIndex(value);
                }
            }
        }

        return new()
        {
            Blocks = bmp,
            Coords = indexer
        };
    }
}

public class GridMesher
{
    public static DMesh3 Mesh(BlueprintMesh blueprint)
    {
        var grid = blueprint.Blocks;
        
        var cubes = new Bitmap3(new(grid.ni, grid.nj, grid.nk));
        foreach(var g in grid.Indices())
        {
            cubes[g] = grid[g] == 0;
        }

        var slopeMesh = new DMesh3();
        foreach (var g in grid.Indices())
        {
            if (cubes[g])
                continue;

            var shapeId = grid[g];
            if (shapeId == BlueprintMesh.NoContent)
                continue;

            var shapeInfo = ShapeDB.Instance.Shapes[shapeId];

            slopeMesh.AppendMesh
            (
                shapeInfo.Shape,
                MathRocks.ForwardUpTranslate
                (
                    shapeInfo.Forward,
                    shapeInfo.Up,
                    (Vector3f) blueprint.Coords.ToBox(g).Center
                )
            );
        }
        
        var cubesSurfaceGenerator = new VoxelSurfaceGenerator();
        cubesSurfaceGenerator.Voxels = cubes;
        cubesSurfaceGenerator.Generate();
        
        var cubesMesh = cubesSurfaceGenerator.Meshes[0];
        MeshTransforms.Scale(cubesMesh, 2.5);

        // Voxel generator generates around UnitZeroCentered, while indexer rounds down to corner
        var correctionOffset = blueprint.Coords.CellSize / 2;
        MeshTransforms.Translate(cubesMesh, blueprint.Coords.Origin + correctionOffset);


        var finalMesh = cubesMesh;
        finalMesh.AppendMesh(slopeMesh);

        return finalMesh;
    }
}

public class ShapeDB
{
    public const float LargeBlockSize = 2.5f;
    public const float SmallBlockSize = 0.25f;
    public const float LargeBlockHalfExtent = LargeBlockSize / 2;

    public static AxisAlignedBox3f CenterBlockAlignment => new(Vector3f.Zero, LargeBlockHalfExtent);

    public static ShapeDB Instance = new();

    public record ShapeInfo
    {
        public DMesh3 Shape;
        public string Prefab;

        public int Forward = Base6Directions.Forward;
        public int Up = Base6Directions.Up;
    }

    public ShapeInfo[] Shapes =
    [
        // Cube
        CubicShape("2eacbbf2-d8fb-4a78-91dc-7b492517ef97", x => x.AppendBox(CenterBlockAlignment)),

        // Slopes
        SlopeShape(Base6Directions.Left, Base6Directions.Up),
        SlopeShape(Base6Directions.Right, Base6Directions.Up),
        SlopeShape(Base6Directions.Forward, Base6Directions.Up),
        SlopeShape(Base6Directions.Backward, Base6Directions.Up),

        SlopeShape(Base6Directions.Left, Base6Directions.Down),
        SlopeShape(Base6Directions.Right, Base6Directions.Down),
        SlopeShape(Base6Directions.Forward, Base6Directions.Down),
        SlopeShape(Base6Directions.Backward, Base6Directions.Down),

        SlopeShape(Base6Directions.Forward, Base6Directions.Left),
        SlopeShape(Base6Directions.Left, Base6Directions.Backward),
        SlopeShape(Base6Directions.Backward, Base6Directions.Right),
        SlopeShape(Base6Directions.Right, Base6Directions.Forward),
    ];

    private static ShapeInfo SlopeShape(int forward, int up)
    {
        var info = CubicShape
        (
            "f9efcc6c-6c76-4762-bbf0-6013ec969539",
            x =>
            {
                x.AppendSlope
                (
                    CenterBlockAlignment,
                    Base6Directions.Vectors[forward],
                    -Base6Directions.Vectors[up]
                );
            }
        );

        info.Up = up;
        info.Forward = forward;
        return info;
    }

    private static ShapeInfo CubicShape(string prefab, Action<DMesh3> shape)
    {
        return new()
        {
            Prefab = prefab,
            Shape = MakeShape(shape)
        };
    }

    private static DMesh3 MakeShape(Action<DMesh3> factory)
    {
        var mesh = new DMesh3();
        factory(mesh);
        return mesh;
    }
}

public class BlueprintMesh
{
    public const int NoContent = int.MaxValue;

    public DenseGrid3i Blocks;
    public ShiftGridIndexer3 Coords;
}

public class BlueprintWriter
{
    public required string WriteFolder { get; set; }

    public void Write(BlueprintMesh blueprint, string name)
    {
        var sb = new StringBuilder();
        Generate(blueprint, sb);
        File.WriteAllText(Path.Combine(this.WriteFolder, $"{name}.txt"), sb.ToString());
    }

    public void Generate(BlueprintMesh blueprint, StringBuilder sb)
    {
        //Prefab|PositionX|PositionY|PositionZ|ColorHUE|ColorSATURATION|ColorVALUE|OrientationFORWARD|OrientationUP|Integrity
        var blockGrid = blueprint.Blocks;
        foreach (var g in blockGrid.Indices())
        {
            var content = blockGrid[g];
            if (content == BlueprintMesh.NoContent)
                continue;

            if (content < 0)
            {
                throw new NotImplementedException("Shape lists will go here");
            }

            var block = ShapeDB.Instance.Shapes[content];
            sb.Append(block.Prefab);
            sb.Append('|');

            var forwardAxis = block.Forward;
            var upAxis = block.Up;

            var cube = blueprint.Coords.ToBox(g);
            var gridPosition = ToInt(cube.Center / ShapeDB.SmallBlockSize);
            gridPosition += PositionOffset
            (
                new(new Vector3i(-4, -4, -4), new Vector3i(5, 5, 5)),
                forwardAxis,
                upAxis
            );


            sb.Append(gridPosition.x);
            sb.Append('|');
            sb.Append(gridPosition.y);
            sb.Append('|');
            sb.Append(gridPosition.z);
            sb.Append('|');

            sb.Append(0);
            sb.Append('|');
            sb.Append(0);
            sb.Append('|');
            sb.Append(0.25);
            sb.Append('|');

            sb.Append(forwardAxis);
            sb.Append('|');
            sb.Append(upAxis);
            sb.Append('|');

            sb.Append(1);
            sb.Append('|');

            sb.AppendLine();
        }

        Vector3i PositionOffset(AxisAlignedBox3i blockSize, int blockForward, int blockRight)
        {
            var baseRight = Base6Directions.Vectors[blockRight];
            var baseForward = -Base6Directions.Vectors[blockForward];
            return BlockOffset
            (
                blockSize,
                new Matrix3f
                (
                    (Vector3f) baseRight.Cross(baseForward),
                    (Vector3f) baseRight,
                    (Vector3f) baseForward,
                    bRows: false
                )
            );
        }

        static Vector3i BlockOffset(AxisAlignedBox3i blockSize, Matrix3f blockOrientation)
        {
            var offsetNegative = (Vector3f) blockSize.Min;
            var offsetPositive = (Vector3f) blockSize.Max;

            var a = ToInt(blockOrientation.Multiply(ref offsetNegative));
            var b = ToInt(blockOrientation.Multiply(ref offsetPositive));

            var minI = new Vector3i(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
            return blockSize.Min - minI;
        }

        static Vector3i ToInt(Vector3d vec)
        {
            return new
            (
                (int) Math.Round(vec.x),
                (int) Math.Round(vec.y),
                (int) Math.Round(vec.z)
            );
        }
    }
}