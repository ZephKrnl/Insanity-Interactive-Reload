using System.Runtime.CompilerServices;

namespace MIR.Test;

internal static class TestAssemblySetup
{
    /// <summary>
    /// Base game data names its types as "MIR.Something, MIR", after the game assembly's name from before it became IIR.
    /// The game registers the redirect at the start of <c>Program.Main</c>. The tests never run Main, so this registers it
    /// before any test runs, whatever order they run in.
    /// </summary>
#pragma warning disable CA2255 // a test assembly's own startup, not a library's
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void RegisterLegacyAssemblyRedirect() => LegacyAssemblyRedirect.Register();
}
