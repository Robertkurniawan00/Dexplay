# Dexplay

A high-performance native Windows desktop audio player, dynamic island widget, and DJ mashup studio built with C# (.NET Framework / Win32 / GDI+) and Windows Media Foundation.

---

## Overview

Dexplay is a lightweight desktop audio workstation and player engineered as a standalone Windows process with zero dependencies on web runtimes, Chromium, Electron, or background servers. It combines a minimalist, top-anchored dynamic island notch with an expanded studio deck featuring visualizers, synchronized lyrics, queue controls, and an integrated dual-deck Pro Mashup Studio.

### Core Highlights
- **Zero Web Runtime Overhead**: Built using pure Win32 APIs, GDI+ vector rendering, and Windows Media Foundation (`MediaPlayer` + `IMFSourceReader`). Typical working set memory remains under 35–45 MB.
- **Crystal Clear Glassmorphic Design**: Custom vector-drawn translucent surfaces, multi-stop linear gradients, specular highlights, and adjustable glass clarity.
- **Windows 10/11 Smart App Control (SAC) & High-DPI Ready**: Standardized application manifest (`app.manifest`) with `PerMonitorV2` DPI awareness and Windows 10/11 execution compatibility.
- **Native Audio Interop**: Direct P/Invoke integration with C99 (`title_cleaner.dll`) and C++20 audio inspection (`audio_probe.exe`).
- **DASH M4A Fixer**: Custom in-memory container parser that repairs YouTube DASH `edts` atom timestamp offsets on fragmented streams for seamless scrubbing.

---

## Features

### 1. Interface Modes
- **Compact Dynamic Island Notch (460 × 46 px)**:
  - Top-anchored or freely draggable floating island widget.
  - Vector glass controls: Play/Pause, Search, Deck Toggle, Window Pin, and Close.
  - Real-time animated mini spectrum equalizer bars.
- **Expanded Command Deck (550 × 480 px)**:
  - **DECK**: Visualizer selection (Spectrum Analyzer, Stereo Mirror, Oscilloscope, Dot Matrix), rotating vinyl album art, smooth scrub bar with drag preview, repeat modes, and volume slider.
  - **SEARCH**: YouTube Music query engine filtered for official studio releases (`- Topic` and verified artist channels).
  - **LYRICS**: Real-time synchronized lyrics via LRCLIB with automatic smooth scrolling and click-to-seek playback.
  - **QUEUE**: Drag-free track management with instant track remove (`×`), batch queue clear (`CLEAR`), keyboard deletion, and right-click context menu.
  - **EQ**: 5-band graphic equalizer (60Hz, 250Hz, 1kHz, 4kHz, 12kHz), acoustic bass boost, playback speed control (0.5x – 1.5x), theme selector, and glass clarity slider.
  - **MASHUP**: Professional dual-deck DJ mashup studio with audio decoding, autocorrelation BPM detection, interactive waveform scrubbing, dynamic vocal ducking, and studio WAV export.

---

### 2. Pro Mashup Studio Engine

Dexplay includes an in-app dual-deck mashup workstation designed for real-time live mixing and offline studio rendering:

- **Hardware-Accelerated Decoding**: Utilizes `mfreadwrite.dll` (`IMFSourceReader`) to decode formats (MP3, M4A, AAC, WAV) into uncompressed 16-bit 44.1 kHz stereo PCM buffers.
- **Dual-Deck Architecture**:
  - **Deck A (Instrumental / Base Track)**: Independent gain, mute, cue point detection, and transport controls.
  - **Deck B (Acapella / Vocal Track)**: Independent gain, mute, cue point detection, and transport controls.
- **Waveform Analysis & Visualization**: Generates 140-bin interactive peak waveforms with real-time playhead scrubbing and cue markers (Intro, Drop, Outro).
- **Autocorrelation BPM Detection**: Estimates track tempos (BPM) and provides a 1-click **SYNC TO DECK A** tempo-matching mechanism.
- **Professional Crossfader**: Supports 4 mixing curves:
  - *Linear* (Gradual linear transition)
  - *Equal-Power* (Constant acoustic loudness across center mix)
  - *S-Curve* (Smooth exponential transition)
  - *DJ Hard Cut* (Instant trigger cut for live drops)
