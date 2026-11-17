using Walgelijk;

namespace MIR;

/// <summary>
/// Makes the settings save that <see cref="Settings.Apply(Game)"/> asks for, on the main thread, once the changes stop
/// (<see cref="Settings.SaveDelaySeconds"/>). It replaces a thread-pool save on every <c>Apply</c>, which during a slider drag
/// was a save every frame, serialising the settings while the UI changed them.
/// </summary>
public sealed class SettingsSaveLoopEvent : IGameLoopEvent
{
    public void Update(Game game, float dt) => Settings.SaveIfDue();

    public void FixedUpdate(Game game, float interval)
    {
    }
}
