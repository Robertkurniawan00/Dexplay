# Dexplay — Desktop Dynamic Music Island & Audio Player

<div align="center">

**Standalone Native Windows Desktop Application (`Dexplay.exe`)**  
*Direct Win32 / GDI+ / Windows Media Foundation • Graphic Equalizer • YouTube Music Official Audio • LRCLIB Synced Lyrics • 5 Themes*

[![Windows](https://img.shields.io/badge/Platform-100%25%20Pure%20Windows%20Widget-0078D6.svg)](#)
[![C# .NET](https://img.shields.io/badge/Core-C%23%20%2F%20Win32%20API-512BD4.svg)](#)
[![C Standard](https://img.shields.io/badge/DSP-C99%20DLL-00599C.svg)](#)
[![C++ Standard](https://img.shields.io/badge/Probe-C%2B%2B20%20Binary-004482.svg)](#)

</div>

---

## 🌟 100% Pure Native Windows Desktop Music Island

**Dexplay** ([Dexplay.exe](file:///d:/MUSIC%20WIDGET/Dexplay.exe)) adalah aplikasi pemutar musik desktop native Windows berkecepatan tinggi:
- **Bukan Web App**: Zero Electron, zero browser, tidak memakan resource RAM berlebih (<35 MB).
- **Tanpa Web Server**: Tidak memerlukan localhost, Node daemon, atau port HTTP.
- **Eksekusi Biner Mandiri**: Berjalan langsung sebagai proses Windows native dengan Windows Media Foundation (`PresentationCore` / `MediaPlayer`).
- **Direct P/Invoke & Native Integration**: Memanggil DLL C99 (`title_cleaner.dll`) secara biner langsung dan binary C++20 (`audio_probe.exe`) untuk inspeksi audio lokal.

---

## 🔍 Fitur Utama Dexplay

1. **Dual-State Dynamic Notch & Island**:
   - **Collapsed Notch Bar (460x46 px)** & **Floating Island Mode**: Tombol vektor GDI+ anti-aliased (Play, Pause, Search, Expand, Island, Pin, Close) dan mini equalizer indicator.
   - **Expanded Command Deck (550x450 px)** dengan 5 tab:
     - **DECK**: Visualizer 4-mode (*Spectrum*, *Stereo Mirror*, *Oscilloscope*, *Dot Matrix*), vinyl spinning disc interaktif, scrub bar dengan real-time live preview saat dragging, loop, volume + mute.
     - **YT MUSIC**: Pencarian lagu YouTube Music khusus **Official Audio** (memprioritaskan channel `- Topic` & label resmi, menyaring MV, fancam, live broadcast, kompilasi, karaoke, cover, dan spam `#`), dengan dukungan penuh Unicode (Hangul/Kanji/Latin).
     - **LIRIK**: Lirik tersinkronisasi LRCLIB dengan auto-scroll dan *click-to-seek*.
     - **QUEUE**: Antrean lagu interaktif dengan tombol pindah urutan, hapus, dan play.
     - **EQUALIZER**: 
       - **5-Band Graphic Equalizer** (60Hz Sub-Bass, 250Hz Bass, 1kHz Mid, 4kHz Presence, 12kHz Treble) dengan rentang -12 dB s/d +12 dB.
       - **Equalizer Presets**: *Flat*, *Bass Boost*, *Vocal Clarity*, *Treble Booster*, *Rock & Metal*, *Electronic/EDM*, *Pop & R&B*, *Lo-Fi Chill*, serta *Kustom*.
       - **Bass Boost Switch** (+6 dB acoustic boost).
       - **Tempo / Playback Speed** (0.5x s/d 1.5x) dengan tombol reset 100%.
       - **Pilihan Skin Tema Widget (Full Width 506px)**: 5 tema resmi (*Cyber Neon*, *Sunset Glow*, *Emerald Night*, *Cyberpunk 2077*, *Minimalist Monochrome*).

2. **DASH M4A Container Sanitizer (`SanitizeM4aEdts`)**:
   - Algoritma pembersih container MP4 berbasis C# murni yang secara otomatis memperbaiki atom `edts` YouTube pada file fragmented MP4, menjamin lagu dapat diputar mulus dan di-*seek* ke menit berapa pun tanpa error.

3. **Global Windows Hotkeys**:
   - `Ctrl + Alt + Space`: Play / Pause
   - `Ctrl + Alt + Right`: Next Track
   - `Ctrl + Alt + Left`: Previous Track
   - `Ctrl + Alt + E`: Buka Tab Equalizer
   - `Ctrl + Alt + T`: Ganti Tema / Skin
   - `Ctrl + Alt + B`: Toggle Bass Boost
   - `Ctrl + Alt + Up / Down`: Atur Volume

---

## 🛠️ Cara Menjalankan & Membangun

### Menjalankan Widget:
Cukup klik dua kali berkas:
- **`Dexplay.exe`** atau jalankan script **`scripts\Launch_Widget.ps1`**

### Mengompilasi Ulang Biner (Standalone C# Compiler):
```powershell
powershell -ExecutionPolicy Bypass -File scripts\build_pure_widget.ps1
```
Hasil kompilasi berupa biner mandiri `Dexplay.exe` berukuran ~98 KB tanpa dependensi eksternal.
