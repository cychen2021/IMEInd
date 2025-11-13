# IMEInd Utility Tool

A Windows utility that detects and displays the current Input Method Editor (IME) status when an editable control receives focus.

## Features

- **Auto-detection**: Monitors UI Automation focus changes to detect when editable controls are focused
- **IME Status Display**: Shows a toast notification with current IME (EN, CHS, CHT, JA, KO, etc.)
- **Smart Positioning**: Toast appears near the text cursor or control location
- **Lightweight**: Runs in the background with minimal resource usage (120ms polling interval)

## Requirements

- Windows OS
- .NET 8.0 or later

## Build

```powershell
dotnet build -c Release
```

## Run

```powershell
.\bin\Release\net8.0-windows\IMEInd.exe
```

Or simply:

```powershell
dotnet run -c Release
```

## How It Works

1. Uses UI Automation API to monitor focus changes across all applications
2. Filters for editable controls (Edit, Document, or controls supporting ValuePattern/TextPattern)
3. Polls keyboard layout (HKL) every 120ms to detect IME changes
4. Displays a semi-transparent toast overlay for 800ms showing the current IME status

## Technical Details

- **Framework**: .NET 8.0 Windows Forms + WPF (for UI Automation)
- **UI Automation**: Monitors `AutomationFocusChangedEvent` system-wide
- **IME Detection**: Uses Win32 API (`GetKeyboardLayout`) to identify active input method
- **Display**: Topmost, borderless WinForms window with auto-hide timer

## Future Enhancements

- Add TSF (Text Services Framework) integration for detailed IME names (e.g., "Microsoft Pinyin" vs "CHS")
- Per-monitor DPI awareness
- Application whitelist/blacklist
- Customizable toast appearance (colors, fonts, duration)
- Fade-in/out animations

## Attribution

- Tray icon: See [licenses/icon.pdf](licenses/icon.pdf) for icon attribution and license information.
