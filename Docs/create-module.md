# How to Create an OptiAssist Module (`v1.2.S7`)

OptiAssist supports decoupled Class Library plugins (`.NET Framework 4.8`) placed in `Config\Addons\<ModuleName>\`. Each module implements `IOptiAssistAddon` and interacts with the host through `IOptiAssistHost`.

---

## 1. Project Setup (`.csproj`)

Create a `.NET Framework 4.8` Class Library project referencing `OptiAssist.exe` with `<Private>False</Private>` so the host executable is not duplicated into the add-on folder:

```xml
<Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Release</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <OutputType>Library</OutputType>
    <RootNamespace>OptiAssist.Addons.SampleModule</RootNamespace>
    <AssemblyName>SampleModule</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <OutputPath>..\..\..\Config\Addons\SampleModule\</OutputPath>
    <PackageLicenseExpression>GPL-3.0-or-later</PackageLicenseExpression>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Xaml" />
    <Reference Include="WindowsBase" />
    <Reference Include="PresentationCore" />
    <Reference Include="PresentationFramework" />
    <Reference Include="OptiAssist">
      <HintPath>..\..\..\OptiAssist.exe</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
</Project>
```

---

## 2. Minimal Module Implementation

Based directly on the real `IOptiAssistAddon` contract in `src/AddonHelper.cs`:

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using OptiAssist;

namespace OptiAssist.Addons.SampleModule
{
    public class SampleModuleAddon : IOptiAssistAddon
    {
        private IOptiAssistHost _host;
        private TextBlock _lblTitle;

        public string Id { get { return "sample-module"; } }
        public string Name { get { return T("Nav_SampleModule", "Sample Module"); } }
        public string Description { get { return T("Sample_Desc", "Example add-on module"); } }
        public string Version { get { return "1.2.S7"; } }
        public string Author { get { return "Your Name"; } }
        public string Icon { get { return "🧩"; } } // Automatically rendered as Fluent UI SVG vector

        public void Initialize(IOptiAssistHost host)
        {
            _host = host;
            if (_host != null)
            {
                _host.Log("[SampleModule] Initialized.");
            }
        }

        public FrameworkElement CreateView()
        {
            StackPanel root = new StackPanel { Margin = new Thickness(4) };
            _lblTitle = new TextBlock
            {
                Text = Name,
                FontSize = 15,
                FontWeight = FontWeights.Bold
            };
            _lblTitle.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
            root.Children.Add(_lblTitle);

            Button btnAction = new Button
            {
                Content = "⚡ Run Sample Action",
                Height = 32,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            btnAction.SetResourceReference(Button.StyleProperty, "BtnAccent");
            btnAction.Click += (s, e) =>
            {
                if (_host != null) _host.Log("[SampleModule] Action executed.");
                OptiDialog.Show("Hello from SampleModule!", "OptiAssist", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            root.Children.Add(btnAction);

            return root;
        }

        public void OnLanguageChanged(string langCode)
        {
            if (_lblTitle != null) _lblTitle.Text = Name;
        }

        private string T(string key, string fallback)
        {
            return _host != null ? _host.GetLocalizedString(key, fallback) : fallback;
        }
    }
}
```

---

## 3. Localization & Deployment

1. Place `en-US.json` and `pt-BR.json` inside `Config\Addons\SampleModule\`. `AddonHelper` merges them automatically at load time.
2. Always use `SetResourceReference(...)` with OptiAssist theme brushes (`BrushBgPrimary`, `BrushBgCard`, `BrushBorderCard`, `BrushTextPrimary`, `BrushTextSecondary`, `BrushAccent`) and button styles (`BtnDefault`, `BtnAccent`, `BtnDanger`, `BtnRowAction`) so Light/Dark themes and Fluent UI SVG icons apply automatically.
3. Add any third-party dependency DLLs next to `SampleModule.dll` in `Config\Addons\SampleModule\` and register their names in `AddonHelper.IsKnownDependencyDllName(...)` if they should not be scanned as add-ons.
4. Use `OptiDialog.Show(...)` instead of `System.Windows.MessageBox.Show(...)` for consistent window chrome.
