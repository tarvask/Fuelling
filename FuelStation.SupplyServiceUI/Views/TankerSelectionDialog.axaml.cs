using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FuelStation.SupplyServiceUI.ViewModels;

namespace FuelStation.SupplyServiceUI.Views;

public partial class TankerSelectionDialog : Window
{
    public TankerSelectionDialog(TankerSelectionViewModel viewModel)
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = viewModel;
        viewModel.CloseAction = result => Close(result);
    }
}