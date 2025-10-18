using Assimp;
using g4;
using Microsoft.Win32;
using SpaceEditor.Algorithms;
using SpaceEditor.Data;
using SpaceEditor.Rocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SpaceEditor.Controls;

public record ModelSettings
{
    public BlueprintGenerator Screen;

    [ButtonProperty(nameof(SwapXYImpl))]
    public bool SwapXY { get; set; }

    [ButtonProperty(nameof(SwapXZImpl))]
    public bool SwapXZ { get; set; }

    [ButtonProperty(nameof(SwapYZImpl))]
    public bool SwapYZ { get; set; }

    [ButtonProperty(nameof(RecenterImp))]
    public bool Recenter { get; set; }
    
    public float ModelSize { get; set; }

    [ButtonProperty(nameof(ScareToTargetSize))]
    public bool ApplyScale { get; set; }

    private void SwapXYImpl() => Swap(0, 1);
    private void SwapXZImpl() => Swap(0, 2);
    private void SwapYZImpl() => Swap(1, 2);

    private void RecenterImp()
    {
        UpdateModelCopy(model =>
        {
            var bb = model.CachedBounds;
            var offset = -bb.Center;

            MeshTransforms.Translate(model, offset);
        });
    }

    private void ScareToTargetSize()
    {
        UpdateModelCopy(model =>
        {
            var bb = model.CachedBounds;
            var dimensions = bb.Extents * 2;
            
            var size = dimensions.MaxAbs;
            var targetSize = Math.Max(this.ModelSize, 1);

            MeshTransforms.Scale
            (
                model, 
                new(targetSize / size),
                bb.Center
            );
        });
    }

    private void Swap(int first, int second)
    {
        UpdateModelCopy(model =>
        {
            foreach (var id in model.VertexIndices())
            {
                var vertex = model.GetVertexAll(id);
                (vertex.v[first], vertex.v[second]) = (vertex.v[second], vertex.v[first]);
                model.SetVertex(id, vertex.v);

                if (vertex.bHaveN)
                {
                    (vertex.n[first], vertex.n[second]) = (vertex.n[second], vertex.n[first]);
                    model.SetVertexNormal(id, vertex.n);
                }
            }

            foreach (var id in model.TriangleIndices())
            {
                var tri = model.GetTriangle(id);
                (tri.a, tri.c) = (tri.c, tri.a);
                model.SetTriangle(id, tri, bRemoveIsolatedVertices: false);
            }
        });
    }

    private void UpdateModelCopy(Action<DMesh3> update)
    {
        var model = this.Screen.Model;
        if (model is null)
            return;

        model = new DMesh3(model);
        update(model);

        this.Screen.SetNewModel(model);
    }
}

/// <summary>
/// Interaction logic for BlueprintGenerator.xaml
/// </summary>
public partial class BlueprintGenerator : UserControl
{
    public DMesh3? Model;
    public AsyncLazy<DMeshAABBTree3>? ModelBVH;
    public CancellationTokenSource? ModelLifetime;
        
    public CancellationTokenSource? GeneratorLifetime;

    private ModelSettings ModelSettingsVM
    {
        get => (ModelSettings) this.ModelSettings.ReflectedInstance;
        set
        {
            // Make sure to reload the new values
            this.ModelSettings.ReflectedInstance = null!;
            
            this.ModelSettings.ReflectedInstance = value;
        }
    }

    public BlueprintGenerator()
    {
        InitializeComponent();
        this.GeneratorSettings.ReflectedInstance = new GridShaper.GeneratorSettings();
        this.ModelSettingsVM = new ModelSettings
        {
            Screen = this
        };
    }

    private void SelectModel(object sender, RoutedEventArgs e)
    {
        var selectFile = new OpenFileDialog();
        if (selectFile.ShowDialog(Window.GetWindow(this)) != true)
            return;

        DMesh3? model = null;
        try
        {
            model = LoadModel(selectFile.FileName);
            this.BlueprintName.Text = Path.GetFileNameWithoutExtension(selectFile.FileName);

            

        }
        catch
        {
        }

        SetNewModel(model);
    }

