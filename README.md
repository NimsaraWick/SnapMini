# ✨ SnapMini — Intelligent Desktop AI Assistant

**SnapMini** is a lightweight, high-performance Windows desktop AI assistant built with C# and .NET 8 WPF. It runs quietly in the system tray and provides instant AI answers for **highlighted text** or **screen screenshots** using global keyboard shortcuts.

Powered by native **Windows OCR** and support for **Google Gemini**, **Groq Cloud**, and **OpenRouter AI Gateway**, SnapMini delivers answers in a modern, frameless, auto-dismissing dark SaaS popup.

---

## 🌟 Key Features

* ⌨️ **Global Shortcuts System**:
  * **`Ctrl + Alt + A`**: Highlight any text in your browser, PDF, or editor -> press `Ctrl+Alt+A` -> Get instant AI answer!
  * **`Ctrl + Alt + S`**: Press `Ctrl+Alt+S` -> Windows Snipping Tool opens -> snip any screen area -> Instant OCR + AI answer!
* 👁️ **Native Windows OCR (`Windows.Media.Ocr`)**: High-speed, on-device text recognition with zero 3rd-party binary dependencies.
* 🤖 **Multi-Provider AI Engine (Gemini, Groq & OpenRouter)**:
  * **Google Gemini**: `Gemini 2.5 Flash`, `Gemini 1.5 Flash`, `Gemini 1.5 Pro`
  * **Groq Cloud**: `Llama 3.3 70B`, `DeepSeek R1 Distill 70B`, `Mixtral 8x7B`, `Gemma 2 9B`
  * **OpenRouter Free Models**: `OpenAI GPT-OSS 20B`, `NVIDIA Nemotron 3 Ultra 550B`, `NVIDIA Nemotron 3 Super 120B`
* ⚙️ **In-App Settings Window (`SettingsWindow.xaml`)**:
  * Easily switch Providers & Model Versions using dark-themed dropdown controls.
  * **Hidden API Keys**: Input fields are secured with `PasswordBox` and interactive `👁️` Show/Hide eye toggles.
  * **Customizable System Prompt**: Edit the prompt template sent before your text/OCR input, or click "Reset Default" anytime.
* 🎨 **Premium Obsidian & Dual-Gradient Dark UI (`AnswerWindow.xaml`)**:
  * Frameless luxury obsidian glass UI (`#0A0A0E`) with `#8D3BF0` Electric Purple and `#0B33D3` Deep Royal Blue dual-gradient accents, crisp white typography, and deep drop shadows.
  * **Screen Position & Mode Selector**: Position the popup at `Top-Left ↖`, `Top-Right ↗`, `Center ⊹`, `Bottom-Left ↙`, `Bottom-Right ↘`, or toggle **`Full Screen ⛶`**. Choice is saved directly in `appsettings.json`.
  * **Pause / Resume Timer**: Freeze or resume the auto-dismiss countdown timer anytime using the button or pressing <kbd>Space</kbd>.
  * **One-Click Copy**: Copy AI answers to your clipboard with instant feedback.
* 📌 **System Tray Integration**: Custom **`SMini_icon`** in the Windows Taskbar tray with right-click Settings and Exit controls.

---

## ⌨️ Shortcuts Reference

### Global Shortcuts (System-wide)
| Shortcut | Action | Description |
| :--- | :--- | :--- |
| **`Ctrl + Alt + A`** | **Selected Text Q&A** | Automatically copies highlighted text from any application/browser and queries AI. |
| **`Ctrl + Alt + S`** | **Screenshot Q&A** | Automatically launches Windows Snipping Tool (`ms-screenclip:`), extracts text via OCR, and queries AI. |

### Popup Shortcuts (Answer Window)
| Shortcut | Action | Description |
| :--- | :--- | :--- |
| **`Space`** or **`P`** | **Pause / Continue Timer** | Toggles the auto-close countdown timer pause/resume state. *(Ignored when typing in the follow-up chat input box)* |
| **`F11`** or **`F`** | **Toggle Full Screen** | Expands the answer popup to full screen or restores default window dimensions. |
| **`Esc`** | **Close Window** | Instantly closes the answer popup with a smooth fade-out animation. |
| **`Enter`** | **Send Chat Message** | Submits follow-up questions when typing in the chat input box. |
| **`Shift + Enter`** | **New Line** | Inserts a new line in the chat input box without submitting. |

---

## ⚙️ Configuration & Setup (`appsettings.json`)

All configuration parameters (API Keys, Active Model, System Prompt, and Screen Position) are stored in **`appsettings.json`**:

```json
{
  "Provider": "Gemini",
  "GeminiModel": "gemini-2.5-flash",
  "GroqModel": "llama-3.3-70b-versatile",
  "OpenRouterModel": "openai/gpt-oss-20b:free",
  "GeminiApiKey": "your_gemini_api_key_here",
  "GroqApiKey": "your_groq_api_key_here",
  "OpenRouterApiKey": "your_openrouter_api_key_here",
  "SystemPrompt": "The following text was captured from the user's screen or selected text...",
  "WindowPosition": "TopRight"
}
```

> 💡 **Tip:** You don't need to edit JSON manually — open the Settings window (⚙️) inside SnapMini or from the System Tray to change options visually!

---

## 🚀 Running & Publishing

### Prerequisites
1. Windows 10 or 11
2. [.NET 8 SDK](https://dotnet.microsoft.com/download)

### Run from Terminal
```powershell
cd /path/to/SnapMini
dotnet run
```

### Publish Standalone `.exe`
To build a single standalone `SnapMini.exe` executable with embedded custom branding (`SMini_icon_`):

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
│   ├── AIService.cs           # Central AI Dispatcher & appsettings.json Manager
│   ├── GeminiService.cs       # Google Gemini REST API client
│   ├── GroqService.cs         # Groq OpenAI-compatible REST API client
│   ├── OpenRouterService.cs   # OpenRouter AI Gateway REST API client
│   └── OcrService.cs          # Native Windows.Media.Ocr text extraction
├── 📁 Views/
│   ├── AnswerWindow.xaml      # Modern dark SaaS popup layout (660px width, pause control)
│   ├── AnswerWindow.xaml.cs   # Screen position picker engine & timer logic
│   ├── SettingsWindow.xaml    # Dark SaaS Settings UI (Provider, Models, Keys, Prompt)
│   └── SettingsWindow.xaml.cs # Settings UI event handling & password eye toggles
├── 📁 Images/                 # SMini.png, SMini.png, SMini_icon.ico
├── ⚙️ App.xaml & App.xaml.cs  # Application entry point & System Tray setup
├── 🔑 appsettings.json        # Unified application settings file
└── 📦 SnapMini.csproj         # .NET 8 Project file
```

---

## 🛡️ License & Credits

Built with C# .NET 10 WPF. Native OCR provided by `Windows.Media.Ocr`. AI Inference powered by Google Gemini API, Groq Cloud & OpenRouter.
