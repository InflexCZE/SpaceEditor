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
        pi.Category = instance.GetType().Name;
        if (LocalizationManager.Current.LanguageCode == "zh-CN")
        {
            pi.Category = LocalizationManager.DisplayName(pi.Category);
            pi.DisplayName = LocalizationManager.PropertyName(pi.Descriptor.Name);
        }
    }
}
