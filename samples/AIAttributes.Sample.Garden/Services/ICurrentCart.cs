using AIAttributes.Sample.Garden.Models;

namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Active shopping cart — manages item list, quantities, and checkout.
/// </summary>
public interface ICurrentCart
{
    string Id { get; }
    CancellationTokenSource Cts { get; }
    IReadOnlyList<ListItem> Items { get; }

    void Reset();

    ListItem AddOrIncrement(Product product, int quantity);
    ListItem? ChangeQuantity(string sku, int newQuantity);
    bool Remove(string sku);
    IReadOnlyList<ListItem> Snapshot();

    IReadOnlyList<ListItem> ShowList();
    string AddToList(string skuOrName, int quantity = 1);
    string ChangeQty(string skuOrName, int quantity);
    string RemoveFromList(string skuOrName);
    string CheckoutList(OrderArchive archive);
    string CancelList();
}
