# OptiAssist - Assistente Portátil de Otimização e Manutenção (Windows 10 / 11)

**OptiAssist** é uma aplicação portátil nativa em **WPF (.NET Framework 4.8 / XAML)** projetada para técnicos de informática, profissionais de suporte e assistências técnicas. O objetivo é agilizar e padronizar o trabalho de pós-formatação, diagnóstico rápido de hardware, otimização de sistema, reparos de rede/impressoras e geração de laudos técnicos de manutenção.

---

## 🚀 Arquitetura e Estrutura de Layout Rígido

A interface foi desenhada seguindo uma arquitetura moderna, responsiva e alinhada ao esquadro, eliminando coordenadas fixas manuais e margens flutuantes:

1. **Window Principal**:
   - Título: **`OptiAssist`**
   - `Width="1000"`, `Height="620"`, `MinWidth="960"`, `MinHeight="560"`.
   - `WindowStartupLocation="CenterScreen"`.
   - Compatível com DPI Scaling (100%, 125%, 150%) e resoluções padrão (1366x768, 1080p, 2K, 4K).
   - Compilado em modo 64-bit nativo (`Prefer32Bit = false`), garantindo acesso direto aos utilitários do `System32` sem desvios de redirecionamento SysWOW64.
   - Textos com `TextTrimming="CharacterEllipsis"` para prevenir transbordamentos visuais.

2. **Grid Principal da Janela (3 Linhas)**:
   - **Linha 0 (Barra Superior Estilo CCleaner)**: `Height="Auto" MinHeight="52"`:
     - **Canto Superior Esquerdo**: Nome **`OptiAssist`** em destaque.
     - **Canto Superior Direito**: Informações essenciais e despoluídas de hardware:
       - **Linha 1**: `[Marca e Modelo da Placa-Mãe]`
       - **Linha 2**: `[Processador] • [Memória RAM Total]`
    - **Linha 1 (Workspace)**: `Height="*"` dinâmico:
      - **Coluna 0 (Sidebar)**: Menu lateral fixo (`Width="200"`) com 7 opções de navegação direta e **botão inferior para alternar Modo Claro / Modo Escuro**.
      - **Coluna 1 (Área de Conteúdo)**: Área ampla de tela cheia para o conteúdo dinâmico das abas.
    - **Linha 2 (Barra de Status Inferior)**: `Height="28"` fixo na base da janela, persistente em todas as abas:
      - **Lado Esquerdo**: `BitLocker | TPM | Secure Boot` com cores dinâmicas.
      - **Lado Direito**: Identificação customizada: **`Nome personalizado do aplicativo`** (configurado em Configurações).

3. **Barra de Status Inferior Persistente**:
   - Exibe em tempo real: **BitLocker | TPM | Secure Boot**.
   - **Destaque Dinâmico por Cores**:
     - **Verde**: Opção ativa / disponível (ex: `Ativo`, `Disponível (2.0)`).
     - **Vermelho**: Opção inativa / desabilitada (ex: `Inativo`).
     - **Cinza / Neutro**: `Indisponível/não suportado` quando o recurso não estiver disponível ou não for suportado pela placa-mãe/edição do Windows.

---

## 🖥️ Módulos e Recursos Atualizados

- **Visão Geral** (**Bem vindo ao OptiAssist!**):
  - Subtítulo: *A ferramenta suprema para manter seu equipamento em ordem*.
  - **Layout em 2 Blocos por Linha com Alinhamento Perfeito**:
    - **Informações do hardware (Bloco da Esquerda)**: Especificações dispostas estritamente em 1 item por linha:
      1. Processador (CPU e quantidade de núcleos)
      2. Memória RAM (total e disponível)
      3. Placa de Vídeo (GPU)
      4. Placa-Mãe (marca e modelo)
      5. BIOS (versão, fabricante e data)
      6. Armazenamento (disco primário e espaço livre)
      7. **Número de Série do Equipamento** (posicionado abaixo de Armazenamento) com botão **`📋 Copiar`** dedicado.
    - **Sistema Operacional (Bloco da Direita)**: Edição do Windows, versão, compilação (Build), arquitetura e o **Nome do Dispositivo** alinhado perfeitamente na mesma altura exata que o Número de Série do bloco ao lado.
  - **Tarefas Comuns (Bloco Inferior)**: Grade rápida com 8 atalhos essenciais em 1 clique (Gerenciador de Apps, Pasta Manutenção, Chris Titus WinUtil, Massgrave MAS, Diagnóstico de Memória, Gerenciador de Dispositivos, Propriedades do Sistema e Reiniciar para UEFI).
