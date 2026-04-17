namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Publishes the active <see cref="ChatSession"/> so AI tools can access
/// it via <c>[FromServices] CurrentSession</c>.
/// </summary>
public sealed class CurrentSession
{
    private ChatSession _session = new("session-initial");

    public ChatSession Session => _session;

    public event Action<ChatSession>? Changed;

    public void Set(ChatSession session)
    {
        _session = session;
        Changed?.Invoke(session);
    }
}
