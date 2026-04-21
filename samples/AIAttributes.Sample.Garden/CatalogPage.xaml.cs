using AIAttributes.Sample.Garden.ViewModels;

namespace AIAttributes.Sample.Garden;

public partial class CatalogPage : ContentPage
{
    public CatalogPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
