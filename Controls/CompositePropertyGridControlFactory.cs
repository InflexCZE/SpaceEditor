using PropertyTools.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SpaceEditor.Algorithms;

namespace SpaceEditor.Controls;

public interface IControlFactory
{
    FrameworkElement? TryCreateControl(PropertyItem property, PropertyControlFactoryOptions options);
}

public class CompositePropertyGridControlFactory : PropertyGridControlFactory, IControlFactory
{
    public readonly List<IControlFactory> Factories = new();

    public override FrameworkElement CreateControl(PropertyItem property, PropertyControlFactoryOptions options)
    {
        return TryCreateControl(property, options) ?? base.CreateControl(property, options);
    }

    public virtual FrameworkElement? TryCreateControl(PropertyItem property, PropertyControlFactoryOptions options)
    {
        if (property.Descriptor.ComponentType == typeof(GridShaper.GeneratorSettings)
            && property.Descriptor.Name == nameof(GridShaper.GeneratorSettings.BlockSize))
        {
            var sizes = new ComboBox
            {
                ItemsSource = new[] { GridShaper.BlockSizes.TwoPointFive, GridShaper.BlockSizes.HalfMeter },
                ItemTemplate = LocalizedNameConverter.Template("", "BlockSize")
            };
            sizes.SetBinding(ComboBox.SelectedItemProperty, property.CreateBinding(UpdateSourceTrigger.PropertyChanged));
            return sizes;
        }

        foreach(var factory in this.Factories)
        {
            if (factory.TryCreateControl(property, options) is {} control)
            {
                return control;
            }
        }

        return null;
    }
}
