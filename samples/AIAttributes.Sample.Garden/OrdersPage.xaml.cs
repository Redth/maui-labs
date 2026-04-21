using AIAttributes.Sample.Garden.ViewModels;

namespace AIAttributes.Sample.Garden;

public partial class OrdersPage : ContentPage
{
    public OrdersPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
