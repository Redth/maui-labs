using AIAttributes.Sample.Garden.ViewModels;

namespace AIAttributes.Sample.Garden.Pages;

public partial class CartPage : ContentPage
{
    public CartPage(MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
