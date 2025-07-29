using Assimp;
using g4;
using SpaceEditor.Rocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PropertyTools.DataAnnotations;

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

    public static class BlockSizes
    {
        public const string TwoPointFive = "2.5m";
        public const string HalfMeter = "0.5m (VERY VERY slow on large ships!)";
        public const string TwentyFiveC = nameof(TwentyFiveC);
    }

    public class GeneratorSettings
    {
        public bool SlopesUpper { get; set; } = true;
        public bool SlopesLower { get; set; } = true;
        public bool SlopesSides { get; set; } = true; 
        public bool SlopesMustBeSupported { get; set; } = false;

        [ItemsSourceProperty(nameof(BlockSizeValues))]
        public string BlockSize { get; set; } = BlockSizes.TwoPointFive;

        [Browsable(false)]
        public List<string> BlockSizeValues { get; } =
        [
            BlockSizes.TwoPointFive,
            BlockSizes.HalfMeter,
            //BlockSizes.TwentyFiveC,
        ];
    }

    public BlueprintMesh Generate(GeneratorSettings settings, CancellationToken ct)
    {
        var blockSize = settings.BlockSize switch
        {
            BlockSizes.TwoPointFive => ShapeDB.LargeBlockSize,
            BlockSizes.HalfMeter => ShapeDB.MidBlockSize
        };

        var minimalBounds = this.Tree.Bounds;
        minimalBounds.Min -= blockSize;
        minimalBounds.Max += blockSize;
        
        var boundingBox = new AxisAlignedBox3d(new Vector3d(0), blockSize / 2);
        while (boundingBox.Contains(minimalBounds) == false)
        {
            //TODO: Fix block offset
            boundingBox.Scale(2, 2, 2);
        }

        var cellCount = (int) Math.Ceiling(boundingBox.MaxDim / blockSize);
        var indexer = new ShiftGridIndexer3(boundingBox.Min, blockSize);

        var bmp = new DenseGrid3i(cellCount, cellCount, cellCount, BlueprintMesh.NoContent);
        
        var blueprint = new BlueprintMesh();
        blueprint.Blocks = bmp;
        blueprint.Coords = indexer;
        blueprint.Shapes = settings.BlockSize switch
        {
            BlockSizes.TwoPointFive => ShapeDB.LargeShapes,
            BlockSizes.HalfMeter => ShapeDB.MidShapes
        };
        
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
            var shapeInfo = blueprint.Shapes[content];
            var probeDirectionA = -Base6Directions.Vectors[shapeInfo.Forward];
            var probeDirectionB = Base6Directions.Vectors[shapeInfo.Up];
            var supportDirectionA = -probeDirectionA;
            var supportDirectionB = -probeDirectionB;

            foreach (var g in bmp.Indices())
            {
                if (blueprint[g] != 0)
                    continue;

                if
                (
                    blueprint[g + probeDirectionA] != BlueprintMesh.NoContent ||
                    blueprint[g + probeDirectionB] != BlueprintMesh.NoContent
                )
                {
                    continue;
                }

                if (settings.SlopesMustBeSupported)
                {
                    if
                    (
                        //TODO: Should use face check, something symmetric
                        blueprint[g + supportDirectionA] != 0 ||
                        blueprint[g + supportDirectionB] != 0
                    )
                    {
                        continue;
                    }
                }

                bmp[g] = content;
            }
        }

        return blueprint;
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

            var shapeInfo = blueprint.Shapes[shapeId];

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
        MeshTransforms.Scale(cubesMesh, blueprint.Coords.CellSize);

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
    public const float MidBlockSize = 0.5f;
    public const float SmallBlockSize = 0.25f;

    public record ShapeInfo
    {
        public DMesh3 Shape;
        public string Prefab;

        public int Forward = Base6Directions.Forward;
        public int Up = Base6Directions.Up;
    }

    public ShapeInfo[] Shapes { get; }
    public ShapeInfo this[int index] => this.Shapes[index];

    public ShapeDB(params ShapeInfo[] shapes)
    {
        this.Shapes = shapes;
    }

    public static ShapeDB LargeShapes = new
    (
        // Cube
        CubicShape("2eacbbf2-d8fb-4a78-91dc-7b492517ef97", x => x.AppendBox(Dims(LargeBlockSize))),

        // Slopes
        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Left, Base6Directions.Up),
        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Right, Base6Directions.Up),
        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Forward, Base6Directions.Up),
        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Backward, Base6Directions.Up),

        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Left, Base6Directions.Down),
        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Right, Base6Directions.Down),
        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Forward, Base6Directions.Down),
        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Backward, Base6Directions.Down),

        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Forward, Base6Directions.Left),
        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Left, Base6Directions.Backward),
        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Backward, Base6Directions.Right),
        SlopeShape("f9efcc6c-6c76-4762-bbf0-6013ec969539", LargeBlockSize, Base6Directions.Right, Base6Directions.Forward)
    );

    public static ShapeDB MidShapes = new
    (
        // Cube
        CubicShape("632d7385-12b9-47a6-802a-a610d0cbd1e0", x => x.AppendBox(Dims(MidBlockSize))),

        // Slopes
        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Left, Base6Directions.Up),
        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Right, Base6Directions.Up),
        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Forward, Base6Directions.Up),
        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Backward, Base6Directions.Up),

        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Left, Base6Directions.Down),
        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Right, Base6Directions.Down),
        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Forward, Base6Directions.Down),
        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Backward, Base6Directions.Down),

        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Forward, Base6Directions.Left),
        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Left, Base6Directions.Backward),
        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Backward, Base6Directions.Right),
        SlopeShape("69902790-3e2d-43d2-81e4-1c0b42bc7461", MidBlockSize, Base6Directions.Right, Base6Directions.Forward)
    );

    private static ShapeInfo SlopeShape(string prefab, float size, int forward, int up)
    {
        var info = CubicShape
        (
            prefab,
            x =>
            {
                x.AppendSlope
                (
                    Dims(size),
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

    private static AxisAlignedBox3f Dims(float size)
    {
        return new(Vector3f.Zero, size / 2);
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
    public ShapeDB Shapes;

    public int this[Vector3i index]
    {
        get
        {
            if (this.Blocks.IsValidIndex(index) == false)
                return NoContent;

            return this.Blocks[index];
        }
    }
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

            var block = blueprint.Shapes[content];
            sb.Append(block.Prefab);
            sb.Append('|');

            var forwardAxis = block.Forward;
            var upAxis = block.Up;

            var cube = blueprint.Coords.ToBox(g);
            var gridPosition = ToInt(cube.Center / ShapeDB.SmallBlockSize);
            gridPosition += PositionOffset
            (
                //TODO:
                blueprint.Shapes == ShapeDB.LargeShapes ?
                    new AxisAlignedBox3i(new Vector3i(-4, -4, -4), new Vector3i(5, 5, 5)) : 
                    new AxisAlignedBox3i(new Vector3i(0, 0, 0), new Vector3i(1, 1, 1)),
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