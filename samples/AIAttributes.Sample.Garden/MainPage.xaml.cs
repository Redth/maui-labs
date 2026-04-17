using AIAttributes.Sample.Garden.ViewModels;

namespace AIAttributes.Sample.Garden;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.Initialize();
    }
}
