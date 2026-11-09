# Relatório de Arquitetura e Mapeamento de Código: OptiAssist

**Documento:** `docs/_analysis/00-map.md`  
**Data:** 09 de Novembro de 2026  
**Versão do Sistema:** 1.1.0.0  
**Autor:** João Figueiró  
**Licença:** GNU General Public License v3.0 (GNU GPLv3)  

---

## 1. Stack Tecnológica

### 1.1 Linguagens de Programação e Marcação
* **C# (C Sharp 7.3+)**: Linguagem central de todo o ecossistema. Implementa a aplicação Host (`OptiAssist.exe`) e todos os 5 módulos Add-on desacoplados (Class Libraries `.dll`). O código é estritamente compilável pelo `MSBuild.exe` nativo do .NET Framework 4.8.
* **XAML (Extensible Application Markup Language)**: Utilizado no Host (`src/MainWindow.xaml`, `src/App.xaml`) para declaração de layouts de grade (`Grid`), painéis de rolagem, estilos visuais de botões (`BtnNav`, `BtnPrimary`, etc.), animações e dicionários de recursos de tema (Claro/Escuro). Os Add-ons compilados criam suas interfaces programaticamente em código C# puro, gerando árvores visuais WPF sem necessidade de arquivos `.xaml` ou BAML separados.
* **PowerShell (5.1 / Core)**: Interpretador de scripts para automações avançadas de manutenção do sistema, integração com utilitários da comunidade (ex.: Chris Titus WinUtil via `irm ... | iex`), sincronização de tempo via NTP e execução de scripts da Toolbox codificados em Base64 (`-EncodedCommand`) para evitar conflitos de escape de caracteres.
* **Batch / CMD (Windows Command Processor)**: Scripts de automação de compilação (`build.bat`), rotinas de desinstalação assistida e execução de comandos de integridade do sistema operacional (`sfc /scannow`, `dism`, `chkdsk`, `cleanmgr.exe`).
* **JSON (JavaScript Object Notation)**: Formato único de serialização de dados de configuração (`Config/settings.json`), atalhos personalizáveis da dashboard (`Config/custom_shortcuts.json`), catálogos de scripts e comandos (`Config/Addons/Toolbox/toolbox.json`), catálogos de aplicativos pós-formatação (`Config/Addons/AppManager/autoinstall.json`) e dicionários de internacionalização (`Config/Languages/*.json`).

### 1.2 Runtime e Frameworks
* **Runtime**: Microsoft .NET Framework 4.8 (CLR 4.0.30319.42000). Pré-instalado nativamente em todas as edições modernas do Windows 10 (versão 1903+) e Windows 11.
* **Framework de Interface (UI)**: Windows Presentation Foundation (WPF), usufruindo de renderização acelerada por hardware via DirectX nativo da pilha Windows.
* **Filosofia Zero Dependências Externas (Zero Third-Party NuGets)**: O OptiAssist foi intencionalmente arquitetado sem pacotes NuGet externos (sem dependências de terceiros como `Newtonsoft.Json`, `MahApps.Metro`, `Prism` ou `Costura.Fody`). Parsers de JSON, gerenciamento dinâmico de plugins, controles de interface, conexões WMI, serialização e internacionalização foram construídos sobre a Base Class Library (BCL) padrão do .NET Framework e chamadas à API nativa do Windows.

### 1.3 Assemblies .NET e Namespaces Principais

| Assembly / Namespace | Finalidade e Aplicação no Projeto |
| :--- | :--- |
| `PresentationFramework.dll` / `PresentationCore.dll` / `WindowsBase.dll` / `System.Xaml.dll` | Motor gráfico WPF: janelas, gerenciamento de layout (`Grid`, `StackPanel`, `Border`, `ScrollViewer`), estilos, templates visuais, controle de eventos e marshaling de threads via `Dispatcher`. |
| `System.Management.dll` | Comunicação com o Windows Management Instrumentation (WMI / CIM) para auditoria e coleta assíncrona de especificações de hardware (CPU, GPU, RAM, Placa-Mãe, BIOS, status de ativação do Windows, TPM 2.0 e BitLocker). |
| `System.ServiceProcess.dll` | Monitoramento, parada e reinicialização de serviços do Windows (ex.: serviço de atualização `wuauserv` para limpeza de cache). |
| `Microsoft.Win32.Registry` | Acesso direto às chaves dos hives `HKLM` e `HKCU` para inspeção de itens de autoinicialização (`Run`, `StartupApproved`), registro do desinstalador no Painel de Controle, leitura de versão do Windows e aplicação de tweaks de desempenho. |
| `System.Drawing.dll` / `System.Windows.Forms.dll` | Diálogos nativos do sistema: seleção de pastas (`FolderBrowserDialog`) e seleção de cor de destaque personalizada (`ColorDialog`). |
| `System.Diagnostics.Process` | Orquestração de processos filhos externos (PowerShell, CMD, executáveis de terceiros, instaladores), streaming assíncrono de buffers de `stdout` e `stderr` para o terminal de logs e cancelamento de processos em execução. |
| `System.Security.Principal` | Verificação defensiva de privilégios de Administrador (`WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)`). |

