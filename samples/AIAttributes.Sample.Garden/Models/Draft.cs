namespace AIAttributes.Sample.Garden.Models;

/// <summary>
/// A shopping list saved as a draft when the user starts a new chat with
/// uncommitted items. Drafts live in the singleton archive alongside orders
/// so they survive across sessions even though they were never checked out.
/// </summary>
public record Draft(
    string Id,
    DateTime SavedAt,
    IReadOnlyList<ListItem> Items)
{
    public decimal Total => Items.Sum(i => i.Subtotal);
}
