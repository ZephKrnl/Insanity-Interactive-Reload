using System;
using System.Numerics;
using Walgelijk;
using Walgelijk.SimpleDrawing;

namespace MIR;

/// <summary>
/// The splash screen when the game is opening.
/// </summary>
public static class GameOpeningScene
{
    public static Scene Create(Game game)
    {
        var scene = new Scene(game);
        var camera = scene.CreateEntity();

        scene.AttachComponent(camera, new TransformComponent());
        scene.AttachComponent(camera, new CameraComponent());
        scene.AddSystem(new TransformSystem());
        scene.AddSystem(new CameraSystem());
        scene.AddSystem(new GameOpeningSystem());

        // TODO scene persistence
        //scene.ShouldBeDisposedOnSceneChange = true;

        return scene;
    }

    public class GameOpeningSystem : Walgelijk.System
    {
        /// <summary>
        /// Seconds the logo takes to fade in from black.
        /// </summary>
        public const float FadeIn = 0.4f;

        /// <summary>
        /// Seconds the logo takes to fade out to black at the end of the splash.
        /// </summary>
        public const float FadeOut = 0.6f;

        /// <summary>
        /// Seconds the splash lasts: it fades in, holds at full brightness until <c>TotalDuration - FadeOut</c>, fades out,
        /// and then the game loads.
        /// </summary>
        public const float TotalDuration = 3f;

        /// <summary>
        /// From this many seconds in, any key, mouse button or pad button skips the splash. Not straight away, so a click
        /// or key that launched the game does not skip it.
        /// </summary>
        public const float SkipAfter = 0.25f;

        /// <summary>
        /// Seconds the logo takes to settle from <see cref="LogoStartScale"/> to its resting size.
        /// </summary>
        private const float SettleDuration = 0.6f;

        /// <summary>
        /// The logo starts this much larger and settles to its resting size over <see cref="SettleDuration"/>.
        /// </summary>
        private const float LogoStartScale = 1.06f;

        private float t = 0; // TODO stateful system is technically not allowed, but i doubt it matters here lmao
        private bool finished;

        public override void Update()
        {
            if (finished)
                return;

            t += Time.DeltaTime;

            if (t >= TotalDuration || (t > SkipAfter && SkipPressed()))
            {
                finished = true;
                Game.Scene = GameLoadingScene.Create(Game);
            }
        }

        /// <summary>
        /// A key, mouse button or pad button went down this frame.
        /// </summary>
        private bool SkipPressed()
        {
            if (Input.KeysDown is { Count: > 0 } || Input.MouseButtonsDown is { Count: > 0 })
                return true;

            // A button that was already down the frame before does not count, nor does a pad that has just (re)connected
            // with a button held.
            var current = Gamepad.Current;
            var previous = Gamepad.Previous;
            return current.Connected && previous.Connected
                && ((current.Buttons & ~previous.Buttons) != 0
                    || Gamepad.Pressed(GamepadButton.LeftTrigger)
                    || Gamepad.Pressed(GamepadButton.RightTrigger));
        }

        /// <summary>
        /// 1 is black, 0 is full brightness: fades in over <see cref="FadeIn"/>, holds, and fades out over the last
        /// <see cref="FadeOut"/> seconds.
        /// </summary>
        private float Darkness()
        {
            if (t < FadeIn)
                return 1 - t / FadeIn;
            return Utilities.Clamp((t - (TotalDuration - FadeOut)) / FadeOut);
        }

        public override void Render()
        {
            var r = new Rect(0, 0, Window.Width, Window.Height);

            Draw.Reset();
            Draw.ScreenSpace = true;

            Draw.Colour = Colors.Black;
            Draw.Quad(r);

            float progress = Utilities.Clamp(t / SettleDuration);
            Color colour = Utilities.Lerp(Colors.White, Colors.Black, Darkness());

            // The Insanity logo, centred at half the window's width (never wider than the image itself),
            // settling from slightly enlarged to its resting size while it fades in from black.
            var logo = Textures.UserInterface.Logo.Value;
            float width = MathF.Min(Window.Width * 0.5f, logo.Width);
            var size = new Vector2(width, width * logo.Height / logo.Width) * Utilities.Lerp(LogoStartScale, 1, progress);
            var centre = r.GetCenter();
            var min = centre - size / 2;

            Draw.Colour = colour;
            Draw.Image(logo, new Rect(min.X, min.Y, min.X + size.X, min.Y + size.Y), ImageContainmentMode.Contain);
        }
    }
}