### 1.4 Integrações Win32 P/Invoke e COM Interop
* **P/Invoke (Shell32.dll)**:
  * `SHEmptyRecycleBin`: Esvaziamento programático da Lixeira de todos os discos locais sem bloqueio de prompts ao usuário (`SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND`).
* **P/Invoke (User32.dll)**:
  * `SendMessageTimeout`: Emissão de broadcast do evento `WM_SETTINGCHANGE` com timeout de segurança (2000ms) para forçar o Windows Explorer a recarregar métricas de sistema e tweaks sem travamento de interface.
* **COM Interop Late-Bound (`Type.GetTypeFromProgID` + `Activator.CreateInstance`)**:
  * `WScript.Shell` (ProgID): Criação programática de atalhos `.lnk` para implantação na Área de Trabalho e no Menu Iniciar, configurando `TargetPath`, `WorkingDirectory`, `IconLocation` e `Description`.
  * `Schedule.Service` (ProgID): Conexão direta com a API 2.0 do Agendador de Tarefas do Windows (`ITaskService`), permitindo enumeração recursiva de pastas e ativação/desativação de tarefas agendadas no Startup Manager.

### 1.5 Ferramentas de Build, Empacotamento e Instalação
* **Build Engine**: Microsoft Build Engine (`MSBuild.exe` v4.0.30319 64-bit), acionado via script automatizado `build.bat` ou pelo Visual Studio.
* **Pipeline de Compilação em Cascata (`CleanupLocalArtifacts`)**: O arquivo de projeto raiz `OptiAssist.csproj` contém um target pós-build que compila sequencialmente todos os projetos de add-on (`AppManager.csproj`, `SystemCleaner.csproj`, `MaintenanceReport.csproj`, `Toolbox.csproj`, `StartupManager.csproj`), enviando os binários resultantes diretamente para `Config\Addons\<NomeDoModulo>\*.dll`.
* **Empacotador**: NanaZip / 7-Zip CLI (`7z.exe a -t7z -mx=9`) para gerar o arquivo compactado portátil de distribuição `OptiAssist-v1.1.0.0.7z` (~504 KB).
* **Instalador Embutido Autônomo (`AppDeploymentHelper`)**: Dispensa o uso de geradores de instalador de terceiros (como Inno Setup ou NSIS). O próprio `OptiAssist.exe` gerencia os modos de instalação portátil, instalação padrão em `%ProgramFiles%\OptiAssist`, registro no Adicionar ou Remover Programas do Windows (`SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OptiAssist`), atalhos no sistema e geração de `autorun.inf` para mídias externas.

---

## 2. Estrutura de Pastas e Responsabilidades

