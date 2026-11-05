# Developer Guide: How to Create a Module (Add-on) in OptiAssist

This guide provides a comprehensive, step-by-step technical walkthrough for extending **OptiAssist** through decoupled modules (*Add-ons*). By following this tutorial, you will implement, compile, and integrate a fully functional module compatible with the main host lifecycle.

---

## 1. Overview of the Module Architecture

OptiAssist uses an extensible microkernel pattern where plugins are discovered and loaded dynamically via reflection. Custom external modules do not require altering any source code in the main executable (`OptiAssist.exe`).

### Core Characteristics:
1. **Binary Decoupling**: Modules compile as a .NET Framework 4.8 Class Library referencing only the host contracts in `OptiAssist.exe`.
2. **Anti-Lock Assembly Loading**: The host reads the binary into an in-memory byte array (`File.ReadAllBytes`) before instantiating it (`Assembly.Load`), allowing module DLLs on disk to be updated, recompiled, or deleted without closing OptiAssist.
3. **Cascading Dependency Resolution**: Auxiliary DLLs located in the module directory are resolved automatically via `AppDomain.CurrentDomain.AssemblyResolve`.
4. **View Isolation**: Each module returns a `FrameworkElement` (such as a `Grid`, `ScrollViewer`, or `UserControl`) mounted dynamically into the central content container.

---

## 2. Contracts and Interfaces (`IOptiAssistAddon` and `IOptiAssistHost`)

Every module must implement the `IOptiAssistAddon` interface located in the root `OptiAssist` namespace:

```csharp
namespace OptiAssist
{
    public interface IOptiAssistAddon
    {
        string Id { get; }            // Unique identifier (e.g., "NetworkTools")
        string Name { get; }          // Sidebar display title
        string Description { get; }   // Short description
        string Version { get; }       // Semantic version (e.g., "1.0.0")
        string Author { get; }        // Author or organization
        string Icon { get; }          // MDL2 icon or emoji (e.g., "🌐" or "\uE968")

        void Initialize(IOptiAssistHost host);
        FrameworkElement CreateView();
        void OnLanguageChanged(string langCode);
    }
}
```

The host provides shared services through `IOptiAssistHost`:

```csharp
namespace OptiAssist
{
    public interface IOptiAssistHost
    {
        void Log(string message);
        string GetLocalizedString(string key, string defaultValue);
        string CurrentLanguage { get; }
        AppSettings Settings { get; }
        HardwareSummary Hardware { get; }
        void SaveSettings();
        void OpenLogWindow();
    }
}
```

---

## 3. Step-by-Step: Creating a Module from Scratch

### Step 1: Directory Structure

Create your module directory within the solution:

```text
OptiAssist/
└── src/
    └── Addons/
        └── NetworkTools/
            ├── NetworkTools.csproj
            ├── NetworkToolsAddon.cs
            └── Properties/
                └── AssemblyInfo.cs
```

The final compiled binary should reside in:
```text
Config/
└── Addons/
    └── NetworkTools/
        └── NetworkTools.dll
```

---

### Step 2: Project File Configuration (`.csproj`)

Create the project file targeting .NET Framework 4.8 and referencing WPF core assemblies:

> [!IMPORTANT]
> Always set `<Private>False</Private>` on the `OptiAssist.exe` reference so MSBuild does not copy the host executable into the addon directory.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{A1B2C3D4-E5F6-4789-9012-123456789ABC}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>OptiAssist.Addons.NetworkTools</RootNamespace>
    <AssemblyName>NetworkTools</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>..\..\..\Config\Addons\NetworkTools\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>..\..\..\Config\Addons\NetworkTools\</OutputPath>
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
    <!-- Reference to host executable -->
    <Reference Include="OptiAssist">
      <HintPath>..\..\..\bin\Debug\OptiAssist.exe</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include="NetworkToolsAddon.cs" />
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

---

### Step 3: Implementing the Add-on Class (`NetworkToolsAddon.cs`)

