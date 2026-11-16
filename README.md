# Insanity Interactive Reloaded

Insanity Interactive Reloaded is a fork by Zeph of [Madness Interactive Reloaded](https://github.com/studio-minus/madness-interactive-reloaded), the fan game by Studio Minus based on Madness Interactive by Flecko. This fork adds full XInput controller support, menu navigation with a controller and rebindable pad buttons. Report bugs and contribute at <https://github.com/ZephKrnl/Insanity-Interactive-Reload/issues>.

Current version: 0.1.0

## Controller support
Any XInput controller (Xbox, and most others) works out of the box and can be rebound in the settings. The default layout:

| Action | Button |
| --- | --- |
| Move left / right | Left stick |
| Jump / dodge | A |
| Interact | Y |
| Attack | Right trigger |
| Melee | B |
| Block / aim | Left trigger |
| Throw | X |
| Ability 1 | Left bumper |
| Ability 2 | Right bumper |
| Ability 3 | D-pad left |
| Ability 4 | D-pad right |
| Zoom | D-pad up / down |
| Pause | Start |

## Building the game
- Make sure you have
    - Windows 10 or 11 or Linux
    - git
    - [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Clone this repository
- Initialise the submodules
    ```shell
    git submodule init
    git submodule update --remote
    ```
- Build the solution
    ```shell
    dotnet build src/MIR.sln
    ```
- Run the game from `src/MadnessInteractiveReloaded` with `dotnet run`, or open `src/MIR.sln` in Visual Studio 2022 and press F5.

The executable is called `IIR.exe`. Saves, settings and mods live in `%LOCALAPPDATA%\IIR`. On first start the game copies them over from `%LOCALAPPDATA%\MIR`, the folder older builds (named `MIR.exe`) used, and leaves that folder as it was. Mods built against `MIR.dll` still load.

## Credits and attribution
- Insanity Interactive Reloaded by Zeph.
- Based on [Madness Interactive Reloaded](https://github.com/studio-minus/madness-interactive-reloaded) by Studio Minus: programming by zooi and duston, art and animation by Dikkiedik and Koof, original soundtrack by Lothyde, and the contributors listed in the in-game credits.
- Based on Madness Interactive by Flecko (Max Abernethy) and Madness Combat by Krinkels.
- Third-party libraries and their licenses are listed in `src/MadnessInteractiveReloaded/NOTICE.txt`.

## License
This program is free software licensed under the GNU General Public License v3.0. See [LICENSE](LICENSE).
