using PropertyTools.Wpf;

namespace SpaceEditor.Controls;

public class FlatGroupingDataGridOperator : PropertyGridOperator
{
    protected override void SetProperties(PropertyItem pi, object instance)
    {
        base.SetProperties(pi, instance);
        pi.Category = instance.GetType().Name;
    }
}