    public void SetNewModel(DMesh3? model)
    {
        var lifetime = ResetLifetime(ref this.ModelLifetime);
        this.Model = model;

        if (this.Model is null)
            return;

        this.ModelBVH = new(() =>
        {
            lifetime.ThrowIfCancellationRequested();

            var tree = new DMeshAABBTree3(this.Model);
            tree.Build();
            return tree;
        });

        this.ModelBVH.Poke();

        var bb = this.Model.CachedBounds;
        this.ModelSettingsVM = this.ModelSettingsVM with { ModelSize = (float) bb.Extents.MaxAbs * 2 };

        var modelInfo = new StringBuilder();
        modelInfo.AppendLine($"Model: {this.BlueprintName.Text}");
        modelInfo.AppendLine($"Size: {bb.Width:f2} x {bb.Height:f2} x {bb.Depth:f2}m");

        var modelSize = Math.Max(bb.Min.MaxAbs, bb.Max.MaxAbs) * 2;
        if (modelSize > 3000)
        {
            modelInfo.AppendLine("Mode is too larget to safely convert.");
            modelInfo.AppendLine("Scale it down.");
        }
        else if (modelSize > 500)
        {
            modelInfo.AppendLine("Mode is quite large, performance issues may appear.");
            modelInfo.AppendLine("Recommended to scale it down.");
        }

        this.ModelDetails.Text = modelInfo.ToString().Trim();
        lifetime.Register(() =>
        {
            this.ModelDetails.Text = null;
        });

        var modelRender = CreateRenderModel(this.Model, controlsRow: 0);
        lifetime.Register(() =>
        {
            this.GenerateBlueprintPanel.Visibility = Visibility.Hidden;
            modelRender.Dispose();
        });

        this.ModelsViewport.ScaleToFitCurrentModels();
        this.GenerateBlueprintPanel.Visibility = Visibility.Visible;
    }

    private async void GenerateBlueprint(object sender, RoutedEventArgs e)
    {
        try
        {
            var lifetime = ResetLifetime(ref this.GeneratorLifetime, this.ModelLifetime);

            var model = this.Model;
            if (model is null)
                return;
                
            var tree = await this.ModelBVH!.Value;
                
            var shaper = new GridShaper(model, tree);
            var blueprint = shaper.Generate
            (
                (GridShaper.GeneratorSettings) this.GeneratorSettings.ReflectedInstance,
                lifetime
            );

            var gridMesh = GridMesher.Mesh(blueprint);
            var modelRender = CreateRenderModel(gridMesh, controlsRow: 1);
            lifetime.Register(() =>
            {
                modelRender.Dispose();
                this.ExportBlueprintPanel.Visibility = Visibility.Collapsed;
            });

            this.ExportBlueprintPanel.Tag = blueprint;
            this.ExportBlueprintPanel.Visibility = Visibility.Visible;

            IEnumerable<Vector3i> usedIndicies = blueprint.Blocks.Indices().Where(x => blueprint[x] != BlueprintMesh.NoContent);

            Vector3i diemensions = GetBlueprintDiemensions(usedIndicies);

            string info = $"Blocks: X: {diemensions.x} Y: {diemensions.y} Z: {diemensions.z} Total: {usedIndicies.Count()}";
			this.BlueprintDetails.Text = info;

            lifetime.Register(() =>
            {
                this.BlueprintDetails.Text = null;
            });
        }
        catch
        {
                
        }
    }

    public static Vector3i GetBlueprintDiemensions(IEnumerable<Vector3i> indicies)
    {
		Vector2i x = new(int.MaxValue, int.MinValue);
		Vector2i y = new(int.MaxValue, int.MinValue);
		Vector2i z = new(int.MaxValue, int.MinValue);

		foreach (Vector3i v3i in indicies)
		{
			if (v3i.x < x.x)
				x.x = v3i.x;
			else if (v3i.x > x.y)
				x.y = v3i.x;

			if (v3i.y < y.x)
				y.x = v3i.y;
			else if (v3i.y > y.y)
				y.y = v3i.y;

			if (v3i.z < z.x)
				z.x = v3i.z;
			else if (v3i.z > z.y)
				z.y = v3i.z;
		}

        return new Vector3i(x.y - x.x, y.y - y.x, z.y - z.x);
    }
        
    private void ExportBlueprint(object sender, RoutedEventArgs e)
    {
        var blueprint = (BlueprintMesh)this.ExportBlueprintPanel.Tag;
        var writer = new BlueprintWriter
        {
            WriteFolder = GameFacts.GetBlueprintImportsPath()
        };

        var name = this.BlueprintName.Text;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "No Name";
            this.BlueprintName.Text = name;
        }

