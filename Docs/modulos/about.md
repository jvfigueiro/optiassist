# About Application (About)

**Section Identifier:** `about`  
**Type:** Host Native View (`MainWindow`)  
**Documentation File:** `docs/modulos/about.md`  

---

## 1. Overview and Problem Solved

In technical support and maintenance workflows, technicians must be able to verify the active software version, its open-source licensing terms (GNU GPLv3), official channels for updates/documentation, and the integrity of external modular plugins (*Add-ons*) loaded into host memory.

The **About** view fulfills this requirement by consolidating:
- The host executable version (`OptiAssist v1.1.0`).
- Direct access to the GNU GPLv3 open-source license modal dialog.
- Official links to the GitHub repository, website, and technical documentation.
- Acknowledgments to the open-source community (with special credit to the WinUtil project by Chris Titus Tech, which inspired several maintenance routines).
- A detailed audit of all loaded add-on modules with their respective `AssemblyInfo` metadata (Name, Version, Author, and Description).

---

## 2. Step-by-Step Usage Guide

1. **Accessing the View**:
   - In the OptiAssist sidebar, click **ℹ️ About**.
2. **Reviewing Application Information and Links**:
   - Check the product version and open-source compliance notice.
   - Click **GitHub** to open the source code repository.
   - Click **Official Website** to navigate to the official portal.
   - Click **Documentation** to view local or remote technical guides.
   - Click **License** to open the full GNU GPLv3 terms and conditions dialog.
3. **Auditing Installed Modules and Plugins**:
   - Scroll down to the **🧩 About Installed Modules & Plugins** card.
   - Each card presents:
     - Module Icon and Display Name.
     - Compiled Assembly Version (e.g. `v1.0.0.0` or `v1.1.0.0`).
     - Author or Maintainer organization.
     - Brief description of the module's technical purpose.

---

## 3. Internal Architecture and Implementation

### 3.1 Metadata Inspection and Data Binding
1. When navigating to the About view, the host queries the internal `AddonHelper.LoadedAddons` collection.
2. For each registered addon, the `entry.AssemblyVersion` property is extracted directly from the in-memory assembly attributes (`AssemblyFileVersionAttribute` or `AssemblyName.Version`).
3. The `ItemsControl x:Name="icAboutAddonsList"` binds its `ItemsSource` to the `AddonEntry` collection, dynamically rendering each card.

### 3.2 Displaying the GNU GPLv3 License
Clicking the `btnAboutLicense` button:
1. Calls `AppDeploymentHelper.ShowLicenseDialog(this)`.
2. A standalone modal WPF dialog displays the canonical GNU GPLv3 text (`GplLicenseNoticeText`) in a monospace font (`Consolas`) with a scrollable viewer and close button.

### 3.3 External Hyperlinks
Clicking link buttons (`BtnAboutGithub_Click`, `BtnAboutWebsite_Click`, `BtnAboutDocumentation_Click`) triggers the system's default browser:
```csharp
Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
```
Calls are wrapped in `try/catch` handlers to prevent crashes if default protocol associations are missing or corrupted.

---

## 4. Inputs, Outputs, and Configurations

* **Inputs**:
  * In-memory `AddonHelper.LoadedAddons` collection.
  * Assembly metadata (`AssemblyInfo.cs` of each module project).
* **Outputs**:
  * Informational user interface.
  * Modal license dialog.
  * Browser navigation requests.
* **Configurations**:
  * Localization strings in `Config/Languages/*.json` prefixed with `About_`.

---

## 5. Requirements and Dependencies

* **Administrator Privileges**: Inherited from the host; not strictly required for read-only view operations.
* **Supported Windows Versions**: Windows 10 and Windows 11.
* **External Dependencies**: None.

---

## 6. Risks and Reversibility

* **Safe Operation**: 100% read-only. Does not modify registry keys, services, or files on disk.

---

## 7. Error Handling and Logging

* Hyperlink launches and dialog events log informational entries to the host console (`MainWindow.Log`).
* Missing URL protocol associations are safely caught without interrupting application flow.

---

## 8. Related Source Files

* [`src/MainWindow.xaml`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/MainWindow.xaml): Visual layout of `panelAbout` and `icAboutAddonsList`.
* [`src/MainWindow.xaml.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/MainWindow.xaml.cs): Button handlers, localization, and data binding.
* [`src/AddonHelper.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/AddonHelper.cs): `AddonEntry` structure containing `AssemblyVersion`, `AssemblyCompany`, and metadata.
* [`src/ShortcutsHelper.cs`](file:///D:/Users/jvfigueiro/Desktop/OptiAssist/src/ShortcutsHelper.cs): Implementation of `AppDeploymentHelper.ShowLicenseDialog`.