```text
OptiAssist/
├── .git/                                   # Repositório de controle de versão Git
├── .gitignore                              # Regras de exclusão de binários temporários, logs e pacotes
├── build.bat                               # Script de compilação rápida via MSBuild no terminal
├── LICENSE                                 # Licença oficial GNU General Public License v3.0
├── OptiAssist.csproj                       # Projeto principal WPF (.NET 4.8) do Host
├── OptiAssist.sln                          # Solução Visual Studio integrando Host e Add-ons
├── README.md                               # Apresentação oficial do projeto no GitHub (em inglês)
│
├── Config/                                 # Diretório de persistência de configurações e plugins
│   ├── settings.json                       # Preferências do usuário (tema, idioma, freshstart, add-ons desativados)
│   ├── custom_shortcuts.json               # Configurações de atalhos da seção Common Tasks da Dashboard
│   ├── Languages/                          # Dicionários de internacionalização globais
│   │   ├── en-US.json                      # Dicionário mestre em Inglês (US)
│   │   └── pt-BR.json                      # Dicionário mestre em Português (Brasil)
│   └── Addons/                             # Módulos compilados (.dll) e seus recursos desacoplados
│       ├── AppManager/                     # DLL, autoinstall.json e dicionários do instalador de programas
│       ├── MaintenanceReport/              # DLL e dicionários do gerador de laudos/ordens de serviço
│       ├── StartupManager/                 # DLL e dicionários do gerenciador de inicialização
│       ├── SystemCleaner/                  # DLL e dicionários do módulo de limpeza de disco
│       └── Toolbox/                        # DLL, toolbox.json, scripts auxiliares (.ps1, .bat, .reg) e dicionários
│
├── Docs/                                   # Documentação técnica e de arquitetura do sistema
│   ├── _analysis/                          # Relatórios analíticos e mapas de arquitetura (00-map.md)
│   ├── images/                             # Capturas de tela oficiais dos módulos e assistentes
│   ├── modules/                            # Documentação individualizada por módulo/recurso
│   ├── architecture.md                     # Guia do padrão microkernel e ciclo de vida
│   ├── build-and-distribution.md           # Guia de compilação, requisitos e distribuição
│   ├── configuration-and-logs.md           # Esquemas JSON, i18n e logs
│   ├── faq-and-troubleshooting.md          # Resolução de problemas e casos de borda
│   ├── how-to-create-a-module.md           # Tutorial passo a passo para desenvolvedores de módulos
│   └── README.md                           # Índice geral da documentação
│
├── Installers/                             # Repositório de instaladores offline colocados pelo técnico
│   └── README.txt                          # Orientações sobre convenções de nomenclatura e switches
├── Maintenance/                            # Ferramentas diagnósticas portáteis externas (ex.: CPU-Z, CrystalDiskInfo)
│   └── README.txt                          # Orientações de integração com executáveis portáteis
├── Reports/                                # Destino padrão para salvamento de relatórios técnicos gerados
│   └── README.txt                          # Explicação da pasta de relatórios de manutenção
│
├── src/                                    # Código-fonte central da aplicação Host (OptiAssist.exe)
│   ├── app.ico / app.png                   # Ícones oficiais incorporados na compilação
│   ├── app.manifest                        # Manifesto Windows exigindo elevação de Administrador (requireAdministrator)
│   ├── App.xaml / App.xaml.cs              # Ponto de entrada, tratamento global de exceções e argumentos CLI
│   ├── AssemblyInfo.cs                     # Metadados de versão (1.1.0.0), autor e copyright
│   ├── MainWindow.xaml / .cs               # Janela principal, shell de navegação e coordenação geral
│   ├── AddonHelper.cs                      # Motor de descoberta, anti-lock loading (byte[]), contratos e ciclo de vida
│   ├── LocalizationHelper.cs               # Motor i18n com suporte a hot-reload em tempo real
│   ├── SettingsHelper.cs                   # Parser e serializador JSON zero-dependency para settings.json
│   ├── ShortcutsHelper.cs                  # Lançadores nativos, instalador embutido e assistente Welcome Wizard
│   ├── SystemInfoHelper.cs                 # Coletor assíncrono de telemetria WMI/CIM, registro e segurança
│   ├── TweakHelper.cs                      # Otimizações de registro, processos e comandos streamados
│   └── LogWindow.cs                        # Janela flutuante desacoplada de logs do terminal
│
└── src/Addons/                             # Códigos-fonte dos módulos desacoplados (.NET Class Libraries)
    ├── AppManager/                         # Projeto AppManager.csproj, AppManagerAddon.cs e InstallerHelper.cs
    ├── MaintenanceReport/                  # Projeto MaintenanceReport.csproj e MaintenanceReportAddon.cs
    ├── StartupManager/                     # Projeto StartupManager.csproj, StartupManagerAddon.cs, StartupEngine.cs
    ├── SystemCleaner/                      # Projeto SystemCleaner.csproj e SystemCleanerAddon.cs
    └── Toolbox/                            # Projeto Toolbox.csproj, ToolboxAddon.cs, modelos e runner de processos
```

---

## 3. Funcionamento da Arquitetura Modular

O OptiAssist adota o padrão de arquitetura **Microkernel com Carregamento Dinâmico em Memória**:

