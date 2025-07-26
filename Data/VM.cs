using System.ComponentModel;
using System.Runtime.CompilerServices;
using SpaceEditor.Rocks;

namespace SpaceEditor.Data;

public abstract class VM : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public IDisposable Bind<T>(string property, Action<T> consumer)
    {
        var source = GetType().GetProperty(property)!.GetMethod;

        consumer(Getvalue());
        
        PropertyChangedEventHandler handler = (sender, args) =>
        {
            consumer(Getvalue());
        };
        this.PropertyChanged += handler;
        return Disposable.Create(() =>
        {
            this.PropertyChanged -= handler;
        });
        
        T Getvalue()
        {
            return (T) source.Invoke(this, null);
        }
    }
    
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}