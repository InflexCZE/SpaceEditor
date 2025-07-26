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
using System.Windows;
using System.Windows.Controls;

namespace SpaceEditor.Controls;

public class ModelSettings
{
    public BlueprintGenerator Screen;

    [ButtonProperty(nameof(SwapXYImpl))]
    public bool SwapXY { get; set; }

    [ButtonProperty(nameof(SwapXZImpl))]
    public bool SwapXZ { get; set; }

    [ButtonProperty(nameof(SwapYZImpl))]
    public bool SwapYZ { get; set; }

    private void SwapXYImpl() => Swap(0, 1);
    private void SwapXZImpl() => Swap(0, 2);
    private void SwapYZImpl() => Swap(1, 2);

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

    public BlueprintGenerator()
    {
        InitializeComponent();
        this.GeneratorSettings.ReflectedInstance = new GridShaper.GeneratorSettings();
        this.ModelSettings.ReflectedInstance = new ModelSettings
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
        }
        catch
        {
                
        }
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