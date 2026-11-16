using System;
using System.IO;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using MIR.Cutscenes;
using Walgelijk;
using Walgelijk.AssetManager;
using Walgelijk.Localisation;
using Xunit;

namespace MIR.Test.Controller;

/// <summary>
/// The controller wired into the real game: player systems, weapons, cutscenes and rumble.
/// </summary>
[Collection("Registry collection")]
public class PlayerControllerTests : IDisposable
{
    private const float Dt = ControllerTestHarness.Dt;
    private static readonly TimeSpan OneFrame = TimeSpan.FromSeconds(Dt);

    private readonly ControllerTestHarness h = new();

    public void Dispose() => h.Dispose();

    private static CharacterComponent Player(Scene scene, out PlayerComponent player)
    {
        MadnessUtils.FindPlayer(scene, out var p, out var character).Should().BeTrue();
        player = p!;
        return character!;
    }

    private static CharacterComponent Player(Scene scene) => Player(scene, out _);

    private static BodyPartComponent Head(Scene scene, CharacterComponent character)
        => scene.GetComponentFrom<BodyPartComponent>(character.Positioning.Head.Entity);

    private bool AnyVibration() => h.Pad.Vibrations.Any(v => v.Item1 > 0 || v.Item2 > 0);

    [Fact]
    public void Left_stick_walks_player_right()
    {
        var scene = h.PrepareGameplayScene();
        try
        {
            var character = Player(scene);
            var startX = character.Positioning.GlobalCenter.X;

            h.SetPad(left: new Vector2(1, 0));
            h.Step(TimeSpan.FromSeconds(0.5));

            character.Positioning.GlobalCenter.X.Should().BeGreaterThan(startX + 20);
            Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad, "the per-frame input reset keeps the aim override from reading as mouse movement");
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void A_triggers_jump_dodge()
    {
        var scene = h.PrepareGameplayScene();
        try
        {
            var character = Player(scene);
            character.IsPlayingAnimation.Should().BeFalse();

            h.SetPad(ControllerTestHarness.ButtonA);
            h.Step(OneFrame);
            h.SetPad();
            h.Step(TimeSpan.FromSeconds(0.1));

            character.IsPlayingAnimation.Should().BeTrue();
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Hud_label_uses_pad_names()
    {
        h.Frame();
        Gamepad.ActiveDevice = InputDevice.Gamepad;

        ControlScheme.ActiveControlScheme.GetDisplayName(GameAction.Ability1).Should().Be("LB");

        Gamepad.ActiveDevice = InputDevice.KeyboardMouse;
        ControlScheme.ActiveControlScheme.GetDisplayName(GameAction.Ability1)
            .Should().Be(ControlScheme.ActiveControlScheme.InputMap[GameAction.Ability1].ToString());
    }

    [Fact]
    public void Shooting_rumbles_when_pad_active()
    {
        var scene = h.PrepareGameplayScene();
        try
        {
            var character = Player(scene);
            var weapon = Prefabs.CreateWeapon(scene, character.Positioning.GlobalCenter, Registries.Weapons.Get("m1911"));
            character.EquipWeapon(scene, weapon).Should().BeTrue();
            var rounds = weapon.RemainingRounds;

            h.SetPad();
            h.Step(OneFrame);
            h.Pad.Vibrations.Clear();

            h.SetPad(rightTrigger: ControllerTestHarness.TriggerFull);
            h.Step(OneFrame);
            h.SetPad();
            h.Step(TimeSpan.FromSeconds(0.05));

            weapon.RemainingRounds.Should().BeLessThan(rounds, "RT fired the gun");
            AnyVibration().Should().BeTrue();
            h.Pad.Vibrations.Should().AllSatisfy(v => v.Item2.Should().BeLessThanOrEqualTo(0.65f));
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Shooting_with_mouse_does_not_rumble()
    {
        var scene = h.PrepareGameplayScene();
        try
        {
            var character = Player(scene);
            var weapon = Prefabs.CreateWeapon(scene, character.Positioning.GlobalCenter, Registries.Weapons.Get("m1911"));
            character.EquipWeapon(scene, weapon).Should().BeTrue();
            var rounds = weapon.RemainingRounds;
            Gamepad.ActiveDevice = InputDevice.Gamepad; // the click below has to switch the device back to rule the pad out

            h.SetPad();
            h.EngineReset();
            ref var input = ref h.Input;
            input.MouseButtonsDown!.Add(MouseButton.Left);
            input.MouseButtonsHeld!.Add(MouseButton.Left);
            input.AnyMouseButton = true;
            h.Step(OneFrame, resetFirst: false);
            h.Input.MouseButtonsHeld!.Remove(MouseButton.Left);
            h.Step(TimeSpan.FromSeconds(0.05));

            weapon.RemainingRounds.Should().BeLessThan(rounds, "the mouse fired the gun");
            Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse);
            AnyVibration().Should().BeFalse();
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Losing_health_rumbles()
    {
        var scene = h.PrepareGameplayScene();
        try
        {
            var character = Player(scene);
            Gamepad.ActiveDevice = InputDevice.Gamepad;
            h.Step(OneFrame);
            h.Pad.Vibrations.Clear();

            var head = Head(scene, character);
            head.Damage(head.Health * 0.25f);
            h.Step(OneFrame);

            character.IsAlive.Should().BeTrue();
            h.Pad.Vibrations.Should().Contain((ControllerRumble.HitLow, ControllerRumble.HitHigh));
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Losing_health_with_keyboard_and_mouse_does_not_rumble()
    {
        var scene = h.PrepareGameplayScene();
        try
        {
            var character = Player(scene);
            h.Step(OneFrame);
            Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse);

            var head = Head(scene, character);
            head.Damage(head.Health * 0.25f);
            h.Step(TimeSpan.FromSeconds(0.1));

            AnyVibration().Should().BeFalse();
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Death_rumbles()
    {
        var scene = h.PrepareGameplayScene();
        try
        {
            var character = Player(scene);
            Gamepad.ActiveDevice = InputDevice.Gamepad;
            h.Step(OneFrame);
            h.Pad.Vibrations.Clear();

            character.Kill();
            h.Step(OneFrame);

            h.Pad.Vibrations.Should().Contain((ControllerRumble.DeathLow, ControllerRumble.DeathHigh));
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void New_player_with_less_health_is_not_a_hit()
    {
        var first = h.PrepareGameplayScene();
        Scene? second = null;
        try
        {
            Gamepad.ActiveDevice = InputDevice.Gamepad;
            h.Step(OneFrame);
            h.Pad.Vibrations.Clear();

            // A level change: the next scene's player starts with less health than the last one had.
            second = h.Game.Scene = SceneUtils.PrepareGameScene(h.Game, GameMode.Experiment, true, Registries.Levels.Get("dbg_room").Level.Value);
            var head = Head(second, Player(second));
            head.Damage(head.Health * 0.5f);
            h.Step(TimeSpan.FromSeconds(0.1));

            AnyVibration().Should().BeFalse();
        }
        finally
        {
            first.Dispose();
            second?.Dispose();
        }
    }

    [Fact]
    public void DPad_up_and_down_zoom_within_limits()
    {
        var scene = h.PrepareGameplayScene();
        try
        {
            Player(scene, out var player);
            player.ZoomLevel.Should().Be(1);

            h.SetPad(ControllerTestHarness.ButtonDPadUp);
            h.Step(TimeSpan.FromSeconds(0.1)); // held: zooms once
            player.ZoomLevel.Should().BeApproximately(1 / 0.9f, 0.0001f);

            h.SetPad();
            h.Step(OneFrame);
            h.SetPad(ControllerTestHarness.ButtonDPadDown);
            h.Step(OneFrame);
            player.ZoomLevel.Should().BeApproximately(1, 0.0001f);

            for (int i = 0; i < 10; i++)
            {
                h.SetPad();
                h.Step(OneFrame);
                h.SetPad(ControllerTestHarness.ButtonDPadUp);
                h.Step(OneFrame);
            }
            player.ZoomLevel.Should().Be(2);
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void DPad_pressed_in_menu_does_not_zoom()
    {
        var scene = h.PrepareGameplayScene();
        try
        {
            Player(scene, out var player);
            scene.FindAnyComponent<PauseComponent>(out var pause).Should().BeTrue();

            pause!.Paused = true;
            h.Frame();
            h.Frame(ControllerTestHarness.ButtonDPadUp);
            Gamepad.Pressed(GamepadButton.DPadUp).Should().BeTrue();
            Gamepad.GameplayInputAllowed.Should().BeFalse();

            // The menu closes partway through the frame, before the player system runs.
            pause.Paused = false;
            scene.UpdateSystems();

            player.ZoomLevel.Should().Be(1);
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Holding_start_skips_cutscene()
    {
        var scene = h.Game.Scene = new Scene(h.Game);
        try
        {
            scene.AddSystem(new CutsceneSystem());
            var cutscene = new CutsceneComponent(new Cutscene([new IdleSlide()])) { DestroyEntityOnCompletion = false };
            scene.AttachComponent(scene.CreateEntity(), cutscene);
            h.Game.AdditionalLoopEvents.Add(h.Loop);

            h.SetPad(ControllerTestHarness.ButtonStart);
            h.Step(TimeSpan.FromSeconds(0.5));
            cutscene.IsComplete.Should().BeFalse("Start has to be held for a second");

            h.SetPad();
            h.Step(OneFrame);
            cutscene.SkipTimer.Should().Be(0, "letting go restarts the hold");

            h.SetPad(ControllerTestHarness.ButtonStart);
            h.Step(TimeSpan.FromSeconds(1.2));
            cutscene.IsComplete.Should().BeTrue();
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Controller_settings_are_localised()
    {
        var asset = Assets.EnumerateFolder("locale", SearchOption.AllDirectories)
            .Single(a => Path.GetFileNameWithoutExtension(Assets.GetMetadata(a).Path) == "en-GB");
        var table = Assets.Load<Language>(asset).Value.Table;

        table["settings-controller"].Should().Be("Controller");
        table["settings-controller-vibration"].Should().Be("Controller vibration");
        table["settings-controller-aim-distance"].Should().Be("Controller aim distance");
        table["settings-controller-cursor-speed"].Should().Be("Controller cursor speed");
        table["settings-controller-bindings"].Should().Be("Controller bindings");
        table["settings-controller-press-button"].Should().Be("Press a button... (View/Back or Esc to cancel)");
        table["settings-controller-off-confirm"].Should().Be("Turn off controller? You'll need a mouse or keyboard to turn it back on.");
        table["settings-keyboard-bindings"].Should().Be("Keyboard & mouse");
        table["settings-also-zoom"].Should().Be("(also zooms)");
        table["settings-reset-controller"].Should().Be("Reset controller bindings");
        table["settings-controller-fixed"].Should().Be("Fixed controller buttons");
        table["settings-fixed-pause"].Should().Be("Start: Pause");
        table["settings-fixed-zoom"].Should().Be("D-pad Up/Down: Zoom");
        table["settings-fixed-select"].Should().Be("A: Select");
        table["settings-fixed-back"].Should().Be("B: Back");
        table["settings-fixed-navigate"].Should().Be("D-pad / Left stick: Navigate");
        table["settings-fixed-cursor"].Should().Be("Right stick: Cursor");
        table["settings-fixed-scroll"].Should().Be("LT / RT: Scroll");
        table.Should().NotContainKey("settings-controller-layout", "the read-only layout list became the rebind rows");
    }

    [Fact]
    public void Shot_rumble_scales_with_recoil()
    {
        ControllerRumble.ShotHigh(0).Should().BeApproximately(0.35f, 0.0001f);
        ControllerRumble.ShotHigh(ControllerRumble.FullRumbleRecoil).Should().BeApproximately(0.65f, 0.0001f);
        ControllerRumble.ShotHigh(100).Should().BeApproximately(0.65f, 0.0001f, "clamped");
        ControllerRumble.ShotHigh(-1).Should().BeApproximately(0.35f, 0.0001f, "clamped");
    }

    /// <summary>
    /// A slide that lasts long enough for the skip tests and draws nothing.
    /// </summary>
    private sealed class IdleSlide : ISlide
    {
        public float Duration { get; set; } = 100;
        public bool IsReady => true;

        public void OnStart() { }
        public void OnUpdate(Window window, Time time) { }
        public void OnStop() { }
        public void Execute(IGraphics graphics) { }
        public void Dispose() { }
    }
}
