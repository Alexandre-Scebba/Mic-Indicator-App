# Mic-Indicator-App

# Mic Indicator

> A tiny Windows tray & cursor‐follow mic mute status indicator built with .NET 8.0.

---

## 🎉 Release v1.0.0 “First Pulse” (2025-05-15)

**FEATURES**  
- **Single‐file EXE** – no installer or extra files; just grab `MuteIndicator.exe` and run.  
- **Live Mic Status** – a little box follows your cursor, pulsing **green** when live, **red** when muted.  
- **Context Menu Toggles** – right-click the tray icon to Mute/Unmute, turn Sound On/Off, Enable “Launch on Startup,” and adjust Transparency.  
- **Audible Feedback** – Windows native sounds for mute/unmute (toggleable via the tray menu).  
- **Auto-Resume** – your “Launch on Startup” choice and opacity setting persist between sessions.  
- **Quick Toggle** – left-click the tray icon to instantly mute or unmute.  
- **Hide Indicator** – option to hide the floating box entirely if you just need a tray-only indicator.

---

## 📥 Download

Download the standalone Windows build (no prerequisites):

[🔽 MuteIndicator v1.0.0 (Windows x64)](https://github.com/user-attachments/assets/fbf0ffeb-d36a-4eda-be86-c573a7f01618)

---

## 💻 Installation & Usage

1. **Download** `MuteIndicator.exe` from the link above.  
2. **Double-click** the EXE to launch the app.  
3. A small colored box will follow your cursor indicating mic status.  
4. **Right-click** the tray icon for settings and toggles.  
5. **Left-click** the tray icon to quickly mute/unmute.  

### Requirements

- Windows 10 or 11  
- A functioning microphone  

---

## 🎨 Screenshots/Demo

![GIF-Demo](https://github.com/user-attachments/assets/2bde22ec-b3e3-49fc-802a-93d1134c648f)


---

## 🛠️ Build from Source

```bash
git clone https://github.com/Alexandre-Scebba/Mic-Indicator-App.git
cd Mic-Indicator-App
```

# Publish a self-contained single-file EXE:
```bash
dotnet publish -c Release -r win-x64 \
  --self-contained true \
  /p:PublishSingleFile=true \
  -o ./bin/Release/net8.0/win-x64



