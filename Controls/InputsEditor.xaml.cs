using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ReflectionMagic;
using SpaceEditor.Data;

namespace SpaceEditor.Controls;

/// <summary>
/// Interaction logic for InputsEditor.xaml
/// </summary>
public partial class InputsEditor : UserControl
{
    public PresetVM? VM => this.DataContext as PresetVM;

    public static readonly DependencyProperty InputActionsSearchStringProperty = DependencyProperty.Register
    (
        nameof(InputActionsSearchString),
        typeof(string),
        typeof(InputsEditor),
        new PropertyMetadata(string.Empty, OnInputActionsSearchStringChanged)
    );

    public string InputActionsSearchString
    {
        get { return (string)GetValue(InputActionsSearchStringProperty); }
        set { SetValue(InputActionsSearchStringProperty, value); }
    }

    public InputsEditor()
    {
        this.DataContextChanged += OnDataContextChanged;
        InitializeComponent();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        this.ActionList.ItemsSource = null;

        if (e.NewValue is PresetVM vm)
        {
            var cv = CollectionViewSource.GetDefaultView(vm.Actions.Actions.Select(x => x.Value).ToList());
            cv.Filter = x =>
            {
                var candidate = ((InputActions.InputActionInfo) x).DisplayName;
                return candidate.Contains(this.InputActionsSearchString, StringComparison.InvariantCultureIgnoreCase);
            };

            this.ActionList.ItemsSource = cv;
        }
    }

    private static void OnInputActionsSearchStringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cv = ((InputsEditor)d).ActionList.ItemsSource as ICollectionView;
        cv?.Refresh();
    }

    private void OnInputActionSelected(object sender, SelectionChangedEventArgs e)
    {
        var selected = (InputActions.InputActionInfo?) this.ActionList.SelectedValue;
        if (selected is null)
            goto Nothing;

        var bindings = this.VM?.TryGetBindings(selected.Id);
        if (bindings is null)
            goto Nothing;

        this.BindingsEditor.ReflectedItems = CollectionViewSource.GetDefaultView(bindings);
        object inputKind = DynamicHelper.Unwrap(selected.DefinitionObjectBuilder.AsDynamic().ExpectedInputType);

        var builders = this.VM?.InputIds.GetBuilderTypes(inputKind);
        if (builders is null)
            goto Nothing;

        this.BindingsEditor.NewItemTypeCandidates = builders.Select(x => KeyValuePair.Create(x.Name, x)).ToList();
        this.BindingsEditor.Visibility = Visibility.Visible;
        return;

        Nothing:
        this.BindingsEditor.DataContext = null;
        this.BindingsEditor.ReflectedItems = null;
        this.BindingsEditor.Visibility = Visibility.Hidden;
    }
}