        writer.Write(blueprint, name);
    }

    private void ExportBlueprintAsModel(object sender, RoutedEventArgs e)
    {
        var blueprint = (BlueprintMesh)this.ExportBlueprintPanel.Tag;

        var saveLocation = new SaveFileDialog();
        saveLocation.DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        saveLocation.Filter = "Model (*.obj)|*.obj";
        saveLocation.AddExtension = true;
        saveLocation.DefaultExt = "obj";
        saveLocation.FileName = this.BlueprintName.Text;
        
        if (saveLocation.ShowDialog() != true)
            return;
        
        var mesh = GridMesher.Mesh(blueprint);
        Util.WriteDebugMesh(mesh, saveLocation.FileName);
    }

    private DMesh3 LoadModel(string path)
    {
        using var assimp = new AssimpContext();
        var scene = assimp.ImportFile(path);

        var mesh = new DMesh3(bWantNormals: true, bWantUVs: true, bWantColors: false);

        foreach (var m in scene.Meshes)
        {
            var vertexIndices = new List<int>();
                
            var vertices = m.Vertices;
            var normals = m.HasNormals ? m.Normals : null;
            var uvs = m.HasTextureCoords(0) ? m.TextureCoordinateChannels[0] : null;
            for (int i = 0; i < vertices.Count; i++)
            {
                NewVertexInfo vertex = default;
                vertex.v.x = vertices[i].X;
                vertex.v.y = vertices[i].Y;
                vertex.v.z = vertices[i].Z;

                if (normals is not null)
                {
                    vertex.bHaveN = true;
                    vertex.n.x = normals[i].X;
                    vertex.n.y = normals[i].Y;
                    vertex.n.z = normals[i].Z;
                }

                if (uvs is not null)
                {
                    vertex.bHaveUV = true;
                    vertex.uv.x = uvs[i].X;
                    vertex.uv.y = 1 - uvs[i].Y; //TODO: Why flip?
                }

                vertexIndices.Add(mesh.AppendVertex(vertex));
            }

            foreach (var f in m.Faces)
            {
                var polygon = f.Indices;
                mesh.AppendTriangle
                (
                    vertexIndices[polygon[0]],
                    vertexIndices[polygon[1]],
                    vertexIndices[polygon[2]]
                );

                if (f.IndexCount == 4)
                {
                    mesh.AppendTriangle
                    (
                        vertexIndices[polygon[0]],
                        vertexIndices[polygon[2]],
                        vertexIndices[polygon[3]]
                    );
                }
            }
        }

        return mesh;
    }

    private CancellationToken ResetLifetime(ref CancellationTokenSource? cts, CancellationTokenSource? parent = null)
    {
        cts?.Cancel();
        cts?.Dispose();

        if (parent is not null)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(parent.Token);
        }
        else
        {
            cts = new();
        }

        return cts.Token;
    }

    private IDisposable CreateRenderModel(DMesh3 model, int controlsRow)
    {
        var controls = this.ViewportControls.Children.OfType<FrameworkElement>().Where(x => Grid.GetRow(x) == controlsRow).ToArray();

        var draw = (CheckBox) controls[1];
        var xray = (CheckBox) controls[2];
        var color = (ColourSlider) controls[3];

        var renderData = new ModelViewport.ModelData
        {
            Mesh = model,
        };
            
        RoutedEventHandler onCheckedChanged = (_, _) =>
        {
            renderData.Color = color.SelectedColour;
            renderData.Opacity = xray.IsChecked == true ? 0.5f : 1f;

            if (draw.IsChecked == false)
            {
                renderData.Opacity = 0;
            }
        };
        onCheckedChanged(default, default);

        xray.Checked += onCheckedChanged;
        xray.Unchecked += onCheckedChanged;
        draw.Checked += onCheckedChanged;
        draw.Unchecked += onCheckedChanged;
        color.ColorChanged += onCheckedChanged;

        var modelRender = this.ModelsViewport.AddModel(renderData);
        return Disposable.Create(() =>
        {
            xray.Checked -= onCheckedChanged;
            xray.Unchecked -= onCheckedChanged;
            draw.Checked -= onCheckedChanged;
            draw.Unchecked -= onCheckedChanged;
            color.ColorChanged -= onCheckedChanged;
            modelRender.Dispose();
        });
    }

}