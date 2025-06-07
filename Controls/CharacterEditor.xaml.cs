using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorPicker.Models;
using SpaceEditor.Data;
using SpaceEditor.Data.GameLinks;

namespace SpaceEditor.Controls;

/// <summary>
/// Interaction logic for CharacterEditor.xaml
/// </summary>
public partial class CharacterEditor : UserControl
{
    public Argb32[,] Albedo;
    public Argb32[,] Mask;
    public Argb32[,] OutputMemoryPool;
    public WriteableBitmap PreviewBitmap;

    // Non-null value signifies initialization is done
    public volatile NotifyableColor? Color;
    
    private readonly GameLink GameLink;

    private bool IsConnectedToGameImpl;
    private bool IsConnectedToGame
    {
        get => this.IsConnectedToGameImpl;
        set
        {
            this.IsConnectedToGameImpl = value;

            var a = this.ConnectButton;
            var b = this.DisconnectButton;

            if (value)
            {
                (b, a) = (a, b);
            }

            a.Visibility = Visibility.Visible;
            b.Visibility = Visibility.Collapsed;
        }
    }

    public CharacterEditor(GameProxy game, GameLink gameLink)
    {
        this.GameLink = gameLink;
        
        InitializeComponent();
        this.IsConnectedToGame = false;

        var c = this.ColorPicker.Color;
        c.RGB_R = 0x33;
        c.RGB_G = 0xff;
        c.RGB_B = 0x36;
        c.RaiseUpdateAllCompleted();

        this.SecondaryColor.SecondaryColor = System.Windows.Media.Color.FromRgb
        (
            0xff, 
            0x2a, 
            0x1f
        );

        Task.Run(() =>
        {
            this.Albedo = Read("Resources/CharacterEditor/Albedo.png");
            this.Mask = Read("Resources/CharacterEditor/Mask.png");
            this.OutputMemoryPool = (Argb32[,]) this.Albedo.Clone();

            var dpi = VisualTreeHelper.GetDpi(this);
            var previewBitmap = this.Dispatcher.Invoke(() =>
            {
                var previewBitmap = new WriteableBitmap
                (
                    this.Albedo.GetLength(1),
                    this.Albedo.GetLength(0),
                    dpi.PixelsPerInchX,
                    dpi.PixelsPerInchY,
                    PixelFormats.Bgra32,
                    null
                );
                
                Write(previewBitmap, this.Albedo);
                return previewBitmap;
            });

            this.PreviewBitmap = previewBitmap;

            this.Dispatcher.Invoke(() =>
            {
                this.Preview.Source = this.PreviewBitmap;

                this.Color = this.ColorPicker.Color;
                this.Color.UpdateAllCompleted += (_, _) =>
                {
                    UpdatePreview();
                };
                UpdatePreview();
            });
        });
    }