```
+-------------------------------------------------------------------------+
|                       Host Kernel (OptiAssist.exe)                      |
|  +--------------------+ +--------------------+ +---------------------+  |
|  | Settings & Config  | |    i18n Engine     | | Thread-Safe Logging |  |
|  +--------------------+ +--------------------+ +---------------------+  |
+------------------------------------^------------------------------------+
                                     | Contratos: IOptiAssistAddon / IOptiAssistHost
                                     | Carregamento Anti-Lock em Memória (byte[])
+------------------+-----------------+------------------+-----------------+
|                  |                                    |                 |
v                  v                                    v                 v
Toolbox (Core)     App Manager                          System Cleaner    Startup Manager / Report
(Addon DLL)        (Addon DLL)                          (Addon DLL)       (Addon DLLs)
```

### 3.1 Definição de um Módulo
Cada módulo é um projeto independente do tipo **Class Library (.NET Framework 4.8)**. Ele referencia o executável host `OptiAssist.exe` com a diretiva `<Private>False</Private>` (para evitar duplicação de assemblies no diretório de saída). O módulo deve conter uma classe pública não-abstrata que implementa a interface `IOptiAssistAddon`. Toda a sua interface com o usuário (UI) é montada programaticamente em código C# utilizando elementos do WPF (`Grid`, `StackPanel`, `ScrollViewer`, `Button`, `TextBox`, `DataGrid`, etc.), dispensando arquivos XAML compilados no add-on.

### 3.2 Contratos de Comunicação (`IOptiAssistAddon` e `IOptiAssistHost`)

#### Contrato do Módulo (`IOptiAssistAddon`)
```csharp
public interface IOptiAssistAddon
{
    string Id { get; }              // Identificador único (ex: "toolbox", "system-cleaner")
    string Name { get; }            // Nome exibido na interface
    string Description { get; }     // Descrição das funções do módulo
    string Version { get; }         // Versão do módulo
    string Author { get; }          // Autor / Desenvolvedor
    string Icon { get; }            // Emoji ou símbolo identificador

    void Initialize(IOptiAssistHost host);  // Injeção dos serviços do Host
    FrameworkElement CreateView();           // Constrói e retorna a raiz da árvore visual WPF
    void OnLanguageChanged(string langCode); // Notificação para atualização de textos em tempo real
}
```

#### Contrato do Host (`IOptiAssistHost`)
```csharp
public interface IOptiAssistHost
{
    void Log(string message);                                    // Envio de mensagens para o terminal de logs
    string GetLocalizedString(string key, string defaultValue);  // Consulta ao motor i18n
    string CurrentLanguage { get; }                              // Código do idioma ativo ("en-US", "pt-BR")
    AppSettings Settings { get; }                                // Acesso às configurações globais do usuário
    HardwareSummary Hardware { get; }                            // Telemetria de hardware coletada pelo Host
    void SaveSettings();                                         // Persistência em disco de alterações nas configurações
    void OpenLogWindow();                                        // Abertura da janela flutuante desacoplada de logs
}
```

### 3.3 Descoberta e Carregamento Anti-Lock
1. **Descoberta**: O método `AddonHelper.LoadDiscoveredAddons` realiza uma varredura no diretório `Config\Addons` buscando arquivos `.dll` tanto na raiz quanto em subpastas de módulos (ex.: `Config\Addons\Toolbox\Toolbox.dll`).
2. **Carregamento Anti-Lock (Sem Trava de Arquivo)**: Em sistemas tradicionais, a chamada `Assembly.LoadFrom(caminho)` bloqueia o arquivo `.dll` no disco até que o processo do Windows seja encerrado. No OptiAssist, o arquivo é lido integralmente para a memória com `byte[] rawBytes = File.ReadAllBytes(sourceToLoad)` e carregado via `Assembly.Load(rawBytes)`. Isso permite que desenvolvedores ou o próprio instalador substituam, atualizem ou excluam DLLs no disco com o aplicativo em execução.
3. **Resolução de Tipos e Assemblies Circulares**: O `AddonHelper` registra um manipulador em `AppDomain.CurrentDomain.AssemblyResolve`. Quando a DLL do add-on tenta resolver referências ao assembly `OptiAssist`, o evento intercepta e retorna a instância já carregada do executável principal.
4. **Carregamento Sob Demanda (Lazy Initialization da View)**: Durante a inicialização do programa, apenas a classe do add-on é instanciada e configurada com `Initialize(host)`. A interface gráfica pesada só é construída através do método `CreateView()` no primeiro momento em que o usuário clica no respectivo botão da barra lateral (`entry.GetOrCreateView()`), ficando então armazenada em cache (`CachedView`).

