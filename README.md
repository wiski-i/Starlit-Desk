# SnowDesk for Windows

SnowDesk adds offline snowfall to your desktop. Snow builds up on visible window edges and at the bottom of each screen.

## Run

Open `build\SnowDesk.exe` on Windows 10 or 11 with .NET Framework 4.8 or later. Find the snowflake in the system tray, including under the hidden-icons arrow. Exit the previous version before starting a new release.

- Left-click the snowflake to pause or resume. Sweep the pointer across settled snow to scatter it.
- Right-click for density, wind, decorations, frame rate, startup, and exit.
- Small icicles grow under visible window rims and break when they get too long or their window moves abruptly. Toggle them from **Decorations > Icicles**.
- Santa leaves a heel and toe impression where each boot lands. Deep snow kicks up a little powder. A landed tree compresses the snow beneath its trunk, and new snow slowly buries its base.
- The plow slows down in deep snow. Light powder scatters to either side and excess snow tumbles off a full blade. Snow pours off window edges, and the plow carries it out of view at the right edge of the screen. Wind gradually moves loose surface snow along the ledges.
- Open **Trigger Effect** to drop a tree, start Santa's walk or sleigh, send the plow or gondola, or trigger a gust of wind.
- Click and drag a tree directly. Release it to drop or toss it. A quick cursor sweep pushes both settled snow and nearby falling flakes.
- **Snow on Windows and Menus** controls accumulation on window rims, the taskbar, native popup menus, Start, and the hidden-icons flyout when Windows exposes the panel bounds.
- Choose **Eco (20 FPS)**, **Balanced (30 FPS)**, or **Smooth (60 FPS)** to set the frame-rate cap. Balanced is the default.
- Enable **Start with Windows** from the tray if you want automatic startup for your account. Turn it off before moving the executable to another folder.

Each flake adds 50% more settled snow at the same snowfall density, with a 50% higher depth limit. The snow drifts follow the window frame and its estimated corner radius. Dragging a window dislodges part of its snow in the direction of motion. Resizing it preserves the snow above the supported edge. Dislodged snow can settle on another window or at the bottom of the screen.

SnowDesk reads window geometry through Windows APIs. It creates no network connections and captures no screen content. Preferences are stored in `%LOCALAPPDATA%\SnowDeskWindows\settings.ini`. Settled snow melts slowly at its base while the remaining layer settles onto the frame. Thin cover lasts longer. Deep piles melt faster to keep the desktop readable. Melting stops when you pause.

Unchanged snow contours reuse a small local drawing cache. One reusable drawing surface feeds all active monitors. Pausing stops the frame timer, releases that surface, and unregisters window tracking until you resume.

## Build

From this directory, run the script with PowerShell. It uses the 64-bit .NET Framework C# compiler included with Windows and writes `build\SnowDesk.exe`.

```powershell
.\build.ps1
```

## Artwork

DecorationArt draws the models with GDI+ paths, strokes, and jointed movement. The tree's silhouette defines its mouse target. Snowflakes and the rest of the overlay pass clicks to the desktop.

## Reference package

The behavior reference was the DADA Creative Snowdesk 1.3 disk image and its bundled guide. The Windows implementation has its own snow simulation, Windows integration, and artwork.

REA 6.7.0 recorded the disk image and identified the extracted executable as universal Mach-O. DMG evidence is `ev_313e0e7a51347a4aa6b901f8d77369ce43333d49f28abba99e3952741a013bae`. Executable evidence is `ev_62d7f00a7d104dfc94c2aecee17c865be9a99d9ce5f6fb563e6489293e3b4522`. The bundled guide supplied the feature list. The analysis was static, with macOS runtime behavior outside its coverage.