- **Gerenciador de Apps**:
  - **Atualização de Aplicativos (Sempre Visível)**: Painel superior permanente com consulta de atualizações via Winget. Durante o processo de atualização, exibe a barra de progresso em tempo real e um botão centralizado **`⬛ Visualizar Logs`** para navegar diretamente à tela de Logs.
    - **Ações de Atualização**: Botão **`🚀 Atualizar Tudo`** na esquerda (estilo padrão) e **`🔄 Atualizar Selecionados`** em destaque à direita com a cor de acento (`BrushAccent`).
  - **Instalação Automatizada de Aplicativos (Bloco Integrado)**: Lista de programas organizada dentro de um card padrão (`Border`), contendo no rodapé as ações: **`Selecionar Essenciais`**, **`Desmarcar Todos`**, **`⚙️ Personalizar Instalação Automatizada`**, **`📂 Pasta de Instaladores`** e **`🚀 Instalar Selecionados`**.
  - **Personalizar instalação automatizada de aplicativos (Config\PostInstall.json)**:
    - O técnico pode gerenciar os softwares instaláveis diretamente na interface:
      1. **Nome do Programa**: Identificação visual na lista.
      2. **Tipo de Instalação**: Alternância dinâmica entre **`Local`** (com botão seletor de arquivos executáveis `.exe`/`.msi` na pasta `PostInstall\`) e **`Winget`** (com campo para ID oficial do pacote).
      3. **Automatizar**: Checkbox para definir se o programa vem pré-marcado por padrão.
      4. **Ações por Item na Lista**: Mover para cima (`▲`), Mover para baixo (`▼`), Editar preferência (`✏️`) e Remover da lista (`🗑️`).
      5. Argumentos silenciosos opcionais para instaladores personalizados.
  - **Fallback Inteligente**: Se o método for `Local` e o instalador não for encontrado, tenta automaticamente o Winget como contingência quando habilitado.
- **Otimização**:
  - **Ferramentas da Comunidade**: Foco direto nos utilitários comunitários: **Chris Titus Tool (WinUtil)** e **Ativação Massgrave (MAS)**.
  - **Integridade do Sistema e Correções (Coluna Esquerda)**: Seção dedicada a correções profundas:
    - **Verificar e Reparar Integridade do Windows via SFC** (`sfc /scannow`)
    - **Verificar e Reparar Integridade do Windows via DISM** (`Dism /Online /Cleanup-Image /RestoreHealth`)
    - **Verificar Integridade do Disco (CHKDSK C:)**
    - **Correção de problema de impressoras em rede** (RPC 0x11b)
    - **Correção de hora do sistema para instalação dual boot**
    - **Instalar ou Reparar a Microsoft Store** (`wsreset -i`)
    - **Redefinição da Rede**
    - **Benchmark WinSAT** formal com exibição imediata dos resultados de CPU, RAM, Disco e Gráficos.
  - **Limpeza & Desempenho (Coluna Direita)**:
    - **Limpar Arquivos Temporários** (`%TEMP%`, Prefetch, WER e Lixeira)
    - **Limpar Cache do Windows Update**
    - **Otimizar Efeitos Visuais**
    - **Desativar Telemetria e Otimizar Serviços do Sistema**
    - **Compactar Instalação do Windows** (`compact.exe /CompactOS:always`)
    - **Abrir Pasta Scripts**
- **Ferramentas do Windows**:
  - **Ferramentas Administrativas**: Acesso centralizado em grade de 3 colunas para: Ferramenta GPEDIT (Pro/Enterprise), Diagnóstico de Memória, Monitor de Confiabilidade, Visualizador de Eventos, Contas de Usuário Legado, Ferramenta MSCONFIG, Gerenciador de Dispositivos, Gerenciamento de Disco, Painel de Controle Clássico, Informações do Sistema, Sobre o Windows, Conexões de Rede, Recursos do Windows, Editor de Registro, Gerenciador de Tarefas e Monitor de Recursos.
  - **Pasta de Ferramentas de Manutenção**: Botão direto posicionado imediatamente abaixo de Ferramentas Administrativas para acesso à pasta `Maintenance\`.
- **Registrar Manutenção**:
  - **Fluxo de Emissão Seguro**: Ao gerar o laudo, o arquivo é **sempre emitido e arquivado na pasta `Reports\`** do aplicativo. Em seguida, a aplicação pergunta se o técnico deseja salvar uma cópia local no equipamento do usuário (no caminho definido em Configurações).
  - **Layout Padronizado e Direto**:
    - Cabeçalho: `{Nome Personalizado} - {Informações de Contato}` seguido de `REGISTRO DE MANUTENÇÃO`.
    - **Dados do Atendimento**: Ordem de Serviço (OS), Nome do Cliente e Data/Hora de execução (sem campo conflitante de telefone do cliente).
    - **Especificações do Computador**: Nome do Dispositivo, BIOS, Sistema Operacional, Placa-Mãe, CPU, Memória RAM, GPU, Armazenamento Primário (com unidade, espaço livre, total e sistema de arquivos), Secure Boot, TPM, BitLocker e Número de Série / Tag.
    - **Serviços e Procedimentos Executados**: Itens marcados no checklist em formato `[X] Procedimento`.
    - **Laudo Técnico e Observações**: Campo de observações técnicas ou `"Não informado"` quando vazio.
    - Rodapé: `Relatório gerado via OptiAssist`.
- **Configurações & Personalização**:
  - **Modo prestador de serviço**:
    - Checkbox para ativar/desativar o perfil profissional.
    - Ao habilitar, exibe a seção para customizar:
      1. **Nome personalizado do aplicativo** (com dica: *"Customize a aplicação de acordo com o nome que você preferir."*).
      2. **Informações de Contato** (com dica: *"Informe um e-mail, número de contato ou rede social que preferir"*).
      3. **Habilitar Registro de Manutenção**: Checkbox que permite exibir ou ocultar a aba "Registrar Manutenção" no menu lateral.
  - **Suporte Multi-Idiomas Dinâmico e Escalável (i18n)**:
    - Suporte nativo a **Português (Brasil - `pt-BR`)** e **Inglês (Estados Unidos - `en-US`)**, selecionável em 1 clique na aba Configurações.
    - **Tradução em tempo real**: todo o aplicativo é traduzido instantaneamente em memória, sem exigir reinicialização.
    - **Arquitetura 100% escalável**: todas as strings e textos residem em arquivos JSON planos na pasta `Config\Languages\` (`pt-BR.json`, `en-US.json`). Para adicionar um novo idioma (ex: Espanhol `es-ES.json`), basta criar o arquivo correspondente na pasta sem necessidade de recompilar o executável.
  - **Dados Gerais**: Caminho Padrão do Relatório (`C:\MaintenanceReport.txt`), Idioma selecionado e preferências de contingência Winget.
  - **Personalizar o Aplicativo**:
    - **Alternância de Tema (Modo Claro & Modo Escuro)**: 100% consistente em todas as telas e seções (incluindo Tarefas Comuns, Registrar Manutenção e Logs) via DynamicResources.
    - Seleção de cor de botões e acentos por **Código de cor hexadecimal** ou via **Seletor de Cores do Windows**.
    - **Cores Sugeridas**: Azul (Padrão), Verde claro, Roxo, Laranja, Vermelho, Amarelo e Rosa.
    - Pré-visualização e atualização dinâmica em tempo real.
- **Sobre o aplicativo**:
  - Tela dedicada acessível pela barra lateral (abaixo de Configurações).
  - Informações completas de versão (`OptiAssist v1.0.0`), copyright (`Copyright (C) 2026 João Figueiró`) e termos integrais da licença **GNU General Public License v3 (GPLv3)**.
  - Botões de acesso rápido: **Repositório GitHub** (`https://github.com/joaovfigueiro/OptiAssist`) e **Site Oficial** (`https://joaovfigueiro.github.io/`).

---

## 📁 Estrutura de Pastas

```text
📁 OptiAssist/
│
├── OptiAssist.exe             <-- Executável portátil compilado (~125 KB)
├── OptiAssist.csproj          <-- Arquivo de projeto MSBuild para compilação WPF
├── build.bat                  <-- Script de compilação rápida com 1 clique
├── README.md                  <-- Guia de uso e documentação técnica
│
├── 📁 Config/
│   ├── settings.json          <-- Configurações da sua loja, telefone, módulos e cor de acento
│   ├── PostInstall.json       <-- Lista de programas personalizáveis de pós-instalação (Local/Winget)
│   └── 📁 Languages/          <-- Dicionários de tradução multi-idioma
│       ├── pt-BR.json         <-- Português (Brasil)
│       └── en-US.json         <-- English (US)
│
├── 📁 PostInstall/            <-- Setups offline de programas
│   ├── LEIAME.txt
│   ├── ChromeSetup.exe
│   ├── 7ZipSetup.exe
│   └── OfficeSetup.exe
│
├── 📁 Maintenance/            <-- Utilitários portáteis de terceiros
│   └── LEIAME.txt
│
├── 📁 Scripts/                <-- Scripts personalizados (.ps1, .bat, .reg)
│   ├── tweaks_interface.ps1   (Otimização de animações e resposta de interface)
│   ├── fix_printer_rpc.reg    (Correção do erro 0x0000011b de impressoras em rede)
│   └── reset_network.bat      (Redefinição de Winsock, TCP/IP e flush DNS)
│
├── 📁 Reports/                <-- Histórico de laudos e ordens de serviço geradas
│   └── LEIAME.txt
│
└── 📁 src/                    <-- Código-fonte WPF modular completo
    ├── app.ico                (Ícone principal da aplicação em múltiplos tamanhos)
    ├── app.png                (Asset de imagem do logotipo em alta definição)
    ├── app.manifest           (Manifesto com permissão de administrador e DPI)
    ├── App.xaml               (Estilos XAML, temas e recursos dinâmicos Fluent escuros)
    ├── App.xaml.cs            (Tratamento de inicialização e exceções)
    ├── MainWindow.xaml        (Estrutura de Grid rígida da janela principal)
    ├── MainWindow.xaml.cs     (Lógica, eventos de controle e seletor de cores)
    ├── SystemInfoHelper.cs    (WMI: SO, Hardware, Serial, Placa-Mãe, BIOS, WinSAT)
    ├── InstallerHelper.cs     (Gerenciador de instalação offline e Winget)
    ├── TweakHelper.cs         (Limpeza, DISM, SFC, Chkdsk e correções)
    ├── ShortcutsHelper.cs     (Atalhos rápidos para ferramentas nativas e portáteis)
    ├── ReportHelper.cs        (Gerador e exportador do laudo técnico de manutenção)
    ├── SettingsHelper.cs      (Leitor e gravador de settings.json)
    └── LocalizationHelper.cs  (Gerenciador e parser JSON de internacionalização i18n)
```

---

## 🛠️ Como Recompilar

Caso deseje personalizar o código XAML ou C# na pasta `src\`, basta executar o arquivo:
👉 **`build.bat`**

O MSBuild padrão do Windows compilará o projeto `OptiAssist.csproj` em menos de 1 segundo, gerando o executável único `OptiAssist.exe`.
