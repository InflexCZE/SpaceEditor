using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyTools.Wpf;
using SpaceEditor.Services;

namespace SpaceEditor.Controls;

public class FlatGroupingDataGridOperator : PropertyGridOperator
{
    protected override void SetProperties(PropertyItem pi, object instance)
    {
        base.SetProperties(pi, instance);
        pi.Category = LocalizationManager.DisplayName(instance.GetType().Name);

        // Untranslated names keep the label PropertyTools generated
        var name = LocalizationManager.PropertyName(pi.Descriptor.Name);
        if (name != pi.Descriptor.Name)
            pi.DisplayName = name;
    }
}
