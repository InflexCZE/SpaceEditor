using PropertyTools.Wpf;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SpaceEditor.Controls;

public class ButtonPropertyAttribute(string invokeRoutine) : Attribute
{
    public string InvokeRoutine { get; } = invokeRoutine;
}

public class ButtonPropertyGridControlFactory : IControlFactory
{
    public FrameworkElement? TryCreateControl(PropertyItem property, PropertyControlFactoryOptions options)
    {
        if (property.Descriptor.GetFirstAttributeOrDefault<ButtonPropertyAttribute>() is not {} target)
            return null;

        var button = new Button();
        button.Content = property.DisplayName;
        button.SetBinding(FrameworkElement.TagProperty, property.CreateBinding());
        button.Click += (_, _) =>
        {
            var binding = BindingOperations.GetBindingExpression(button, FrameworkElement.TagProperty);

            var vm = binding?.DataItem;
            if (vm is null)
                return;

            var method = vm.GetType().GetMethod(target.InvokeRoutine, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            method!.Invoke(vm, null);
        };

        return button;
    }
}