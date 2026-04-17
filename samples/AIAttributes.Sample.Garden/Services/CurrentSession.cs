namespace AIAttributes.Sample.Garden.Services;

/// <summary>
/// Singleton accessor that publishes the active <see cref="ChatSession"/>
/// to AI tools. The view model swaps the current session whenever the user
/// taps "New Chat"; tools always read whichever session is current at
/// invocation time via <c>[FromServices] ICurrentSession</c>.
/// </summary>
/// <remarks>
/// This is the bridge between view-model-owned per-session state and the
/// singleton-only DI graph. No <see cref="IServiceScope"/> required.
/// </remarks>
public interface ICurrentSession
{
    ChatSession Session { get; }

    event Action<ChatSession>? Changed;

    void Set(ChatSession session);
}

public sealed class CurrentSession : ICurrentSession
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

/// <summary>
/// Singleton factory that produces <see cref="ChatSession"/> instances.
/// Kept separate from <see cref="ICurrentSession"/> so the view model can
/// new up a session, drain the previous one (e.g. save as draft), then
/// publish — instead of having to do all of that under one accessor call.
/// </summary>
public sealed class ChatSessionFactory
{
    public ChatSession Create() =>
        new($"session-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}");
}
