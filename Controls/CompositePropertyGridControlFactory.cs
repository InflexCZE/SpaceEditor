using PropertyTools.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

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