# Developer Guide: How to Create a Module in OptiAssist

**Document:** `docs/create-module.md`  
**Target Platform:** .NET Framework 4.8 Class Library  
**Host Executable:** `OptiAssist.exe`  

---

## 1. Overview

Extending OptiAssist does not require modifying the host executable. Custom technician tools, system inspectors, or automation scripts can be built as standalone .NET Framework 4.8 Class Libraries that implement the `IOptiAssistAddon` interface.

Key architectural benefits for module developers:
- **Zero Host Recompilation**: Drop your compiled `.dll` into `Config/Addons/<YourModule>/` and OptiAssist detects and integrates it immediately.
- **Anti-Lock Development**: Because the host loads plugins into memory via byte arrays (`Assembly.Load(byte[])`), you can recompile your DLL and overwrite it on disk while OptiAssist is open.
- **Self-Contained UI**: Build your user interface programmatically in C# using standard WPF controls (`Grid`, `StackPanel`, `Button`, `TextBox`), eliminating the need for complex XAML/BAML compilation pipelines.
- **Unified Logging and i18n**: Directly log output to the host terminal and register localized strings in companion JSON files.

---

## 2. Interface Contracts

Your module interacts with the host through two contracts defined in `OptiAssist`:

### 2.1 `IOptiAssistAddon` (Implemented by your Module)
```csharp
namespace OptiAssist
{
    public interface IOptiAssistAddon
    {
        string Id { get; }              // Unique alphanumeric ID (e.g., "network-tester")
        string Name { get; }            // Human-readable title displayed in navigation
        string Description { get; }     // Concise summary of the module's purpose
        string Version { get; }         // Semantic version string (e.g., "1.0.0.0")
        string Author { get; }          // Developer or organization name
        string Icon { get; }            // Symbol or character (e.g., "[N]", "🌐")

        void Initialize(IOptiAssistHost host);  // Injects host services on discovery
        FrameworkElement CreateView();           // Builds and returns the WPF UI tree
        void OnLanguageChanged(string langCode); // Dispatched when UI language changes
    }
}
```

### 2.2 `IOptiAssistHost` (Exposed by OptiAssist)
```csharp
namespace OptiAssist
{
    public interface IOptiAssistHost
    {
        void Log(string message);                                    // Writes to console buffers
        string GetLocalizedString(string key, string defaultValue);  // Queries i18n dictionary
        string CurrentLanguage { get; }                              // Current language code
        AppSettings Settings { get; }                                // Global user preferences
        HardwareSummary Hardware { get; }                            // Audited hardware metrics
        void SaveSettings();                                         // Persists settings to disk
        void OpenLogWindow();                                        // Spawns detached log window
    }
}
```

---

## 3. Minimal Working Example

Below is a complete, minimal, real-world example of an OptiAssist module: a **Network Ping Tester** (`NetworkTesterAddon.cs`).

### 3.1 Project File (`NetworkTester.csproj`)
Create a standard .NET 4.8 Class Library project. Set `<Private>False</Private>` on the `OptiAssist.exe` reference so the host executable is not copied to the output folder:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Release</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{A1B2C3D4-E5F6-7890-ABCD-1234567890AB}</ProjectGuid>
    <OutputType>Library</OutputType>
    <RootNamespace>OptiAssist.Addons.NetworkTester</RootNamespace>
    <AssemblyName>NetworkTester</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>..\..\Config\Addons\NetworkTester\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="PresentationCore" />
    <Reference Include="PresentationFramework" />
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Xaml" />
    <Reference Include="WindowsBase" />
    <Reference Include="OptiAssist">
      <HintPath>..\..\OptiAssist.exe</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include="NetworkTesterAddon.cs" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

