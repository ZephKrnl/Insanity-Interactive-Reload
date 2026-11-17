using MIR.Serialisation;
using System;
using System.IO;
using System.Text;
using Walgelijk;
using Walgelijk.AssetManager;
using Walgelijk.Localisation;

namespace MIR;

/// <summary>
/// Steps for loading the game.
/// </summary>
public static class GameLoadingSteps
{
    /// <summary>
    /// Load all the flipbook textures.
    /// </summary>
    public static void LoadFlipbooks()
    {
        foreach (var bloodSpurt in Textures.BloodSpurts)
            _ = bloodSpurt.Asset.Value;

        foreach (var item in Textures.Muzzleflashes)
            _ = item.Value;
    }

    /// <summary>
    /// Load up the sounds.
    /// </summary>
    public static void PreloadAudio()
    {
        _ = Sounds.Empty; // this loads all sounds in the Sounds struct
    }

    /// <summary>
    /// Initialises the <see cref="BodyPartMaterialPool"/>.
    /// </summary>
    public static void PrefillMaterialPool()
    {
        for (int i = 0; i < 10; i++)
            BodyPartMaterialPool.Instance.Prefill();
    }

    /// <summary>
    /// Load animations.
    /// </summary>
    public static void PreloadAnimations()
    {
        _ = Animations.DeathByHeadshot.FromBehind;
    }

    /// <summary>
    /// Load the fonts.
    /// </summary>
    public static void PrepareFonts()
    {
        _ = Fonts.Inter;
        _ = Fonts.Toxigenesis;
        _ = Fonts.Oxanium;
    }

    /// <summary>
    /// Load textures.
    /// </summary>
    public static void PrepareTextures()
    {
        Assets.Load<Texture>("textures/backgrounds/background_opening.qoi");
        Assets.Load<Texture>("textures/red-black-gradient.png");
        Assets.Load<Texture>("textures/black-transparent-gradient.png");
        _ = Textures.UserInterface.Logo;

        foreach (var w in Registries.Weapons.GetAllValues())
        {
            Game.Main.Window.Graphics.Upload(w.BaseTexture.Value);
            Game.Main.Window.Graphics.Upload(WeaponThumbnailCache.Instance.Load(w));
        }
    }

    /// <summary>
    /// Load the player look or default it to the "grunt". An unreadable <c>player.look</c> falls back to its <c>.bak</c>.
    /// It is not moved aside: a look that uses armour from a mod that is not installed is unreadable now but fine later.
    /// Only a look read from the file (or a missing file) marks <see cref="UserData.Loaded.PlayerLook"/>, so the exit save
    /// does not overwrite a look this session could not read.
    /// </summary>
    public static void LoadPlayerLook()
    {
        Registries.Looks.Get("grunt").CopyTo(UserData.Instances.PlayerLook);

        var result = SaveFile.TryLoad(UserData.Paths.PlayerLookFile,
            static text => CharacterLookDeserialiser.Load(new MemoryStream(Encoding.UTF8.GetBytes(text)), UserData.Paths.PlayerLookFile),
            out var read, "player look", quarantine: false);
        read?.CopyTo(UserData.Instances.PlayerLook);
        UserData.Loaded.PlayerLook = result is SaveLoadResult.Loaded or SaveLoadResult.Missing;

        UserData.Instances.PlayerLook.Cosmetic = true;
    }
}
