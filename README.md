# Dexplay

A native Windows desktop audio player and dynamic island widget built with C# (.NET / Win32 / GDI+) and Windows Media Foundation.

## Overview

Dexplay is a lightweight desktop music player that runs as a standalone Windows process without web runtimes, browser engines, or background web servers. It features a compact, top-anchored notch/island interface that expands into a full control deck.

### Technical Characteristics
- **Pure Native Execution**: Built using Win32 APIs, GDI+ vector rendering, and Windows Media Foundation (`MediaPlayer`).
- **Low Memory Footprint**: Typical working set memory remains under 35 MB.
- **Native Interop**: Direct P/Invoke integration with C99 (`title_cleaner.dll`) and a C++20 inspection utility (`audio_probe.exe`).
- **DASH M4A Fixer**: Built-in container parser that repairs YouTube DASH `edts` atom offsets on fragmented audio streams for smooth seeking.

---

## Features

### Interface Modes
- **Compact Notch / Floating Island (460 × 46 px)**:
  - Top-anchored or draggable floating island.
  - GDI+ vector controls: Play/Pause, Search, Deck toggle, Pin, and Close.
  - Real-time mini equalizer bar indicator.
- **Expanded Command Deck (550 × 450 px)**:
  - **Deck**: Visualizer (Spectrum, Stereo Mirror, Oscilloscope, Dot Matrix), rotating vinyl art, scrub bar with drag preview, repeat modes, and volume control.
  - **Search**: YouTube Music query engine filtered for official audio releases (`- Topic` and verified label channels).
  - **Lyrics**: Synced lyrics via LRCLIB with auto-scroll and click-to-seek.
  - **Queue**: Reorderable playback queue.
  - **Equalizer**: 5-band graphic equalizer (60Hz, 250Hz, 1kHz, 4kHz, 12kHz), acoustic bass boost, playback speed control (0.5x – 1.5x), and 5 UI themes.

### Global Shortcuts
| Shortcut | Action |
| --- | --- |
| `Ctrl + Alt + Space` | Play / Pause |
| `Ctrl + Alt + Right` | Next Track |
| `Ctrl + Alt + Left` | Previous Track |
| `Ctrl + Alt + Up` / `Down` | Adjust Volume |
| `Ctrl + Alt + E` | Open Equalizer |
| `Ctrl + Alt + T` | Cycle Themes |
| `Ctrl + Alt + B` | Toggle Bass Boost |

---

## Getting Started

### Running the Application
Launch the precompiled executable directly:
```cmd
Dexplay.exe
```
Or launch using PowerShell:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\Launch_Widget.ps1
```

### Building from Source
Prerequisites: Windows 10/11 (uses built-in .NET Framework `csc.exe`).

Run the build script:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\build_pure_widget.ps1
```
The script compiles `src\Dexplay.cs` directly into `Dexplay.exe`.