### 3.2 Implementation (`NetworkTesterAddon.cs`)
```csharp
using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OptiAssist.Addons.NetworkTester
{
    public class NetworkTesterAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;
        private TextBlock _txtStatus;
        private TextBox _tbTarget;

        public string Id => "network-tester";
        public string Name => _host != null ? _host.GetLocalizedString("NetTest_Name", "Network Tester") : "Network Tester";
        public string Description => "Tests network latency and packet loss to external gateways.";
        public string Version => "1.0.0.0";
        public string Author => "IT Technician";
        public string Icon => "[NET]";

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
            _host.Log("[NetworkTester] Module initialized successfully.");
        }

        public FrameworkElement CreateView()
        {
            StackPanel root = new StackPanel
            {
                Margin = new Thickness(24),
                Orientation = Orientation.Vertical
            };

            TextBlock heading = new TextBlock
            {
                Text = Name,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = Brushes.Black
            };
            root.Children.Add(heading);

            TextBlock label = new TextBlock
            {
                Text = "Target Hostname or IP:",
                Margin = new Thickness(0, 8, 0, 4)
            };
            root.Children.Add(label);

            _tbTarget = new TextBox
            {
                Text = "8.8.8.8",
                Padding = new Thickness(8),
                FontSize = 14,
                Width = 300,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            root.Children.Add(_tbTarget);

            Button btnPing = new Button
            {
                Content = "Execute Ping Test",
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 12, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromRgb(14, 165, 233)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            btnPing.Click += BtnPing_Click;
            root.Children.Add(btnPing);

            _txtStatus = new TextBlock
            {
                Text = "Ready to test.",
                FontSize = 13,
                Foreground = Brushes.DimGray
            };
            root.Children.Add(_txtStatus);

            return root;
        }

        private async void BtnPing_Click(object sender, RoutedEventArgs e)
        {
            string host = _tbTarget.Text.Trim();
            if (string.IsNullOrEmpty(host)) return;

            _txtStatus.Text = string.Format("Pinging {0}...", host);
            _host.Log(string.Format("[NetworkTester] Initiating ping to {0}", host));

            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(host, 3000);
                    if (reply.Status == IPStatus.Success)
                    {
                        string result = string.Format("Success: {0}ms (TTL: {1})", reply.RoundtripTime, reply.Options?.Ttl);
                        _txtStatus.Text = result;
                        _txtStatus.Foreground = Brushes.Green;
                        _host.Log(string.Format("[NetworkTester] {0} -> {1}", host, result));
                    }
                    else
                    {
                        string error = string.Format("Failed: {0}", reply.Status);
                        _txtStatus.Text = error;
                        _txtStatus.Foreground = Brushes.Crimson;
                        _host.Log(string.Format("[NetworkTester] {0} -> {1}", host, error));
                    }
                }
            }
            catch (Exception ex)
            {
                _txtStatus.Text = "Error: " + ex.Message;
                _txtStatus.Foreground = Brushes.Crimson;
                _host.Log(string.Format("[NetworkTester ERROR] {0}: {1}", host, ex.Message));
            }
        }

        public void OnLanguageChanged(string langCode)
        {
            if (_host != null)
            {
                _host.Log(string.Format("[NetworkTester] Language changed to: {0}", langCode));
            }
        }
    }
}
```

---

## 4. Localization for Add-ons

You can bundle localized JSON dictionaries directly alongside your add-on DLL in `Config/Addons/<YourModule>/`:

### `en-US.json`
```json
{
  "NetTest_Name": "Network Tester",
  "NetTest_Ready": "Ready to test."
}
```

### `pt-BR.json`
```json
{
  "NetTest_Name": "Testador de Rede",
  "NetTest_Ready": "Pronto para testar."
}
```

During discovery, `AddonHelper` automatically merges your localized files into the host's central `LocalizationHelper` dictionary.

---

## 5. Deployment and Verification

1. **Build**: Compile your Class Library project using `MSBuild.exe` or Visual Studio in `Release` configuration.
2. **Deploy**: Copy `NetworkTester.dll`, `en-US.json`, and `pt-BR.json` into:
   ```text
   OptiAssist/Config/Addons/NetworkTester/
   ```
3. **Run**: Launch `OptiAssist.exe`. 
   - Check the **Logs** tab: you should see `[NetworkTester] Module initialized successfully.`
   - In the sidebar, your module will appear dynamically as `[NET]  Network Tester`.
   - In **Settings**, your module will be listed in the Add-on Modules table with a toggle switch and remove button.
