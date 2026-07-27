using g4;
using ILGPU;
using ILGPU.Runtime;
using PropertyTools.DataAnnotations;
using SpaceEditor.Rocks;
using System.IO;
using System.Runtime;
using System.Text;

namespace SpaceEditor.Algorithms;

// Unmanaged, SIMD-free representation of a 3D vector for ILGPU
public struct Float3
{
    public float X, Y, Z;
    public Float3(float x, float y, float z) { X = x; Y = y; Z = z; }
}

// Unmanaged representation of a triangle for the GPU
public struct GpuTriangle
{
    public Float3 V0;
    public Float3 V1;
    public Float3 V2;
    public Float3 MinBounds;
    public Float3 MaxBounds;
}

// Struct to bypass the C# 16-parameter limit for delegates
public struct GpuSlopeParams
{
    public int GridX, GridY, GridZ;
    public int PAx, PAy, PAz;
    public int PBx, PBy, PBz;
    public int SAx, SAy, SAz;
    public int SBx, SBy, SBz;
    public int Content;
    public int MustBeSupported;
}

public class GridShaper
{
    public DMesh3 Mesh { get; }
    public DMeshAABBTree3 Tree { get; }

    public GridShaper(DMesh3 mesh, DMeshAABBTree3 tree)
    {
        this.Mesh = mesh;
        this.Tree = tree;
    }

    // Class to cache the GPU context and kernels globally
    public static class GpuSetup
    {
        public static Context Context { get; private set; }
        public static Accelerator Accelerator { get; private set; }
        public static Action<Index1D, ArrayView<GpuTriangle>, ArrayView<int>, Float3, float, int, int, int> VoxelizeKernel { get; private set; }
        public static Action<Index1D, ArrayView<int>, int> InitGridKernel { get; private set; }
        public static Action<Index1D, ArrayView<int>, GpuSlopeParams> SlopeKernel { get; private set; }

        public static readonly object SyncLock = new object();

        static GpuSetup()
        {
            RebuildContext();
        }

        public static void RebuildContext()
        {
            lock (SyncLock)
            {
                // Force ILGPU to release all cached VRAM pools back to the OS
                try { Accelerator?.Dispose(); } catch { }
                try { Context?.Dispose(); } catch { }

                Context = Context.CreateDefault();
                Accelerator = Context.GetPreferredDevice(preferCPU: false).CreateAccelerator(Context);
                System.Diagnostics.Debug.WriteLine($"\n[ILGPU INITIALIZATION] Compiled and Cached on: {Accelerator.Name} (Type: {Accelerator.AcceleratorType})\n");

                VoxelizeKernel = Accelerator.LoadAutoGroupedStreamKernel<
                    Index1D, ArrayView<GpuTriangle>, ArrayView<int>, Float3, float, int, int, int>(
                    VoxelizationKernel.Voxelize);

                InitGridKernel = Accelerator.LoadAutoGroupedStreamKernel<
                    Index1D, ArrayView<int>, int>(
                    VoxelizationKernel.InitializeGrid);

                SlopeKernel = Accelerator.LoadAutoGroupedStreamKernel<
                    Index1D, ArrayView<int>, GpuSlopeParams>(
                    VoxelizationKernel.GenerateSlopes);
            }
        }
    }

    public static class BlockSizes
    {
        public const string TwoPointFive = "2.5m";
        public const string HalfMeter = "0.5m (May be slow on large ships!)";
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

    // --- PUBLIC FACADES ---
    // These methods are the entry points for generating a blueprint mesh, either using GPU acceleration or CPU processing.
    public BlueprintMesh Generate(GeneratorSettings settings, CancellationToken ct, IProgress<(double, string)> progress = null)
        => CoreGenerate(settings, useGpu: true, ct, progress);

    public BlueprintMesh GenerateCpu(GeneratorSettings settings, CancellationToken ct, IProgress<(double, string)> progress = null)
        => CoreGenerate(settings, useGpu: false, ct, progress);


