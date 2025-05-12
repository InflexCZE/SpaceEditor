using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SpaceEditor.Rocks;

public static class ViewRocks
{
    public static T? TryFindParentOfType<T>(this FrameworkElement? current)
        where T : FrameworkElement
    {
        while (current is not null)
        {
            if (current is T found)
                return found;

            current = (FrameworkElement?) VisualTreeHelper.GetParent(current);

        }

        return null;
    }
}