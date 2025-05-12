using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SpaceEditor.Controls;

public partial class AsyncView : UserControl
{
    private CancellationTokenSource? Lifetime;

    public AsyncView()
    {
        this.DataContextChanged += OnDataContextChanged;
        this.Unloaded += OnUnloaded;
        
        InitializeComponent();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        this.Lifetime?.Cancel();
    }

    private async void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (this.Lifetime?.IsCancellationRequested == true)
        {
            // Control is already dead
            return;
        }

        this.Lifetime?.Cancel();
        this.Lifetime = new();
        
        var lifetime = this.Lifetime.Token;

        this.LoadingContent.Visibility = Visibility.Visible;
        this.MainContent.Visibility = Visibility.Collapsed;

        if (e.NewValue is Task data)
        {
            try
            {
                await data.WaitAsync(lifetime);
                
                this.LoadingContent.Visibility = Visibility.Collapsed;
                this.MainContent.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException)
            { }
        }
    }
}