namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Publishes the active <see cref="Cart"/> so AI tools can access
/// it via <c>[FromServices] CurrentCart</c>.
/// </summary>
public sealed class CurrentCart
{
    private Cart _cart = new("cart-initial");

    public Cart Cart => _cart;

    public event Action<Cart>? Changed;

    public void Set(Cart cart)
    {
        _cart = cart;
        Changed?.Invoke(cart);
    }
}
