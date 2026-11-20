<div align="center">
  <img src="branding/StarlitDesk.png" alt="Starlit Desk logo" width="92" height="92">
  <h1>Starlit Desk</h1>
  <p><strong>Seasonal effects for your Windows desktop: winter, spring, summer, autumn and space.</strong></p>
  <p><strong>Language:</strong> <a href="README.md">EN</a> | <a href="README.tr.md">TR</a> · <a href="#quick-start">Quick start</a> · <a href="#build-from-source">Build</a></p>
  <p>
    <img src="https://img.shields.io/badge/Windows-10%20%2F%2011-69304e?style=flat-square" alt="Windows 10 or 11">
    <img src="https://img.shields.io/badge/.NET%20Framework-4.8%2B-9c607e?style=flat-square" alt=".NET Framework 4.8 or later">
    <img src="https://img.shields.io/badge/Themes-Winter%20%C2%B7%20Spring%20%C2%B7%20Summer%20%C2%B7%20Autumn%20%C2%B7%20Space-e7a0b9?style=flat-square" alt="Five desktop themes">
  </p>
</div>

## Windows interface

<p align="center"><img src="docs/images/windows.png" alt="Starlit Desk control panel in Turkish with the Winter theme selected, snowfall controls and an effect launcher" width="800"></p>

The Turkish control panel uses the burgundy and rose palette of AI Usage Hub. The theme selector lists five themes in season order: **Kış** (Winter), **İlkbahar** (Spring), **Yaz** (Summer), **Sonbahar** (Autumn) and **Uzay** (Space). Each theme has its own settings card and effect launcher.

## Quick start

1. Build the app on Windows 10 or 11 with .NET Framework 4.8 or later, then open **StarlitDesk.exe** in the `build` folder. Close older SnowDesk instances before launching.
2. Use the control panel to choose a desktop theme, change the density and frame rate, or trigger an effect. Changes are saved locally.
3. Click the Starlit Desk icon beside the clock to reopen the panel. Windows may place it under **^**. Right-click for quick settings, pause or **Çıkış** (Quit).
4. Enable **Windows ile başlat** to start in the tray when you sign in. Turn it off before moving the executable.

## Features

| Area | Behavior |
| --- | --- |
| **Winter** | Snow settles on window edges and at the bottom of each screen. Moving windows can dislodge the snow. |
| **Spring** | Cherry blossom petals drift and settle. Flowers grow in small beds while butterflies, bees and swallows move around them. A cherry tree drops in with a hive. A car can crush the fallen hive, the angry swarm chases it, and a beekeeper hangs a new hive. |
| **Summer** | Rain collects on window tops and runs off their edges. Puddles form on the taskbar edge, and a drainage truck pumps them dry. A bather floats in a swim ring, and a rainbow follows the shower. |
| **Autumn** | Leaves tumble down and pile up. An autumn tree sheds leaves and acorns, and a squirrel snatches the acorns. A gardener rakes a heap for a child to jump into, and a street sweeper fills his bin. Occasional gales strip the tree and blow it away. |
| **Space** | A Milky Way band with a crescent planet. A satellite, meteors and an aurora pass by. Occasional events include a supernova and its remnant, a ringed gas giant, an asteroid collision, a comet and an astronaut on a spacewalk. The sky stays behind windows by default. |
| **Interaction** | Sweep the pointer to scatter snow, leaves, petals or cosmic dust. Shake a window to throw off what has settled on it. Drag and release a winter tree to drop or toss it. |
| **Decorations** | Toggle icicles, trees, Santa, the sleigh, the snowplow and the gondola. Launch them on demand, send a gust or clear the trees. |
| **Displays** | Turn the effect on or off for each monitor from the panel or the tray menu. |
| **Performance** | Choose 20, 30 or 60 FPS. Repeated artwork is cached as bitmaps. Pausing stops the frame timer and releases the shared render surface. |
| **Tray** | Close the panel to keep the current effect running. Choose **Çıkış** to exit. |
| **Privacy** | The app runs offline and places effects using window geometry from Windows APIs. |

Starlit Desk keeps existing preferences in `%LOCALAPPDATA%\SnowDeskWindows\settings.ini`.

## Build from source

```powershell
git clone https://github.com/wiski-i/Starlit-Desk.git
cd Starlit-Desk
.\build.ps1
.\build\StarlitDesk.exe
```

The build uses the 64-bit C# compiler included with .NET Framework on Windows. The logo is embedded in the executable for the panel, title bar and notification area.

## Adding a desktop theme

Create a `DesktopScene` implementation and give it a catalog entry in [src/DesktopTheme.cs](src/DesktopTheme.cs). Each entry uses a stable ID, a display name, a description and a factory that creates one scene per monitor. Include the entry in `DesktopTheme.All` to show it in the selector. Keep the ID stable to preserve saved selections. Missing or older theme selections resolve to Winter.

To add a live preview, supply an optional `ThemePreview` factory in the catalog entry. The Winter preview runs the snowfall simulation over two miniature windows and reflects the current density, wind and accumulation settings. The card animates while the control panel is in the foreground and the effect is running.

| Path | Purpose |
| --- | --- |
| [src/DeskWindow.cs](src/DeskWindow.cs) | Control panel and settings |
| [src/DeskStyle.cs](src/DeskStyle.cs) | Burgundy palette and themed controls |
| [src/SnowField.cs](src/SnowField.cs) | Winter scene and snow simulation |
| [src/SeasonScene.cs](src/SeasonScene.cs) | Spring and autumn scene: falling and settling pieces |
| [src/SummerScene.cs](src/SummerScene.cs) | Summer scene, rain and puddles |
| [src/SpaceScene.cs](src/SpaceScene.cs), [src/SpaceEvents.cs](src/SpaceEvents.cs) | Space sky and its events |
| [branding/](branding/) | PNG logo and multi-size Windows icon |

## Credits

The Windows snowfall implementation started in this repository as SnowDesk for Windows. DADA Creative's *Snowdesk 1.3* and its bundled guide were the original behavior reference.
