# ✨ SnapMini — Intelligent Desktop AI Assistant

**SnapMini** is a lightweight, high-performance Windows desktop AI assistant built with C# and .NET 8 WPF. It runs quietly in the system tray and provides instant AI answers for **highlighted text** or **screen screenshots** using global keyboard shortcuts.

Powered by native **Windows OCR** and support for both **Google Gemini 2.5 Flash** and **Groq Llama 3.3 70B**, SnapMini delivers answers in a modern, frameless, auto-dismissing dark SaaS popup.

---

## 🌟 Key Features

* ⌨️ **Global Shortcuts System**:
  * **`Ctrl + Alt + A`**: Highlight any text in your browser, PDF, or editor -> press `Ctrl+Alt+A` -> Get instant AI answer!
  * **`Ctrl + Alt + S`**: Press `Ctrl+Alt+S` -> Windows Snipping Tool opens -> snip any screen area -> Instant OCR + AI answer!
* 👁️ **Native Windows OCR (`Windows.Media.Ocr`)**: High-speed, on-device text recognition with zero 3rd-party binary dependencies.
* 🤖 **Dual AI Provider Engine**: Switch seamlessly between **Google Gemini** (`gemini-2.5-flash`) and **Groq** (`llama-3.3-70b-versatile`) via `appsettings.json`.
* 🎨 **Linear/Stripe-Inspired Dark UI**:
  * Frameless dark glass UI (`#0F172A`) with deep drop shadows.
  * **5-Way Screen Position Picker**: Position the popup at `Top-Left ↖`, `Top-Right ↗`, `Center ⊹`, `Bottom-Left ↙`, or `Bottom-Right ↘`. Automatically remembers your choice!
  * **Pause / Resume Timer**: Pause the 25-second auto-dismiss countdown anytime.
  * **One-Click Copy**: Copy AI answers to your clipboard with instant feedback.
* 📌 **System Tray Integration**: Custom **`SM_icon`** in the Windows Taskbar tray with right-click Exit controls.

---

## ⌨️ Shortcuts Reference

| Shortcut | Action | Description |
| :--- | :--- | :--- |
| **`Ctrl + Alt + A`** | **Selected Text Q&A** | Automatically copies highlighted text from any application/browser and queries AI. |
| **`Ctrl + Alt + S`** | **Screenshot Q&A** | Automatically launches Windows Snipping Tool (`ms-screenclip:`), extracts text via OCR, and queries AI. |

---

## ⚙️ Configuration & Setup (`appsettings.json`)

Create `appsettings.json` in the root folder to set your API Keys and choose your AI Provider:

```json
{
  "Provider": "Gemini",
  "GeminiApiKey": "your_google_gemini_api_key_here",
  "GroqApiKey": "your_groq_api_key_here"
}
```

* Set `"Provider": "Gemini"` to use **Google Gemini 2.5 Flash**.
* Set `"Provider": "Groq"` to use **Groq Llama 3.3 70B**.

---

## 🚀 Running the Project

### Prerequisites
1. Windows 10 or 11
2. [.NET 8 SDK](https://dotnet.microsoft.com/download)

### Run from Terminal
```powershell
cd C:\SnapMini #project location
dotnet run
```

---

## 📦 Publishing a Standalone `.exe`

To build a single standalone `SnapMini.exe` executable with embedded custom branding (`SM_icon`):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The published executable will be saved at:
`bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\SnapMini.exe`

### Launch Automatically on Windows Startup
1. Press `Win + R`, type `shell:startup` and press Enter.
2. Paste a shortcut to `SnapMini.exe` inside the Startup folder.
3. SnapMini will now launch automatically whenever Windows boots!

---

## 📂 Project Architecture

```text
SnapMini/
├── 📁 Helpers/
│   └── HotkeyManager.cs       # Win32 RegisterHotKey, keybd_event & DIB clipboard interop
├── 📁 Properties/
│   └── AssemblyInfo.cs        # Assembly & WPF theme metadata
├── 📁 Services/
│   ├── AIService.cs           # AI Provider Dispatcher (Gemini vs. Groq)
│   ├── GeminiService.cs       # Google Gemini REST API client
│   ├── GroqService.cs         # Groq OpenAI-compatible REST API client
│   └── OcrService.cs          # Native Windows.Media.Ocr text extraction
├── 📁 Views/
│   ├── AnswerWindow.xaml      # Modern dark SaaS popup layout
│   └── AnswerWindow.xaml.cs   # Position picker engine, timer & copy logic
├── 📁 Images/                 # SM_logo.png, SM_icon.png, SM_icon.ico
├── ⚙️ App.xaml & App.xaml.cs  # Application entry point & System Tray setup
├── 🔑 appsettings.json        # Secret API key & Provider configuration
└── 📦 SnapMini.csproj         # .NET 8 Project file
```

---

## 🛡️ License & Credits

Built with C# .NET 8 WPF. Native OCR provided by `Windows.Media.Ocr`. AI Inference powered by Google Gemini API & Groq Cloud.
