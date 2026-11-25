# EZ Video/Music Player

**Play Everything. See the Music. Control the Sound.**

A free Windows media player with a 50 x 50 spectrum analyzer, 20 visualizations, a professional equalizer, and media repair and conversion.

**⬇ [Download for Windows](https://github.com/DrSzaba/ez-video-music-player/releases/latest/download/EZ-Video-Music-Player-Setup.zip)** · 💚 Donate on Cash App: **$Szaba**

## What's new in 1.2.3
- **Play several videos at once.** Uncheck **VIDEOS: one at a time** (Start menu → **EZ Player - How files open**)
  and every video you open gets its own window, even while others are already playing.
- **Your choice sticks.** If you change the setting inside the player, opening the player again no longer switches it back.

## What's new in 1.2.1
- **Videos play one at a time.** A new video replaces the one that's playing, in the same window.
- **Songs go to the playlist.** Opening a song while the player is open adds it after the others.
- Change it any time: Start menu → **EZ Player - How files open**.

## Updates
The player updates itself. It checks
`releases/latest/download/EZ-Video-Music-Player-Setup.zip`, so every release must have that zip attached
(containing the `Setup` .exe).

## What's in this repository
- `index.html` – the download page
- `launcher/EZPlayerLauncher.cs` – the 1.2.3 launcher that decides how files open
  (build: `mcs -target:winexe -sdk:4.5 -out:EZPlayer.exe -win32icon:installer/app.ico -r:System.Windows.Forms.dll -r:System.Drawing.dll launcher/EZPlayerLauncher.cs`)
- `installer/installer.nsi` – the NSIS installer script

The core player program (`EZPlayerCore.exe`, version 1.2.0, C++ with FFmpeg, SDL2 and Dear ImGui) is included in the release installer.

Free software under the GNU GPL v3 – see `LICENSE`.