### 3.4 Integração com a Interface do Usuário (UI)
* O Host possui um painel de navegação dinâmico na barra lateral (`pnlSidebarAddons`).
* Cada add-on carregado e habilitado recebe um `RadioButton` estilizado com seu ícone e nome.
* Ao selecionar o botão, o Host oculta os painéis nativos e injeta a visualização do add-on (`panelAddonHost.Children.Add(view)`).
* Na aba de Configurações, o OptiAssist lista todos os módulos instalados com switches de ativação/desativação e botões para adicionar novas DLLs ou remover módulos existentes (o módulo Toolbox possui proteção `CanModify = false` por ser o núcleo de reparo do sistema).

---

## 4. Inventário Completo de Módulos e Recursos

| Módulo / Recurso | Tipo | Objetivo e Responsabilidade | Arquivos Principais | Dependências Internas | Exige Admin? |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Dashboard** | Host Nativo | Apresenta telemetria de hardware em tempo real, data de instalação do Windows, status de ativação, indicadores de segurança (BitLocker, TPM 2.0, Secure Boot) e atalhos customizáveis de tarefas comuns. | `src/MainWindow.xaml`, `src/MainWindow.xaml.cs`, `src/SystemInfoHelper.cs` | `SystemInfoHelper`, `custom_shortcuts.json`, WMI | **Sim** |
| **Windows Tools** | Host Nativo | Central de atalhos diretos para consoles nativos de gerenciamento do Windows, ferramentas de diagnóstico, pastas de manutenção e reinicialização direta no firmware UEFI. | `src/MainWindow.xaml.cs`, `src/ShortcutsHelper.cs` | `ShortcutsHelper`, `System.Diagnostics.Process` | **Sim** |
| **Settings & Deployment** | Host Nativo | Personalização de tema (Claro/Escuro/Cor de destaque), seleção dinâmica de idiomas com leitura de metadados, Always on Top, logotipo personalizado, gerenciamento de add-ons e assistente de instalação/desinstalação. | `src/MainWindow.xaml.cs`, `src/SettingsHelper.cs`, `src/ShortcutsHelper.cs` | `SettingsHelper`, `AppDeploymentHelper`, `AddonHelper` | **Sim** |
| **Logs & Terminal** | Host Nativo | Terminal de diagnóstico em tempo real (embutido na shell principal e com suporte a janela flutuante desacoplada `LogWindow`), com busca, cópia e exportação para `.txt`. | `src/MainWindow.xaml.cs`, `src/LogWindow.cs` | `Dispatcher`, `System.Text.StringBuilder` | **Sim** (herda do Host) |
| **Toolbox** | Add-on Desacoplado *(Core)* | Hub de scripts e comandos de diagnóstico e reparo do Windows (PowerShell, CMD, executáveis). Suporta categorização, busca em tempo real, streaming de saída e cancelamento de tarefas. | `src/Addons/Toolbox/ToolboxAddon.cs`, `ToolboxModels.cs`, `ToolboxProcessRunner.cs`, `ToolboxStorageManager.cs` | `IOptiAssistHost`, `toolbox.json`, pasta `Scripts/` | **Sim** |
| **App Manager** | Add-on Desacoplado | Gerenciador de instalação autônoma de software pós-formatação com catálogo visual, execução de instaladores locais (`Installers/`), fallback para Winget e scanner de atualizações de programas instalados. | `src/Addons/AppManager/AppManagerAddon.cs`, `src/Addons/AppManager/InstallerHelper.cs` | `IOptiAssistHost`, `autoinstall.json`, `winget.exe` | **Sim** |
| **System Cleaner** | Add-on Desacoplado | Ferramenta de limpeza profunda inspirada no BleachBit: arquivos temporários, caches de navegadores, compactação SQLite (VACUUM), logs de eventos e artefatos de desenvolvimento com modo Dry-Run e proteção `PathSanitizer`. | `src/Addons/SystemCleaner/SystemCleanerAddon.cs` | `IOptiAssistHost`, `Shell32.SHEmptyRecycleBin` | **Sim** |
| **Startup Manager** | Add-on Desacoplado | Inspetor e gerenciador unificado de inicialização do Windows: chaves do Registro (`Run`/`RunOnce` e `StartupApproved`), pastas de Inicializar e Tarefas Agendadas via COM `Schedule.Service`. | `src/Addons/StartupManager/StartupManagerAddon.cs`, `StartupEngine.cs`, `StartupModels.cs` | `IOptiAssistHost`, Registro, COM `Schedule.Service` | **Sim** |
| **Maintenance Report** | Add-on Desacoplado | Gerador de ordens de serviço e laudos técnicos de manutenção contendo dados do cliente, especificações completas da máquina, checklist de 8 etapas de procedimentos e exportação para `.txt`. | `src/Addons/MaintenanceReport/MaintenanceReportAddon.cs` | `IOptiAssistHost`, `HardwareSummary` | **Sim** (herda do Host) |