Here is a minimal, complete, and production-ready implementation of a Network Diagnostics module:

```csharp
using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OptiAssist;

namespace OptiAssist.Addons.NetworkTools
{
    public class NetworkToolsAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;
        private TextBlock _statusText;
        private Button _pingButton;
        private Button _flushDnsButton;

        // Module Metadata
        public string Id => "NetworkTools";
        public string Name => _host != null ? _host.GetLocalizedString("net_tools_title", "Network Tools") : "Network Tools";
        public string Description => "Connectivity diagnostics and local DNS resolver cache clearing tools.";
        public string Version => "1.0.0";
        public string Author => "OptiAssist Community";
        public string Icon => "🌐";

        public void Initialize(IOptiAssistHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _host.Log("[NetworkTools] Module initialized successfully.");
        }

        public FrameworkElement CreateView()
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(24)
            };

            var mainPanel = new StackPanel
            {
                MaxWidth = 900,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Title
            var titleBlock = new TextBlock
            {
                Text = _host.GetLocalizedString("net_tools_title", "Network Tools"),
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(titleBlock);

            // Subtitle
            var descBlock = new TextBlock
            {
                Text = _host.GetLocalizedString("net_tools_desc", "Test gateway connectivity and flush the local DNS resolver cache."),
                FontSize = 13,
                Foreground = (Brush)new BrushConverter().ConvertFromString("#AAAAAA"),
                Margin = new Thickness(0, 0, 0, 24)
            };
            mainPanel.Children.Add(descBlock);

            // Action Card
            var cardBorder = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString("#252526"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var cardStack = new StackPanel();

            var actionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 16)
            };

            // Ping Button
            _pingButton = CreateStyledButton(_host.GetLocalizedString("net_ping_btn", "Ping 8.8.8.8"), "#0078D7");
            _pingButton.Click += async (s, e) => await ExecutePingAsync();
            actionsPanel.Children.Add(_pingButton);

            // Flush DNS Button
            _flushDnsButton = CreateStyledButton(_host.GetLocalizedString("net_flush_btn", "Flush DNS"), "#2D2D30");
            _flushDnsButton.Margin = new Thickness(12, 0, 0, 0);
            _flushDnsButton.Click += (s, e) => ExecuteFlushDns();
            actionsPanel.Children.Add(_flushDnsButton);

            cardStack.Children.Add(actionsPanel);

            // Status Area
            _statusText = new TextBlock
            {
                Text = _host.GetLocalizedString("net_status_idle", "Status: Ready..."),
                FontSize = 13,
                Foreground = (Brush)new BrushConverter().ConvertFromString("#CCCCCC"),
                TextWrapping = TextWrapping.Wrap
            };
            cardStack.Children.Add(_statusText);

            cardBorder.Child = cardStack;
            mainPanel.Children.Add(cardBorder);

            scrollViewer.Content = mainPanel;
            return scrollViewer;
        }

        public void OnLanguageChanged(string langCode)
        {
            if (_statusText == null) return;

            _statusText.Text = _host.GetLocalizedString("net_status_idle", "Status: Ready...");
            if (_pingButton != null)
                _pingButton.Content = _host.GetLocalizedString("net_ping_btn", "Ping 8.8.8.8");
            if (_flushDnsButton != null)
                _flushDnsButton.Content = _host.GetLocalizedString("net_flush_btn", "Flush DNS");
        }

        private async Task ExecutePingAsync()
        {
            _statusText.Text = "Sending ping to 8.8.8.8...";
            _pingButton.IsEnabled = false;

            try
            {
                var ping = new Ping();
                _host.Log("[NetworkTools] Executing Ping to 8.8.8.8...");

                var reply = await ping.SendPingAsync("8.8.8.8", 3000);
                if (reply.Status == IPStatus.Success)
                {
                    _statusText.Text = $"Connection OK! RTT: {reply.RoundtripTime} ms | TTL: {reply.Options?.Ttl}";
                    _host.Log($"[NetworkTools] Ping successful: {reply.RoundtripTime}ms");
                }
                else
                {
                    _statusText.Text = $"Ping Failed: {reply.Status}";
                    _host.Log($"[NetworkTools] Ping failure: {reply.Status}");
                }
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Error during ping: {ex.Message}";
                _host.Log($"[NetworkTools] Exception during ping: {ex.Message}");
            }
            finally
            {
                _pingButton.IsEnabled = true;
            }
        }

        private void ExecuteFlushDns()
        {
            try
            {
                _host.Log("[NetworkTools] Executing ipconfig /flushdns...");
                var psi = new ProcessStartInfo("ipconfig", "/flushdns")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(5000);
                    _statusText.Text = "DNS Resolver cache flushed successfully!";
                    _host.Log("[NetworkTools] DNS cache flushed.");
                }
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Error flushing DNS: {ex.Message}";
                _host.Log($"[NetworkTools] Error in ipconfig: {ex.Message}");
            }
        }

        private Button CreateStyledButton(string text, string hexBg)
        {
            var btn = new Button
            {
                Content = text,
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0),
                Background = (Brush)new BrushConverter().ConvertFromString(hexBg),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            return btn;
        }
    }
}
```

