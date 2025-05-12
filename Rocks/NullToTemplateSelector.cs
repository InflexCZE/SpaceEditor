using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;

namespace SpaceEditor.Rocks;

public class NullToTemplateSelector : DataTemplateSelector
{
    public DataTemplate NullTemplate { get; set; } = null!;
    public DataTemplate NotNullTemplate { get; set; } = null!;

    public override DataTemplate SelectTemplate(object? item, DependencyObject container)
    {
        return item is null ? this.NullTemplate : this.NotNullTemplate;
    }
}