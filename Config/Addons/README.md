# 🧩 OptiAssist Add-ons & Modular DLL Plugins (`Config/Addons`)

This directory stores modular `.dll` plugins and extensions for **OptiAssist**.

## How It Works

Each module lives in its own subdirectory inside `Config/Addons/<ModuleName>/` and consists of a compiled .NET library (`<ModuleName>.dll`) that implements `OptiAssist.IOptiAssistAddon`:

```text
Config/
└── Addons/
    └── SystemCleaner/
        └── SystemCleaner.dll
```

1. **Dynamic Discovery & Non-Locking Load**:
   When OptiAssist starts (or when you click **Reload Modules** in **Settings**), `OptiAssist.exe` scans `Config/Addons` for `.dll` assemblies, loads their bytes into memory (so files on disk are never locked), and initializes every class implementing `IOptiAssistAddon`.
2. **First-Class Sidebar Integration**:
   Every enabled module automatically appears as a native navigation tab in the left sidebar menu (`pnlSidebarAddons`). Clicking a module renders its `UIElement` view (`CreateView()`) directly inside the main window.
3. **Settings Management**:
   In **Settings → Modular Add-ons & Plugins**, you can:
   - **➕ Add Module (.dll)**: Import an external `.dll` plugin into `Config/Addons/<ModuleName>/`.
   - **Active Checkbox**: Enable or disable any installed module (persisted in `Config/settings.json` under `DisabledAddons`).
   - **🗑️ Remove**: Delete a module's `.dll` and remove it from the sidebar immediately.

## Built-in Modules

- **`SystemCleaner` (`Config/Addons/SystemCleaner/SystemCleaner.dll`)**:
  Full-featured System Cleaner & Optimizer compiled as a standalone plugin DLL (`src/Addons/SystemCleaner/SystemCleaner.csproj`), encapsulating its entire WPF user interface, scanning engine, Win32 P/Invoke definitions (`SHEmptyRecycleBin`, `SHQueryRecycleBin`, `DnsFlushResolverCache`), and 15 cleanup categories.