    // --- UNIFIED GENERATOR ---
    private BlueprintMesh CoreGenerate(GeneratorSettings settings, bool useGpu, CancellationToken ct, IProgress<(double, string)> progress)
    {
        // ==========================================
        // PHASE 1: SHARED SETUP & GRID ALLOCATION
        // ==========================================
        var blockSize = settings.BlockSize switch
        {
            BlockSizes.TwoPointFive => ShapeDB.LargeBlockSize,
            BlockSizes.HalfMeter => ShapeDB.MidBlockSize
        };

        var bounds = this.Tree.Bounds;
        bounds.Expand(blockSize);

        int gridX = (int)Math.Ceiling(bounds.Width / blockSize);
        int gridY = (int)Math.Ceiling(bounds.Height / blockSize);
        int gridZ = (int)Math.Ceiling(bounds.Depth / blockSize);

        long totalGridVolume = (long)gridX * gridY * gridZ;

        var indexer = new ShiftGridIndexer3(bounds.Min, blockSize);
        var bmp = new DenseGrid3i(gridX, gridY, gridZ, BlueprintMesh.NoContent);
        int triangleCount = this.Mesh.TriangleCount;

        var blueprint = new BlueprintMesh
        {
            Blocks = bmp,
            Coords = indexer,
            Shapes = settings.BlockSize switch
            {
                BlockSizes.TwoPointFive => ShapeDB.LargeShapes,
                BlockSizes.HalfMeter => ShapeDB.MidShapes
            }
        };

        IEnumerable<Vector3i> activeBlocks;

        // ==========================================
        // PHASE 2 & 3: GPU VOXELIZATION & SLOPE GENERATION
        // ==========================================
        if (useGpu)
        {
            Accelerator accelerator;
            Action<Index1D, ArrayView<GpuTriangle>, ArrayView<int>, Float3, float, int, int, int> voxelizeKernel;
            Action<Index1D, ArrayView<int>, int> initGridKernel;
            Action<Index1D, ArrayView<int>, GpuSlopeParams> slopeKernel;

            lock (GpuSetup.SyncLock)
            {
                accelerator = GpuSetup.Accelerator;
                voxelizeKernel = GpuSetup.VoxelizeKernel;
                initGridKernel = GpuSetup.InitGridKernel;
                slopeKernel = GpuSetup.SlopeKernel;
            }

            System.Diagnostics.Debug.WriteLine($"\n[ILGPU VERIFICATION] Executing on: {accelerator.Name} (Type: {accelerator.AcceleratorType})\n");

            var flatTriangles = new GpuTriangle[triangleCount];
            int tIndex = 0;

            foreach (var triangle in this.Mesh.EnumerateTriangles())
            {
                ct.ThrowIfCancellationRequested();
                if (tIndex % 5000 == 0) progress?.Report((0.1 * ((double)tIndex / triangleCount), "Mesh Flattening..."));

                var box = triangle.ToBox();
                flatTriangles[tIndex++] = new GpuTriangle
                {
                    V0 = new Float3((float)triangle.V0.x, (float)triangle.V0.y, (float)triangle.V0.z),
                    V1 = new Float3((float)triangle.V1.x, (float)triangle.V1.y, (float)triangle.V1.z),
                    V2 = new Float3((float)triangle.V2.x, (float)triangle.V2.y, (float)triangle.V2.z),
                    MinBounds = new Float3((float)box.Min.x, (float)box.Min.y, (float)box.Min.z),
                    MaxBounds = new Float3((float)box.Max.x, (float)box.Max.y, (float)box.Max.z)
                };
            }

            int totalCells = (int)totalGridVolume;

            try
            {
                using var deviceTriangles = accelerator.Allocate1D(flatTriangles);
                using var deviceGrid = accelerator.Allocate1D<int>(totalCells);

                initGridKernel(totalCells, deviceGrid.View, BlueprintMesh.NoContent);

                Float3 origin = new Float3((float)bounds.Min.x, (float)bounds.Min.y, (float)bounds.Min.z);

                voxelizeKernel(deviceTriangles.IntExtent, deviceTriangles.View, deviceGrid.View, origin, blockSize, gridX, gridY, gridZ);
                accelerator.Synchronize();
                progress?.Report((0.40, "Voxelization complete on GPU..."));

                int totalSlopePasses = (settings.SlopesUpper ? 4 : 0) + (settings.SlopesLower ? 4 : 0) + (settings.SlopesSides ? 4 : 0);
                int executedPasses = 0;

                void ExecGpuSlopes(int content)
                {
                    var shapeInfo = blueprint.Shapes[content];
                    var probeDirectionA = -Base6Directions.Vectors[shapeInfo.Forward];
                    var probeDirectionB = Base6Directions.Vectors[shapeInfo.Up];
                    var supportDirectionA = -probeDirectionA;
                    var supportDirectionB = -probeDirectionB;

                    var slopeParams = new GpuSlopeParams
                    {
                        GridX = gridX,
                        GridY = gridY,
                        GridZ = gridZ,
                        PAx = probeDirectionA.x,
                        PAy = probeDirectionA.y,
                        PAz = probeDirectionA.z,
                        PBx = probeDirectionB.x,
                        PBy = probeDirectionB.y,
                        PBz = probeDirectionB.z,
                        SAx = supportDirectionA.x,
                        SAy = supportDirectionA.y,
                        SAz = supportDirectionA.z,
                        SBx = supportDirectionB.x,
                        SBy = supportDirectionB.y,
                        SBz = supportDirectionB.z,
                        Content = content,
                        MustBeSupported = settings.SlopesMustBeSupported ? 1 : 0
                    };

                    slopeKernel(totalCells, deviceGrid.View, slopeParams);
                    accelerator.Synchronize();

                    if (totalSlopePasses > 0)
                    {
                        int currentPass = Interlocked.Increment(ref executedPasses);
                        progress?.Report((0.40 + (0.30 * ((double)currentPass / totalSlopePasses)), "GPU Slope Evaluation..."));
                    }
                }

                if (settings.SlopesUpper) { ExecGpuSlopes(1); ExecGpuSlopes(2); ExecGpuSlopes(3); ExecGpuSlopes(4); }
                if (settings.SlopesLower) { ExecGpuSlopes(5); ExecGpuSlopes(6); ExecGpuSlopes(7); ExecGpuSlopes(8); }
                if (settings.SlopesSides) { ExecGpuSlopes(9); ExecGpuSlopes(10); ExecGpuSlopes(11); ExecGpuSlopes(12); }

                int[] flatResults = deviceGrid.GetAsArray1D();
                _ = Task.Run(() => GpuSetup.RebuildContext());

                int processedZ = 0;
                var gpuActiveBlocks = new System.Collections.Concurrent.ConcurrentBag<Vector3i>();

                Parallel.For(0, gridZ, new ParallelOptions { CancellationToken = ct }, z =>
                {
                    for (int y = 0; y < gridY; y++)
                    {
                        for (int x = 0; x < gridX; x++)
                        {
                            int flatIdx = x + (y * gridX) + (z * gridX * gridY);
                            if (flatResults[flatIdx] != BlueprintMesh.NoContent)
                            {
                                var cell = new Vector3i(x, y, z);
                                bmp[cell] = flatResults[flatIdx];
                                gpuActiveBlocks.Add(cell);
                            }
                        }
                    }

                    int currentZ = Interlocked.Increment(ref processedZ);
                    if (currentZ % 10 == 0)
                    {
                        progress?.Report((0.70 + (0.30 * ((double)currentZ / gridZ)), "Grid Reconstruction..."));
                    }
                });

                activeBlocks = gpuActiveBlocks;
            }
            catch (Exception ex)
            {
                throw new Exception("The GPU driver failed during allocation or kernel execution. Out of VRAM or driver error.", ex);
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"\nExecuting the model to blueprint conversion on the CPU\n");
            int tIndex = 0;

            foreach (var triangle in this.Mesh.EnumerateTriangles())
            {
                ct.ThrowIfCancellationRequested();
                if (tIndex % 1000 == 0) progress?.Report((0.7 * ((double)tIndex / triangleCount), "Triangle Evaluation..."));
                tIndex++;

                var triBox = triangle.ToBox();
                foreach (var cell in Enumerators.BoxRange(triBox, indexer))
                {
                    var cellBox = indexer.ToBox(cell);
                    if (cellBox.IntersectWithTriangle(triangle) != IntersectResult.NoIntersection)
                    {
                        bmp[cell] = 0;
                    }
                }
            }

            var cpuActiveBlocks = new List<Vector3i>();
            foreach (var g in bmp.Indices())
            {
                if (bmp[g] == 0)
                {
                    cpuActiveBlocks.Add(g);
                }
            }

            activeBlocks = cpuActiveBlocks;

            progress?.Report((0.70, "Slope Evaluation..."));
            int totalSlopePasses = (settings.SlopesUpper ? 4 : 0) + (settings.SlopesLower ? 4 : 0) + (settings.SlopesSides ? 4 : 0);
            int executedPasses = 0;

            if (settings.SlopesUpper) { ExecCpuSlopes(1); ExecCpuSlopes(2); ExecCpuSlopes(3); ExecCpuSlopes(4); }
            if (settings.SlopesLower) { ExecCpuSlopes(5); ExecCpuSlopes(6); ExecCpuSlopes(7); ExecCpuSlopes(8); }
            if (settings.SlopesSides) { ExecCpuSlopes(9); ExecCpuSlopes(10); ExecCpuSlopes(11); ExecCpuSlopes(12); }

            void ExecCpuSlopes(int content)
            {
                var shapeInfo = blueprint.Shapes[content];
                var probeDirectionA = -Base6Directions.Vectors[shapeInfo.Forward];
                var probeDirectionB = Base6Directions.Vectors[shapeInfo.Up];
                var supportDirectionA = -probeDirectionA;
                var supportDirectionB = -probeDirectionB;

                Parallel.ForEach(activeBlocks, new ParallelOptions { CancellationToken = ct }, g =>
                {
                    if (blueprint[g] != 0) return;

                    if (blueprint[g + probeDirectionA] != BlueprintMesh.NoContent || blueprint[g + probeDirectionB] != BlueprintMesh.NoContent)
                    {
                        return;
                    }

                    if (settings.SlopesMustBeSupported)
                    {
                        if (blueprint[g + supportDirectionA] != 0 || blueprint[g + supportDirectionB] != 0)
                        {
                            return;
                        }
                    }

                    bmp[g] = content;
                });

                if (totalSlopePasses > 0)
                {
                    int currentPass = Interlocked.Increment(ref executedPasses);
                    progress?.Report((0.70 + (0.30 * ((double)currentPass / totalSlopePasses)), "Slope Evaluation..."));
                }
            }
        }

        // Forcing the .NET runtime to compact the LOH and free up memory after the voxelization and slope generation, watch this space in case it hurts performance
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        progress?.Report((1.0, "Complete, finalization!"));
        return blueprint;
    }