---

## 5. Pontos de Integração com o Windows e Diferenças Win 10 vs 11

### 5.1 Registro do Windows (`Microsoft.Win32.Registry`)
* **Identificação e Auditoria do SO**:
  * `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`: Consulta de `DisplayVersion` (ex.: "22H2", "23H2"), `UBR` (Update Build Revision), `ReleaseId` e timestamp de `InstallDate`.
* **Segurança do Sistema**:
  * `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State`: Leitura do DWORD `UEFISecureBootEnabled` para confirmar se a inicialização segura está ativa.
* **Autoinicialização e StartupApproved**:
  * `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` e `RunOnce`
  * `HKLM\Software\Microsoft\Windows\CurrentVersion\Run` e `RunOnce`
  * `HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`
  * `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run` e `HKLM\...`: Controle nativo do Windows para habilitar e desabilitar inicializadores sem deletar a entrada (byte 0 = `0x02` ativado, byte 0 = `0x03` desativado).
* **Tweaks do Sistema e Ajustes de Desempenho**:
  * `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects`: Configuração do valor `VisualFXSetting = 2` (Ajustar para melhor desempenho).
  * `HKCU\Control Panel\Desktop\WindowMetrics`: Desativação de animações de minimizar e maximizar (`MinAnimate = "0"`).
  * `HKCU\Control Panel\Desktop`: Redução do atraso de abertura de menus (`MenuShowDelay = "50"`).
  * `HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection`: Bloqueio de telemetria corporativa (`AllowTelemetry = 0`).
  * `HKLM\SYSTEM\CurrentControlSet\Control\Print`: Resolução do erro de RPC em impressoras de rede (`RpcAuthnLevelPrivacyEnabled = 0`).
  * `HKLM\SYSTEM\CurrentControlSet\Control\TimeZoneInformation`: Relógio do hardware em UTC para dual-boot com Linux (`RealTimeIsUniversal = 1`).
* **Registro no Adicionar ou Remover Programas**:
  * `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OptiAssist`: Registro de metadados (`DisplayName`, `DisplayVersion`, `Publisher`, `InstallLocation`, `UninstallString`).

### 5.2 Windows Management Instrumentation (WMI / CIM)
* **`root\cimv2`**:
  * `Win32_OperatingSystem`: Nome da edição, arquitetura, memória livre, data de instalação.
  * `Win32_Processor`: Nome do processador, quantidade de núcleos físicos e lógicos.
  * `Win32_PhysicalMemory` e `Win32_ComputerSystem`: Capacidade física total de memória RAM instalada.
  * `Win32_BaseBoard`: Fabricante e modelo da placa-mãe.
  * `Win32_BIOS`: Fabricante, versão SMBIOS, data de lançamento e número de série da máquina.
  * `Win32_ComputerSystemProduct`: Identificador do produto / Service Tag (fallback).
  * `Win32_VideoController`: Nome da placa de vídeo (GPU) primária e memória de vídeo.
  * `SoftwareLicensingProduct`: Consulta com filtro `ApplicationID = '55c92734-d682-4d71-983e-d6ec3f16059f' AND LicenseStatus = 1` para certificar se o Windows está genuinamente ativado.
  * `Win32_PnPEntity`: Varredura na classe `SecurityDevices` para detecção de módulos TPM 2.0.
  * `Win32_WinSAT`: Leitura de pontuações do Índice de Experiência do Windows.
* **`root\cimv2\Security\MicrosoftTpm`**:
  * `Win32_Tpm`: Consulta de `IsActivated_InitialValue`, `IsEnabled_InitialValue` e `SpecVersion` para certificar disponibilidade e versão do chip TPM.
* **`root\cimv2\Security\MicrosoftVolumeEncryption`**:
  * `Win32_EncryptableVolume`: Identificação de volumes criptografados, método de criptografia, tipo de volume e status de proteção do BitLocker e Device Encryption.

