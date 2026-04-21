using AIAttributes.Sample.Garden.ViewModels;
using System.Globalization;

namespace AIAttributes.Sample.Garden;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private const double WideBreakpoint = 720;

    public MainPage(MainViewModel viewModel)
    {
        Resources.Add("BadgeVisibleConverter", new BadgeVisibleConverter());
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Initialize();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0)
            _viewModel.IsWideLayout = width >= WideBreakpoint;
    }
}

/// <summary>
/// Returns true when the cart badge is > 0.
/// </summary>
internal sealed class BadgeVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && int.TryParse(s, out var n) && n > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
