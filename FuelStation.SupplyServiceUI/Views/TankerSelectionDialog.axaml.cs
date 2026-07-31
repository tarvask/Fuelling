using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FuelStation.SupplyServiceUI.ViewModels;

namespace FuelStation.SupplyServiceUI.Views;

public partial class TankerSelectionDialog : Window
{
    public TankerSelectionDialog()
    {
        AvaloniaXamlLoader.Load(this);
        var viewModel = new TankerSelectionViewModel();
        DataContext = viewModel;
        viewModel.CloseAction = result => Close(result);
    }
}