    // ==========================================
    // PHASE 4: CPU MESHING & FINALIZATION 
    // (Handles VoxelSurfaceGenerator, Marching Cubes, and Text Export)
    // ==========================================
    public class GridMesher
    {
        public static DMesh3 Mesh(BlueprintMesh blueprint)
        {
            var grid = blueprint.Blocks;

            var cubes = new Bitmap3(new(grid.ni, grid.nj, grid.nk));
            foreach (var g in grid.Indices())
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
                        (Vector3f)blueprint.Coords.ToBox(g).Center
                    )
                );
            }

            var cubesSurfaceGenerator = new VoxelSurfaceGenerator();
            cubesSurfaceGenerator.Voxels = cubes;
            cubesSurfaceGenerator.Generate();

            var cubesMesh = cubesSurfaceGenerator.Meshes[0];
            MeshTransforms.Scale(cubesMesh, blueprint.Coords.CellSize);

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
            CubicShape("2eacbbf2-d8fb-4a78-91dc-7b492517ef97", x => x.AppendBox(Dims(LargeBlockSize))),

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
            CubicShape("632d7385-12b9-47a6-802a-a610d0cbd1e0", x => x.AppendBox(Dims(MidBlockSize))),

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
                        (Vector3f)baseRight.Cross(baseForward),
                        (Vector3f)baseRight,
                        (Vector3f)baseForward,
                        bRows: false
                    )
                );
            }

            static Vector3i BlockOffset(AxisAlignedBox3i blockSize, Matrix3f blockOrientation)
            {
                var offsetNegative = (Vector3f)blockSize.Min;
                var offsetPositive = (Vector3f)blockSize.Max;

                var a = ToInt(blockOrientation.Multiply(ref offsetNegative));
                var b = ToInt(blockOrientation.Multiply(ref offsetPositive));

                var minI = new Vector3i(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
                return blockSize.Min - minI;
            }

            static Vector3i ToInt(Vector3d vec)
            {
                return new
                (
                    (int)Math.Round(vec.x),
                    (int)Math.Round(vec.y),
                    (int)Math.Round(vec.z)
                );
            }
        }
    }

    public static class VoxelizationKernel
    {
        private static int Min(int a, int b) => a < b ? a : b;
        private static int Max(int a, int b) => a > b ? a : b;
        private static float Min(float a, float b) => a < b ? a : b;
        private static float Max(float a, float b) => a > b ? a : b;
        private static float Abs(float v) => v < 0f ? -v : v;
        private static float Min3(float a, float b, float c) => Min(a, Min(b, c));
        private static float Max3(float a, float b, float c) => Max(a, Max(b, c));
        private static int Floor(float val) => val < 0f ? (int)val - 1 : (int)val;
        private static int Ceiling(float val) => val > (int)val ? (int)val + 1 : (int)val;

        public static void InitializeGrid(Index1D index, ArrayView<int> grid, int value)
        {
            grid[index] = value;
        }

        public static void Voxelize(
            Index1D index,
            ArrayView<GpuTriangle> triangles,
            ArrayView<int> voxelGrid,
            Float3 gridOrigin,
            float cellSize,
            int gridX,
            int gridY,
            int gridZ)
        {
            var tri = triangles[index];

            int minX = Max(0, Floor((tri.MinBounds.X - gridOrigin.X) / cellSize));
            int minY = Max(0, Floor((tri.MinBounds.Y - gridOrigin.Y) / cellSize));
            int minZ = Max(0, Floor((tri.MinBounds.Z - gridOrigin.Z) / cellSize));

            int maxX = Min(gridX - 1, Ceiling((tri.MaxBounds.X - gridOrigin.X) / cellSize));
            int maxY = Min(gridY - 1, Ceiling((tri.MaxBounds.Y - gridOrigin.Y) / cellSize));
            int maxZ = Min(gridZ - 1, Ceiling((tri.MaxBounds.Z - gridOrigin.Z) / cellSize));

            float e0X = tri.V1.X - tri.V0.X; float e0Y = tri.V1.Y - tri.V0.Y; float e0Z = tri.V1.Z - tri.V0.Z;
            float e1X = tri.V2.X - tri.V1.X; float e1Y = tri.V2.Y - tri.V1.Y; float e1Z = tri.V2.Z - tri.V1.Z;
            float e2X = tri.V0.X - tri.V2.X; float e2Y = tri.V0.Y - tri.V2.Y; float e2Z = tri.V0.Z - tri.V2.Z;

            float normalX = e0Y * e1Z - e0Z * e1Y;
            float normalY = e0Z * e1X - e0X * e1Z;
            float normalZ = e0X * e1Y - e0Y * e1X;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Float3 cellCenter = new Float3(
                            gridOrigin.X + (x + 0.5f) * cellSize,
                            gridOrigin.Y + (y + 0.5f) * cellSize,
                            gridOrigin.Z + (z + 0.5f) * cellSize
                        );

                        if (CheckTriangleBoxIntersection(tri, cellCenter, cellSize,
                            e0X, e0Y, e0Z, e1X, e1Y, e1Z, e2X, e2Y, e2Z, normalX, normalY, normalZ))
                        {
                            int flatIndex = x + (y * gridX) + (z * gridX * gridY);

                            if (voxelGrid[flatIndex] != 0)
                            {
                                Atomic.Exchange(ref voxelGrid[flatIndex], 0);
                            }
                        }
                    }
                }
            }
        }

        public static void GenerateSlopes(
            Index1D index,
            ArrayView<int> voxelGrid,
            GpuSlopeParams p)
        {
            int x = index % p.GridX;
            int y = (index / p.GridX) % p.GridY;
            int z = index / (p.GridX * p.GridY);

            if (voxelGrid[index] != 0) return;

            int nxA = x + p.PAx; int nyA = y + p.PAy; int nzA = z + p.PAz;
            int nxB = x + p.PBx; int nyB = y + p.PBy; int nzB = z + p.PBz;

            if (nxA < 0 || nxA >= p.GridX || nyA < 0 || nyA >= p.GridY || nzA < 0 || nzA >= p.GridZ) return;
            if (nxB < 0 || nxB >= p.GridX || nyB < 0 || nyB >= p.GridY || nzB < 0 || nzB >= p.GridZ) return;

            int idxA = nxA + (nyA * p.GridX) + (nzA * p.GridX * p.GridY);
            int idxB = nxB + (nyB * p.GridX) + (nzB * p.GridX * p.GridY);

            if (voxelGrid[idxA] != int.MaxValue || voxelGrid[idxB] != int.MaxValue)
                return;

            if (p.MustBeSupported == 1)
            {
                int sxA = x + p.SAx; int syA = y + p.SAy; int szA = z + p.SAz;
                int sxB = x + p.SBx; int syB = y + p.SBy; int szB = z + p.SBz;

                if (sxA < 0 || sxA >= p.GridX || syA < 0 || syA >= p.GridY || szA < 0 || szA >= p.GridZ) return;
                if (sxB < 0 || sxB >= p.GridX || syB < 0 || syB >= p.GridY || szB < 0 || szB >= p.GridZ) return;

                int sIdxA = sxA + (syA * p.GridX) + (szA * p.GridX * p.GridY);
                int sIdxB = sxB + (syB * p.GridX) + (szB * p.GridX * p.GridY);

                if (voxelGrid[sIdxA] != 0 || voxelGrid[sIdxB] != 0)
                    return;
            }

            Atomic.Exchange(ref voxelGrid[index], p.Content);
        }

        private static bool CheckTriangleBoxIntersection(
            GpuTriangle tri, Float3 boxCenter, float cellSize,
            float e0X, float e0Y, float e0Z,
            float e1X, float e1Y, float e1Z,
            float e2X, float e2Y, float e2Z,
            float normalX, float normalY, float normalZ)
        {
            float boxHalf = cellSize * 0.5f;

            float v0X = tri.V0.X - boxCenter.X; float v0Y = tri.V0.Y - boxCenter.Y; float v0Z = tri.V0.Z - boxCenter.Z;
            float v1X = tri.V1.X - boxCenter.X; float v1Y = tri.V1.Y - boxCenter.Y; float v1Z = tri.V1.Z - boxCenter.Z;
            float v2X = tri.V2.X - boxCenter.X; float v2Y = tri.V2.Y - boxCenter.Y; float v2Z = tri.V2.Z - boxCenter.Z;

            if (Min3(v0X, v1X, v2X) > boxHalf || Max3(v0X, v1X, v2X) < -boxHalf) return false;
            if (Min3(v0Y, v1Y, v2Y) > boxHalf || Max3(v0Y, v1Y, v2Y) < -boxHalf) return false;
            if (Min3(v0Z, v1Z, v2Z) > boxHalf || Max3(v0Z, v1Z, v2Z) < -boxHalf) return false;

            float d = -(normalX * v0X + normalY * v0Y + normalZ * v0Z);

            float vminX = normalX > 0f ? -boxHalf : boxHalf; float vmaxX = normalX > 0f ? boxHalf : -boxHalf;
            float vminY = normalY > 0f ? -boxHalf : boxHalf; float vmaxY = normalY > 0f ? boxHalf : -boxHalf;
            float vminZ = normalZ > 0f ? -boxHalf : boxHalf; float vmaxZ = normalZ > 0f ? boxHalf : -boxHalf;

            if ((normalX * vminX + normalY * vminY + normalZ * vminZ) + d > 0f) return false;
            if ((normalX * vmaxX + normalY * vmaxY + normalZ * vmaxZ) + d < 0f) return false;

            if (!AxisTest(e0Z, -e0Y, v0Y, v0Z, v2Y, v2Z, boxHalf)) return false;
            if (!AxisTest(e1Z, -e1Y, v1Y, v1Z, v0Y, v0Z, boxHalf)) return false;
            if (!AxisTest(e2Z, -e2Y, v2Y, v2Z, v1Y, v1Z, boxHalf)) return false;

            if (!AxisTest(-e0Z, e0X, v0X, v0Z, v2X, v2Z, boxHalf)) return false;
            if (!AxisTest(-e1Z, e1X, v1X, v1Z, v0X, v0Z, boxHalf)) return false;
            if (!AxisTest(-e2Z, e2X, v2X, v2Z, v1X, v1Z, boxHalf)) return false;

            if (!AxisTest(e0Y, -e0X, v0X, v0Y, v2X, v2Y, boxHalf)) return false;
            if (!AxisTest(e1Y, -e1X, v1X, v1Y, v0X, v0Y, boxHalf)) return false;
            if (!AxisTest(e2Y, -e2X, v2X, v2Y, v1X, v1Y, boxHalf)) return false;

            return true;
        }

        private static bool AxisTest(float a, float b, float fa, float fb, float va, float vb, float boxHalf)
        {
            float p0 = a * fa + b * fb;
            float p2 = a * va + b * vb;
            float min = Min(p0, p2);
            float max = Max(p0, p2);
            float rad = (Abs(a) + Abs(b)) * boxHalf;
            return !(min > rad || max < -rad);
        }
    }
}