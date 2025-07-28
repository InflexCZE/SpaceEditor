using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PropertyTools.Wpf;
using SpaceEditor.Data;

namespace SpaceEditor.Controls;

public class InputIdControlsFactory : IControlFactory
{
    public InputIds InputIds { get; init; } = null!;

    public FrameworkElement? TryCreateControl(PropertyItem property, PropertyControlFactoryOptions options)
    {
        var type = property.ActualPropertyType;
        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            var rc = new ReflectedCollection();
            rc.SetBinding(ReflectedCollection.ReflectedItemsProperty, property.CreateBinding(UpdateSourceTrigger.PropertyChanged));
            rc.NewItemTypeCandidates = this.InputIds.AnalogBuilders.Concat(this.InputIds.DigitalBuilders).Select(x => KeyValuePair.Create(x.Name, x)).ToList();
            return rc;
        }

        var typeName = type.Name;
        if (typeName.Contains("ControlBuilder"))
        {
            var ro = new ReflectedObject();
            ro.NewObjectTypeCandidates = this.InputIds.GetBuilderTypes(type).Select(x => KeyValuePair.Create(x.Name, x)).ToList();
            ro.SetBinding(ReflectedObject.ReflectedInstanceProperty, property.CreateBinding(UpdateSourceTrigger.PropertyChanged));
            return ro;
        }

        if (typeName is "InputId")
        {
            var inputIds = property.Descriptor.ComponentType.Name switch
            {
                {} s when s.StartsWith("Digital") => this.InputIds.Digitals,
                {} s when s.StartsWith("Analog") => this.InputIds.Analogs,
                {} s when s.StartsWith("Pointer") => this.InputIds.Pointers,
            };

            var keyValueInputs = inputIds.Select(x =>
            {
                return KeyValuePair.Create
                (
                    x,
                    this.InputIds.GetDisplayName(x)
                );
            }).ToArray();

            var cb = new ComboBox
            {
                ItemsSource = keyValueInputs,
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key",
            };

            cb.SetBinding(ComboBox.SelectedValueProperty, property.CreateBinding(UpdateSourceTrigger.PropertyChanged));
            return cb;
        }

        return null;
    }
}