namespace AIAttributes.Sample.Garden.Pages;

public partial class OrdersPage : ContentPage
{
    public OrdersPage()
    {
        InitializeComponent();
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