---

## 4. Multi-Language Localization (i18n)

Add your localization strings to the respective language files in `Config/Languages/`:

### In `Config/Languages/en-US.json`:
```json
{
  "net_tools_title": "Network Tools",
  "net_tools_desc": "Test gateway connectivity and flush the local DNS resolver cache.",
  "net_ping_btn": "Ping Test (8.8.8.8)",
  "net_flush_btn": "Flush DNS Cache",
  "net_status_idle": "Status: Ready..."
}
```

### In `Config/Languages/pt-BR.json`:
```json
{
  "net_tools_title": "Ferramentas de Rede",
  "net_tools_desc": "Teste a conectividade do gateway e limpe os registros locais de resolução de nomes.",
  "net_ping_btn": "Testar Conexão (Ping)",
  "net_flush_btn": "Limpar Cache DNS",
  "net_status_idle": "Status: Pronto..."
}
```

When the user switches languages, the host automatically triggers:
```csharp
addon.OnLanguageChanged(host.CurrentLanguage);
```

---

## 5. Compiling and Installing the Add-on

### 1. Manual Compilation via MSBuild
```cmd
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" src\Addons\NetworkTools\NetworkTools.csproj /p:Configuration=Release
```

### 2. Output Verification
Verify that the output DLL exists at:
```text
Config\Addons\NetworkTools\NetworkTools.dll
```

### 3. Solution Build Integration
To ensure the addon compiles with `build.bat`, add it to the `CleanupLocalArtifacts` target in `OptiAssist.csproj`:

```xml
<Target Name="CleanupLocalArtifacts" AfterTargets="Build">
  <!-- Existing Addons -->
  ...
  <!-- Your New Addon -->
  <MSBuild Projects="$(SolutionDir)src\Addons\NetworkTools\NetworkTools.csproj" 
           Properties="Configuration=$(Configuration);Platform=$(Platform)" />
</Target>
```

---

## 6. Best Practices and UI Design Guidelines

1. **Theme Consistency**:
   - Background: Dark theme `#1E1E1E` or transparent (inherited from the host container).
   - Card surfaces: `#252526` or `#2D2D30`.
   - Accent action buttons: `#0078D7` or `#0EA5E9`.
   - Typography: `#FFFFFF` for titles, `#AAAAAA` for secondary descriptions.
2. **Non-Blocking UI Operations**:
   - Never execute network or long-running tasks directly on the WPF Dispatcher thread. Use `async/await` or `Task.Run`.
3. **Dedicated Configuration**:
   - If your module requires private state, persist it in `Config/Addons/<ModuleId>/settings.json`.
4. **Administrator Privileges**:
   - Because `OptiAssist.exe` requests elevated privileges via `requireAdministrator`, your module inherits administrative permissions automatically.
