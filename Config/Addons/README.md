# OptiAssist Add-ons & Plugins System

OptiAssist supports modular add-ons and plugins stored in `Config/Addons/`. Each add-on is placed in its own folder and can be loaded dynamically at runtime.

## Directory Structure
```
Config/
  Addons/
    SystemCleaner/
      addon.json
      README.md
    MyCustomPlugin/
      addon.json
```

## Manifest Specification (`addon.json`)
```json
{
  "id": "my-custom-plugin",
  "name": "My Custom Plugin",
  "description": "Short summary of what this module provides",
  "version": "1.0.0",
  "author": "Technician / Author Name",
  "icon": "🧩",
  "category": "Utilities",
  "enabled": true
}
```

## Adding New Add-ons
1. Create a folder inside `Config/Addons/` (e.g., `Config/Addons/BatteryHealth`).
2. Add an `addon.json` file with the module's metadata.
3. Open OptiAssist and navigate to **Add-ons** > **Manage Add-ons**, then click **Reload Add-ons**.
