using SpaceEditor.Rocks;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace SpaceEditor.Controls;

/// <summary>
/// Interaction logic for ReflectedCollection.xaml
/// </summary>
public partial class ReflectedCollection : UserControl
{
    public static readonly DependencyProperty ReflectedItemsProperty = DependencyProperty.Register
    (
        nameof(ReflectedItems),
        typeof(IEnumerable),
        typeof(ReflectedCollection),
        new PropertyMetadata(default(IEnumerable))
    );

    public static readonly DependencyProperty NewItemTypeCandidatesProperty = DependencyProperty.Register
    (
        nameof(NewItemTypeCandidates),
        typeof(IEnumerable),
        typeof(ReflectedCollection),
        new PropertyMetadata(default(IEnumerable))
    );

    public IEnumerable? ReflectedItems
    {
        get { return (IEnumerable?)GetValue(ReflectedItemsProperty); }
        set { SetValue(ReflectedItemsProperty, value); }
    }

    public IEnumerable? NewItemTypeCandidates
    {
        get { return (IEnumerable?)GetValue(NewItemTypeCandidatesProperty); }
        set { SetValue(NewItemTypeCandidatesProperty, value); }
    }

    public ReflectedCollection()
    {
        InitializeComponent();
    }

    private void RemoveBinding(object sender, RoutedEventArgs e)
    {
        var v = (FrameworkElement)sender;
        var current = v.DataContext!;

        UpdateCollection(x =>
        {
            x.Remove(current);
        });
    }

    private void AddElement(object sender, RoutedEventArgs e)
    {
        var selectedType = (Type?)this.NewElementTypes.SelectedValue;
        if (selectedType is null)
            return;

        UpdateCollection(x =>
        {
            x.Add(selectedType.AllocateObjectBuilder());
        });
    }

    private void UpdateCollection(Action<IList> update)
    {
        if (this.ReflectedItems is ICollectionView cv)
        {
            update((IList)cv.SourceCollection);
            cv.Refresh();
            return;
        }

        if (this.ReflectedItems is IList list)
        {
            update(list);

            // Force refresh UI
            this.ReflectedItems = null!;
            this.ReflectedItems = list;
            return;
        }

        throw new Exception("Unknown collection kind");
    }
}