using System.Windows;

namespace TFG_MonitorEnergia.WpfApp.ViewModels;

// Permite binding desde objetos fuera del árbol visual (p.ej. DataGridColumn)
public sealed class BindingProxy : Freezable
{
    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
