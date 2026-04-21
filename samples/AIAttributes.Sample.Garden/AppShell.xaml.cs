namespace AIAttributes.Sample.Garden;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register modal routes — these pages slide up as modals when navigated to
        Routing.RegisterRoute("orders", typeof(OrdersPage));
        Routing.RegisterRoute("catalog", typeof(CatalogPage));
    }
}
