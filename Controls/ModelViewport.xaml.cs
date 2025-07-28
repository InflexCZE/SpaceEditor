using g4;
using SpaceEditor.Data;
using SpaceEditor.Rocks;
using System;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SpaceEditor.Controls;

/// <summary>
/// Interaction logic for ModelViewport.xaml
/// </summary>
public partial class ModelViewport : UserControl
{
    public ModelViewport()
    {
        InitializeComponent();
    }

    public class ModelData : VM
    {
        public DMesh3 Mesh { get; init; }

        private Color ColorImpl = Colors.Gray;
        public Color Color
        {
            get => this.ColorImpl;
            set => SetField(ref this.ColorImpl, value);
        }
        
        public float Opacity
        {
            get => (float) this.Color.A / byte.MaxValue;
            set => this.Color = this.Color with {A = (byte)(value * byte.MaxValue) };
        }
    }

    public IDisposable AddModel(ModelData model)
    {
        var mesh = new MeshGeometry3D();

        var m = model.Mesh;
        mesh.Positions = new
        (
            m.VertexIndices().Select(x =>
            {
                var v = m.GetVertex(x);
                return new Point3D(v.x, v.y, v.z);
            })
        );

        mesh.TriangleIndices = new
        (
            m.Triangles().SelectMany(t =>
            {
                return new[]{ t.a, t.b, t.c };
            })
        );

        if (m.HasVertexNormals)
        {
            mesh.Normals = new
            (
                m.VertexIndices().Select(x =>
                {
                    var n = m.GetVertexNormal(x);
                    return new Vector3D(n.x, n.y, n.z);
                })
            );
        }

        var geometry = new GeometryModel3D
        {
            Geometry = mesh,
            Transform = Transform3D.Identity
        };
        
        var renderModel = new ModelVisual3D
        {
            Content = geometry
        };

        PropertyChangedEventHandler onPropertyChanged = (_, _) =>
        {
            if (model.Opacity == 0)
            {
                geometry.Material = null;
            }
            else
            {
                geometry.Material = new DiffuseMaterial
                (
                    new SolidColorBrush
                    (
                        model.Color
                    )
                );
            }
        };
        model.PropertyChanged += onPropertyChanged;
        onPropertyChanged(default, default);

        this.ViewPort.Children.Add(renderModel);

        return Disposable.Create(() =>
        {
            this.ViewPort.Children.Remove(renderModel);
            model.PropertyChanged -= onPropertyChanged;
        });
    }

    public void ScaleToFitCurrentModels()
    {
        Rect3D bounds = new(default, new(10, 10, 10));
        foreach (var model in this.ViewPort.Children.OfType<ModelVisual3D>())
        {
            bounds.Union(model.Content.Bounds);
        }

        var maxSide = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
        SetCameraParameters(new Point3D(0, 0, maxSide * 1.3f));
    }

    private Point LastMousePosition;
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(this.ViewPort);
        var diff = this.LastMousePosition - position;
        this.LastMousePosition = position;

        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var cameraPosition = this.Camera.Position;
        UpdateObitCamera(default, ref cameraPosition, new(-(float) diff.X, (float) -diff.Y), 0.03f);
        SetCameraParameters(cameraPosition);
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var d = 1 - (e.Delta * 0.001f);
        var p = this.Camera.Position;
        p.X *= d;
        p.Y *= d;
        p.Z *= d;
        this.Camera.Position = p;
    }

    void SetCameraParameters(Point3D position, Point3D center = default)
    {
        this.Camera.Position = position;

        var lookDirection = center - position;
        this.Camera.LookDirection = new(lookDirection.X, lookDirection.Y, lookDirection.Z);
    }

    void UpdateObitCamera(Point3D center, ref Point3D position, Vector2 mouseInput, float mouseSpeed)
    {
        // Convert position to vector from center to camera
        var offset = position - center;

        // Convert offset to spherical coordinates
        var radius = offset.Length;
        var theta = Math.Atan2(offset.Z, offset.X); // horizontal angle
        var phi = Math.Acos(offset.Y / radius);     // vertical angle

        // Apply mouse input to angles
        theta += mouseInput.X * mouseSpeed;
        phi -= mouseInput.Y * mouseSpeed;

        // Clamp phi to avoid pole locking
        float epsilon = 0.001f;
        phi = Math.Clamp(phi, epsilon, MathF.PI - epsilon);

        // Convert spherical back to Cartesian coordinates
        offset.X = radius * Math.Sin(phi) * Math.Cos(theta);
        offset.Y = radius * Math.Cos(phi);
        offset.Z = radius * Math.Sin(phi) * Math.Sin(theta);

        // Update camera position
        position = center + offset;
    }
}