### 5.3 Win32 APIs, P/Invoke e COM
* **P/Invoke `SHEmptyRecycleBin` (Shell32)**: Esvazia a lixeira silenciosamente de todas as unidades sem caixas de diálogo ou sons.
* **P/Invoke `SendMessageTimeout` (User32)**: Envia notificação `WM_SETTINGCHANGE` com timeout de segurança de 2 segundos para o Windows Explorer atualizar variáveis de ambiente e métricas visuais sem reiniciar o computador.
* **COM Late-Bound `WScript.Shell`**: Criação programática de atalhos `.lnk` na Área de Trabalho e no Menu Iniciar.
* **COM Late-Bound `Schedule.Service`**: Manipulação e leitura da árvore completa do Agendador de Tarefas 2.0 do Windows.

### 5.4 Linha de Comando e Interpretadores (PowerShell, CMD, Winget)
* **PowerShell**: Execução em modo invisível (`-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden`) com codificação em Base64 (`-EncodedCommand`) para evitar quebra de argumentos com caracteres especiais.
* **Winget**: Integração com `winget.exe` para instalação silenciosa em lote (`winget install --id <ID> --silent --accept-package-agreements --accept-source-agreements`) e verificação de novas versões (`winget upgrade`).

### 5.5 Diferenças de Comportamento entre Windows 10 e Windows 11

| Característica / Recurso | Windows 10 | Windows 11 | Comportamento no OptiAssist |
| :--- | :--- | :--- | :--- |
| **TPM 2.0 e Requisitos de Segurança** | Opcional. Muitas máquinas possuem TPM 1.2 ou nenhum chip de segurança. | Obrigatório para o sistema operacional. Quase a totalidade das instalações possui TPM 2.0. | O OptiAssist audita a classe `Win32_Tpm` e faz fallback em `Win32_PnPEntity`. Exibe na barra de status o estado e a versão ("Disponível (2.0)" ou "Inativo"). |
| **Criptografia de Disco (BitLocker vs Device Encryption)** | Criptografia restrita principalmente às edições Pro e Enterprise via BitLocker. | Edições Home frequentemente contam com "Device Encryption" (Criptografia de Dispositivo) ativa por padrão em OEMs. | A consulta ao namespace `MicrosoftVolumeEncryption` identifica com precisão o status de proteção em ambas as edições sem gerar erros em edições Home. |
| **Gerenciador de Pacotes Winget** | Presente nativamente em compilações modernas, mas ausente em versões antigas ou edições LTSC sem Microsoft Store. | Integrado por padrão em todas as edições do sistema operacional. | O OptiAssist detecta preventivamente a presença de `winget.exe`. Se ausente, opera normalmente utilizando o repositório local offline `Installers/`. |
| **Reinício no Firmware UEFI (`shutdown /r /fw /t 0`)** | Em computadores antigos instalados sob Legacy BIOS (MBR), o comando falha com erro Win32 `0x5B3`. | Instalado quase que exclusivamente sob partições GPT com inicialização UEFI. | O OptiAssist executa a tentativa de reinício e, caso ocorra a recusa pelo hardware/MBR, exibe uma mensagem informativa amigável em vez de travar o programa. |
| **Autorun em Mídias Removíveis (USB)** | Execução automática de executáveis é totalmente bloqueada pelo Windows desde atualizações antigas. | Execução automática de executáveis é totalmente bloqueada pelo sistema. | O gerador de autorun do OptiAssist cria o `autorun.inf` estritamente para fornecer o ícone personalizado da aplicação e o rótulo amigável da unidade no Windows Explorer. |

---

## 6. Configuração, Logs, Persistência e Tratamento de Erros

### 6.1 Configuração e Persistência de Dados
* **Parser JSON Zero-Dependency (`SettingsHelper.cs`)**: Motor customizado para leitura e escrita de JSON sem dependência de bibliotecas externas. Suporta valores booleanos, inteiros, strings e listas delimitadas por vírgula.
* **Preferências Globais (`Config/settings.json`)**:
  * `CustomerName` e `ContactInfo`: Dados do técnico ou empresa para emissão de laudos de manutenção.
  * `Language`: Idioma da interface (ex: `"en-US"`, `"pt-BR"`).
  * `ReportSavePath`: Localização padrão para exportação de relatórios técnicos.
  * `AutoRefreshHardware`: Reavaliação automática da telemetria ao alternar abas.
  * `EnableWingetFallback`: Chave que autoriza o download via repositório Microsoft Winget caso o instalador não exista localmente em `Installers/`.
  * `Theme` e `AccentColor`: Preferências de interface visual (tema Claro/Escuro e paleta de cores hexadecimais).
  * `AlwaysOnTop`: Janela principal fixada acima das outras janelas para facilitar acompanhamento durante manutenções.
  * `CustomLogoPath`: Caminho de imagem PNG personalizada para o topo da aplicação.
  * `freshstart`: Flag que controla a exibição do Assistente de Boas-Vindas (`WelcomeWizardWindow`) na primeira execução da aplicação.
  * `DisabledAddons`: Lista de IDs de módulos que o usuário optou por desativar.

