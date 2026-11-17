namespace MIR.Serialisation;

/// <summary>
/// Turns a burst of "please save" requests (a slider drag asks every frame) into one save, made once no request has come in
/// for <see cref="DelaySeconds"/>. It only keeps time; the caller saves when <see cref="TakeIfDue"/> says so.
/// </summary>
public sealed class DebouncedSave
{
    public readonly double DelaySeconds;

    private readonly object gate = new();
    private double? dueAt;

    public DebouncedSave(double delaySeconds)
    {
        DelaySeconds = delaySeconds;
    }

    /// <summary>
    /// True while a requested save has not been made yet.
    /// </summary>
    public bool IsPending
    {
        get
        {
            lock (gate)
                return dueAt.HasValue;
        }
    }

    /// <summary>
    /// Asks for a save <see cref="DelaySeconds"/> after <paramref name="now"/>, pushing back any earlier request.
    /// </summary>
    public void Request(double now)
    {
        lock (gate)
            dueAt = now + DelaySeconds;
    }

    /// <summary>
    /// True once, when a requested save is due at <paramref name="now"/>. The request is then cleared.
    /// </summary>
    public bool TakeIfDue(double now)
    {
        lock (gate)
        {
            if (dueAt is not double due || now < due)
                return false;
            dueAt = null;
            return true;
        }
    }

    /// <summary>
    /// Drops a pending request, because the save was made some other way.
    /// </summary>
    public void Cancel()
    {
        lock (gate)
            dueAt = null;
    }
}
