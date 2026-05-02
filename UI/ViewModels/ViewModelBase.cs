using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.UI.ViewModels;

/// <summary>
/// Base ViewModel with INotifyPropertyChanged implementation.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Simple ICommand implementation.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }
    
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }
    
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
    
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    
    public void Execute(object? parameter) => _execute(parameter);
    
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

/// <summary>
/// Item for ComboBox binding.
/// </summary>
public class ComboItem<T>
{
    public T Value { get; init; }
    public string DisplayName { get; init; }
    
    public ComboItem(T value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }
    
    public override string ToString() => DisplayName;
}

/// <summary>
/// ViewModel for spacing standard selection.
/// </summary>
public class SpacingStandardItem : ComboItem<SpacingStandardType>
{
    public string Region { get; init; }
    
    public SpacingStandardItem(SpacingStandardType type, string name, string region) 
        : base(type, $"{name} ({region})")
    {
        Region = region;
    }
}

/// <summary>
/// ViewModel for insulation type selection.
/// </summary>
public class InsulationTypeItem : ComboItem<InsulationType>
{
    public double DensityKgM3 { get; init; }
    
    public InsulationTypeItem(InsulationType type, string name, double density)
        : base(type, type == InsulationType.None ? name : $"{name} ({density} kg/m³)")
    {
        DensityKgM3 = density;
    }
}

/// <summary>
/// ViewModel for theme selection.
/// </summary>
public class ThemeItem : ComboItem<AppTheme>
{
    public ThemeItem(AppTheme theme) : base(theme, ThemeManager.GetThemeName(theme)) { }
}