### 6.2 Sistema de Logs Centralizado e Thread-Safe
* Todas as mensagens de diagnóstico e logs de processos convergem para o método `MainWindow.Log(message)`.
* **Sincronização entre Threads**: O Host utiliza `Dispatcher.Invoke` e `Dispatcher.BeginInvoke` para garantir que mensagens geradas em threads secundárias de processos ou chamadas WMI sejam inseridas com segurança no `TextBox` da interface visual.
* **Janela Externa Desacoplada (`LogWindow.cs`)**: O técnico pode desacoplar o terminal de logs para uma janela independente, permitindo movê-la para um monitor secundário enquanto executa tarefas ou scripts na janela principal. Ambas as telas sincronizam as mensagens em tempo real.
* **Exportação**: O terminal permite copiar mensagens para a Área de Transferência e exportar o histórico integral com timestamps para um arquivo de texto `.txt`.

### 6.3 Tratamento Global de Exceções e Resiliência
* **Captura de Erros Não Tratados**: Em `src/App.xaml.cs`, o aplicativo intercepta exceções nos eventos `AppDomain.CurrentDomain.UnhandledException` e `DispatcherUnhandledException`, exibindo caixas de diálogo informativas para prevenir encerramento silencioso e abrupto do programa.
* **Auto-Elevação Defensiva de Privilégios**: Na inicialização, caso o usuário não esteja executando como Administrador, o OptiAssist tenta auto-elevar o processo via `Verb = "runas"`. Se o usuário negar a solicitação no diálogo UAC do Windows (código Win32 `1223: ERROR_CANCELLED`), o aplicativo exibe uma mensagem clara e educativa explicando por que os privilégios são necessários antes de encerrar de forma limpa.
* **Isolamento de Falhas nos Módulos**: O carregamento de cada Add-on é encapsulado em blocos `try-catch` individuais. Uma falha de compilação ou incompatibilidade em um módulo específico é registrada no terminal de logs (`[ADDON LOAD ERROR]`), permitindo que todos os demais módulos e a aplicação principal continuem funcionando normalmente.
* **Tolerância a Arquivos Bloqueados**: O módulo `SystemCleaner` ignora silenciosamente arquivos temporários em uso por processos abertos do Windows (`IOException`, `UnauthorizedAccessException`), prosseguindo com a limpeza dos demais itens sem interrupção.

---

## 7. Limitações Técnicas e Aspectos Dependentes do Ambiente

1. **Hardware Virtualizado (VMs) e BIOS Legadas**: Em máquinas virtuais (VirtualBox, VMware, Hyper-V) ou placas-mãe antigas com tabelas SMBIOS incompletas, algumas consultas WMI podem retornar strings genéricas como `"None"`, `"System Serial Number"` ou `"To be filled by O.E.M."`. O OptiAssist possui rotinas de sanitização para exibir `"Não especificado"` ou rótulos amigáveis, mas a precisão dos dados depende estritamente das informações gravadas pelo fabricante da placa-mãe.
2. **Políticas Corporativas Rígidas (AppLocker, SRP e GPOs)**: Em computadores corporativos gerenciados por Active Directory com políticas estritas de bloqueio de scripts ou restrição de caminhos de execução, a execução de comandos PowerShell (mesmo com `-ExecutionPolicy Bypass`) pode ser rejeitada pelo sistema operacional caso haja diretivas de máquina (`MachinePolicy`) sobrepostas.
3. **Disponibilidade e Conectividade do Winget**: O fallback do App Manager para o repositório Microsoft Winget depende de conectividade ativa com a internet e do serviço de distribuição da Microsoft. Em redes isoladas (air-gapped) ou sem acesso externo, a instalação depende exclusivamente de instaladores locais presentes na pasta `Installers/`.
4. **Reinicialização no Firmware UEFI em Particionamento MBR**: A opção de reiniciar diretamente na BIOS/UEFI (`shutdown /r /fw /t 0`) é um recurso restrito a placas-mãe modernas configuradas em modo UEFI com particionamento GPT. Em instalações antigas ou Legacy BIOS, o Windows rejeita a solicitação por limitação de hardware.
