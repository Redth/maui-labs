namespace AIAttributes.Sample.Garden.Pages;

public partial class CatalogPage : ContentPage
{
    public CatalogPage()
    {
        InitializeComponent();
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
