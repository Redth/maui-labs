using AIAttributes.Sample.Garden.ViewModels;

namespace AIAttributes.Sample.Garden;

public partial class CartPage : ContentPage
{
    public CartPage(MainViewModel vm)
    {
        BindingContext = vm;
        InitializeComponent();
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
