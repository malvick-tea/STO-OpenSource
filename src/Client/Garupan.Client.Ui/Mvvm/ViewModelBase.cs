using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Garupan.Client.Ui.Mvvm;

/// <summary>
/// Carry-forward MVVM root from the Godot codebase, with Godot-specific bits removed.
/// Screens hold a view-model derived from this; <see cref="SetField"/> drives
/// <see cref="PropertyChanged"/> notifications without ceremony.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
