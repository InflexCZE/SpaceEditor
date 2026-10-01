using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SpaceEditor.Rocks;
using SpaceEditor.Services;
using System.ComponentModel;

namespace SpaceEditor.Controls;

/// <summary>
/// Interaction logic for ReflectedObject.xaml
/// </summary>
public partial class ReflectedObject : UserControl
{
    public static readonly DependencyProperty ReflectedInstanceProperty = DependencyProperty.Register
    (
        nameof(ReflectedInstance),
        typeof(object),
        typeof(ReflectedObject),
        new PropertyMetadata(default(object))
    );

    public static readonly DependencyProperty NewObjectTypeCandidatesProperty = DependencyProperty.Register
    (
        nameof(NewObjectTypeCandidates),
        typeof(IEnumerable),
        typeof(ReflectedObject),
        new PropertyMetadata(default(IEnumerable))
    );

    public object ReflectedInstance
    {
        get { return (object) GetValue(ReflectedInstanceProperty); }
        set { SetValue(ReflectedInstanceProperty, value); }
    }

    public IEnumerable NewObjectTypeCandidates
    {
        get { return (IEnumerable) GetValue(NewObjectTypeCandidatesProperty); }
        set { SetValue(NewObjectTypeCandidatesProperty, value); }
    }

    public ReflectedObject()
    {
        InitializeComponent();
        PropertyChangedEventManager.AddHandler(LocalizationManager.Current, OnLanguageChanged, nameof(LocalizationManager.LanguageCode));
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 只重建属性面板的显示模板，不清空双向绑定的游戏对象。
        var selector = this.InstanceHost.ContentTemplateSelector;
        this.InstanceHost.ContentTemplateSelector = null;
        this.InstanceHost.ContentTemplateSelector = selector;
    }

    private void OnNewTypeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
            return;

        var type = ((KeyValuePair<string, Type>) e.AddedItems[0]!).Value;
        this.ReflectedInstance = type.AllocateObjectBuilder();
    }
}
