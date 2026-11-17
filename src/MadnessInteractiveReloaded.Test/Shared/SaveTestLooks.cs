using System;
using Walgelijk;

namespace MIR.Test.Shared;

/// <summary>
/// Fake armour and looks, so looks can be written and read without the base data. Registers them on construction and
/// clears the registries on dispose. Use it only from the "Registry collection".
/// </summary>
internal sealed class SaveTestLooks : IDisposable
{
    public readonly ArmourPiece Body = new() { Name = "fake_body", Type = ArmourPieceType.Body };
    public readonly ArmourPiece Head = new() { Name = "fake_head", Type = ArmourPieceType.Head };
    public readonly ArmourPiece HeadAccessory = new() { Name = "fake_hat", Type = ArmourPieceType.HeadAccessory };

    public SaveTestLooks()
    {
        BaseAssetsFixture.EnsureRegistered();
        Registries.ClearAll();
        Registries.Armour.Body.Register("fake_body", Body);
        Registries.Armour.Head.Register("fake_head", Head);
        Registries.Armour.HeadAccessory.Register("fake_hat", HeadAccessory);
        Registries.Looks.Register("grunt", new CharacterLook { Name = "grunt", Body = Body, Head = Head });
    }

    public CharacterLook Look(string name = "look") => new()
    {
        Name = name,
        Body = Body,
        Head = Head,
        HeadLayer1 = HeadAccessory,
        BloodColour = new Color("#6BD33AA3"),
    };

    /// <summary>
    /// A <c>.look</c> file with the given version line, as MIR wrote it.
    /// </summary>
    public static string LookFile(string version) =>
        $"""
        {version}

        body
           fake_body

        head
           fake_head
           fake_hat

        blood #6BD33AA3
        """;

    public void Dispose() => Registries.ClearAll();
}