- **Dynamic Sidechain Ducking**: Automatically attenuates Deck A volume when Deck B audio energy exceeds threshold to keep vocals clear and punchy.
- **Studio Mixdown WAV Export**: Renders the complete multi-track crossfaded arrangement to a 16-bit 44.1 kHz stereo PCM `.wav` file with a non-linear `tanh` soft-clip limiter to eliminate digital clipping.

---

### 3. Glassmorphism & UI Themes

Dexplay features a fully procedural GDI+ design system with customizable glass clarity (86% – 100% opacity) and 6 built-in themes:

| Theme | Description | Accent Color |
| --- | --- | --- |
| **Crystal Clear** *(Default)* | Frosted ice glass with cyan neon highlights | `#00E5FF` |
| **Cyber Neon** | High-contrast synthwave aesthetic | `#00FFA3` |
| **Royal Violet** | Deep ultraviolet glass with magenta accents | `#BD00FF` |
| **Emerald Forest** | Refined dark jade and mint glow | `#00E676` |
| **Rose Gold** | Warm champagne and rose titanium | `#FF4081` |
| **Midnight Dark** | Pure OLED minimalist carbon finish | `#64B5F6` |

---

## Keyboard Shortcuts

### Global Hotkeys
| Shortcut | Action |
| --- | --- |
| `Ctrl + Alt + Space` | Play / Pause |
| `Ctrl + Alt + Right` | Next Track |
| `Ctrl + Alt + Left` | Previous Track |
| `Ctrl + Alt + Up` / `Down` | Volume Up / Down |
| `Ctrl + Alt + E` | Open Equalizer & Settings |
| `Ctrl + Alt + T` | Cycle Color Themes |
| `Ctrl + Alt + B` | Toggle Acoustic Bass Boost |

### Queue Shortcuts
| Shortcut | Action |
| --- | --- |
| `Delete` / `Backspace` | Remove selected item from queue |
| `Right Click` on item | Open Context Menu (Play Now, Remove, Clear Queue) |

---

## Project Structure

```
MUSIC WIDGET/
├── assets/
│   ├── app.manifest           # Windows 10/11 compatibility & PerMonitorV2 DPI manifest
│   ├── dexplay.ico            # High-resolution application icon
│   └── dexplay_logo.png       # Vector-aligned project logo
├── native/
│   ├── audio_probe.cpp        # C++20 audio metadata & container inspector
│   ├── audio_probe.exe        # Compiled inspection utility
│   ├── title_cleaner.c        # C99 track title & artist string sanitizer
│   └── title_cleaner.dll      # Native interop DLL
├── scripts/
│   ├── build_pure_widget.ps1  # Automated build script with manifest & signing support
│   └── Launch_Widget.ps1      # Launcher with Smart App Control (SAC) diagnostics
├── src/
│   └── Dexplay.cs             # Core application source (.NET / Win32 / GDI+)
├── Dexplay.exe                # Precompiled standalone binary
├── settings.json              # Local persistent user preferences
└── README.md                  # Documentation
```

---

## Getting Started

### Prerequisites
- Windows 10 (version 1803+) or Windows 11.
- Built-in Microsoft .NET Framework 4.7.2+ (included by default on Windows 10 and 11).

### Running Dexplay
Run the standalone executable directly:
```cmd
Dexplay.exe
```
Or launch via the diagnostic PowerShell runner:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\Launch_Widget.ps1
```

### Building from Source
To compile `src\Dexplay.cs` with the embedded `app.manifest` and icon:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\build_pure_widget.ps1
```

*(Optional)* Sign with an Authenticode certificate for enterprise or strict Smart App Control environments:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\build_pure_widget.ps1 -CertThumbprint "<THUMBPRINT>"
# or
powershell -ExecutionPolicy Bypass -File scripts\build_pure_widget.ps1 -PfxPath "cert.pfx" -PfxPassword "pass"
```

---

## License
Dexplay is distributed for personal and developer desktop audio usage. Built with native Windows engineering standards.
