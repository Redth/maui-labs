using AIAttributes.Sample.Garden.ViewModels;

namespace AIAttributes.Sample.Garden.Pages;

public partial class OrdersPage : ContentPage
{
    public OrdersPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
