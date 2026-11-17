using System;
using Walgelijk.AssetManager;
using Xunit;

namespace MIR.Test;

/// <summary>
/// Registers the packed <c>base</c> asset package and the game's asset deserialisers once for the whole test run, instead of
/// every test class doing it in its constructor. Used by the "Registry collection" (see <see cref="RegistryCollection"/>).
/// </summary>
public sealed class BaseAssetsFixture
{
    private static readonly object gate = new();
    private static bool ready;

    public BaseAssetsFixture() => EnsureRegistered();

    /// <summary>Safe to call any number of times, from any thread: the work happens once.</summary>
    public static void EnsureRegistered()
    {
        lock (gate)
        {
            if (ready)
                return;

            if (!Assets.TryGetPackage("base", out _))
            {
                try
                {
                    Assets.RegisterPackage("resources/base.waa");
                }
                catch (Exception e)
                {
                    throw new Exception("Tests can't continue because the base assets cannot be found", e);
                }
            }

            if (!Assets.TryGetPackage("base", out _))
                throw new Exception("Tests can't continue because the base assets cannot be found");

            MadnessInteractiveReloaded.PrepareResourceInitialise();
            ready = true;
        }
    }
}

[CollectionDefinition("Registry collection")]
public sealed class RegistryCollection : ICollectionFixture<BaseAssetsFixture>
{
}