    private bool NeedsUpdate;
    private Task PreviousRun;
    private void UpdatePreview()
    {
        if (this.Color is null)
            return;

        this.NeedsUpdate = true;
        if (this.PreviousRun?.IsCompleted != false)
            this.PreviousRun = Impl();

        async Task Impl()
        {
            Run:
            this.NeedsUpdate = false;

            var suitColor = new Argb32
            {
                A = byte.MaxValue,
                R = (byte)(this.Color.RGB_R),
                G = (byte)(this.Color.RGB_G),
                B = (byte)(this.Color.RGB_B),
            };

            var bitmapCompute = Task.Run(() =>
            {
                var mask = this.Mask;
                var albedo = this.Albedo;
                var output = this.OutputMemoryPool;

                var w = albedo.GetLength(1);
                var h = albedo.GetLength(0);
                for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    var c1 = albedo[y, x];
                    var c2 = mask[y, x];

                    if (c2 with {A = default } != default)
                    {
                        c1 = MultiplyColors(c1, MultiplyColors(c2, suitColor));
                    }

                    output[y, x] = c1;
                }

                return output;
            });
            
            await SendToGameIfNeeded();

            Write(this.PreviewBitmap, await bitmapCompute);
            this.Preview.Source = this.PreviewBitmap;

            if (this.NeedsUpdate)
                goto Run;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Argb32 MultiplyColors(Argb32 c1, Argb32 c2)
    {
        return new()
        {
            B = (byte) (Math.Round((c1.R / 255.0) * (c2.R / 255.0) * 255.0)),
            G = (byte) (Math.Round((c1.G / 255.0) * (c2.G / 255.0) * 255.0)),
            R = (byte) (Math.Round((c1.B / 255.0) * (c2.B / 255.0) * 255.0)),
            A = (byte) (Math.Round((c1.A / 255.0) * (c2.A / 255.0) * 255.0)),
        };
    }

    public static Argb32[,] Read(string path)
    {
        BitmapSource bitmap = new BitmapImage(new Uri($"pack://application:,,,/{path}"));

        if (bitmap.Format != PixelFormats.Bgra32)
        {
            var convertedBitmap = new FormatConvertedBitmap();
            convertedBitmap.BeginInit();
            convertedBitmap.Source = bitmap;
            convertedBitmap.DestinationFormat = PixelFormats.Bgra32;
            convertedBitmap.EndInit();
            bitmap = convertedBitmap;
        }

        return Read(bitmap);
    }

    public static unsafe Argb32[,] Read(BitmapSource source)
    {
        var format = PixelFormats.Bgra32;
        if (source.Format != format)
        {
            throw new Exception("Format: " + source.Format);
        }

        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var result = new Argb32[height, width];
        for (int y = 0; y < height; y++)
        {
            fixed (Argb32* dstPtr = &result[y, 0])
            {
                var dst = MemoryMarshal.AsBytes(new Span<Argb32>(dstPtr, width));
                source.CopyPixels(new(0, y, width, 1), (IntPtr)dstPtr, dst.Length, dst.Length);
            }
        }

        return result;
    }

    public static unsafe void Write(WriteableBitmap target, Argb32[,] pixels)
    {
        var format = PixelFormats.Bgra32;
        if (target.Format != format)
        {
            throw new Exception("Format: " + target.Format);
        }

        var width = pixels.GetLength(1);
        var height = pixels.GetLength(0);

        target.Lock();
        try
        {
            for (int y = 0; y < height; y++)
            {
                fixed (Argb32* srcPtr = &pixels[y, 0])
                {
                    var src = MemoryMarshal.AsBytes(new Span<Argb32>(srcPtr, width));
                    target.WritePixels(new(0, y, width, 1), (IntPtr)srcPtr, src.Length, src.Length);
                }
            }
        }
        finally
        {
            target.Unlock();
        }
    }

    /// <summary>
    /// <see cref="PixelFormats.Bgr32"/>
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public record struct Argb32
    {
        public byte B;
        public byte G;
        public byte R;
        public byte A;
    }

    private Task LastConnectionTask = Task.CompletedTask;

    private async void ConnectToGame(object sender, RoutedEventArgs _)
    {
        if (this.LastConnectionTask.IsCompleted == false)
            return;

        var connectionTask = this.GameLink.TestConnection();
        this.LastConnectionTask = connectionTask;

        PrintStatus(success: "Testing connection ...");
        
        var button = (Button) sender;
        button.IsEnabled = false;
        
        Exception? connectionError = null;
        try
        {
            try
            {
                await connectionTask;
            }
            catch
            {
                // Don't care now, just want to get to finished state
            }

            if (ReferenceEquals(connectionTask, this.LastConnectionTask) == false)
                return;

            // Throw if anything is stored inside
            await connectionTask;
        }
        catch(Exception e)
        {
            connectionError = e;
        }
        finally
        {
            button.IsEnabled = true;
        }

        if (connectionError is null)
        {
            this.IsConnectedToGame = true;
            UpdatePreview();
        }

        PrintStatus(connectionError, "Connected successfully");
    }

    private async void DisconnectFromGame(object _, RoutedEventArgs __)
    {
        this.LastConnectionTask = Task.CompletedTask;
        this.IsConnectedToGame = false;
    }
    
    private async Task SendToGameIfNeeded()
    {
        if (this.Color is null)
            return;
        
        if (this.IsConnectedToGame == false)
            return;

        uint colorInt = ((uint) byte.MaxValue << 24) |
                        ((uint) (byte) (this.Color.RGB_R) << 16) |
                        ((uint) (byte) (this.Color.RGB_G) << 8) |
                        ((uint) (byte) (this.Color.RGB_B) << 0);

        var error = await Task.Run(async () =>
        {
            try
            {
                await this.GameLink.PaintCharacters(colorInt);
            }
            catch(Exception e)
            {
                return e;
            }

            return null;
        });

        PrintStatus(error, success: string.Empty);
    }

    private void PrintStatus(Exception? error = null, string? success = null)
    {
        var message = success;
        
        if (error is not null)
        {
            message = error.Message + 
                      Environment.NewLine + 
                      Environment.NewLine + 
                      error.ToString();
        }

        this.ErrorOutput.Text = message ?? string.Empty;
    }
}
