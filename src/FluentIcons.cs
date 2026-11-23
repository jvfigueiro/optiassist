using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OptiAssist
{
    /// <summary>
    /// Microsoft Fluent UI System Icons (MIT License - compatible with GNU GPLv3).
    /// Provides crisp 24x24 SVG Path vector geometries and automatic content/visual-tree conversion
    /// across OptiAssist and all modular add-on DLLs.
    /// Strict .NET Framework 4.8 / C# 5 compatible.
    /// </summary>
    public static class FluentIcons
    {
        // Authentic 24x24 Fluent UI System Icons SVG Path Data (MIT Licensed)
        private static readonly Dictionary<string, string> _iconPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _fixedColorTokens = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly List<string> _orderedTokens = new List<string>();
        private static readonly object _initLock = new object();
        private static bool _initialized = false;
        private static bool _classHandlerRegistered = false;

        // Standard 24x24 Fluent UI System Icons Path Geometries
        public const string PathDesktop = "M4 4.5A2.5 2.5 0 0 1 6.5 2h11A2.5 2.5 0 0 1 20 4.5v10a2.5 2.5 0 0 1-2.5 2.5H14v2h2.25a.75.75 0 0 1 0 1.5h-8.5a.75.75 0 0 1 0-1.5H10v-2H6.5A2.5 2.5 0 0 1 4 14.5v-10ZM6.5 3.5a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h11a1 1 0 0 0 1-1v-10a1 1 0 0 0-1-1h-11Z";
        public const string PathWrenchScrewdriver = "M14.78 3.16a4.5 4.5 0 0 0-5.46 6.08l-5.8 5.8a2.12 2.12 0 0 0 3 3l5.8-5.8a4.5 4.5 0 0 0 6.08-5.46.75.75 0 0 0-1.25-.32L15 8.61l-1.61-.4-1.6-1.6-.4-1.61 2.15-2.15a.75.75 0 0 0-.32-1.25 4.48 4.48 0 0 0-1.44-.44Zm-1.02 7.82-5.94 5.94a.62.62 0 0 1-.88-.88l5.94-5.94a4.52 4.52 0 0 0 .88.88Z";
        public const string PathWindowConsole = "M4.5 3A2.5 2.5 0 0 0 2 5.5v13A2.5 2.5 0 0 0 4.5 21h15a2.5 2.5 0 0 0 2.5-2.5v-13A2.5 2.5 0 0 0 19.5 3h-15ZM3.5 5.5a1 1 0 0 1 1-1h15a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1h-15a1 1 0 0 1-1-1v-13Zm3.28 3.22a.75.75 0 0 0-1.06 1.06L8.44 12.5l-2.72 2.72a.75.75 0 1 0 1.06 1.06l3.25-3.25a.75.75 0 0 0 0-1.06L6.78 8.72ZM12 15.25a.75.75 0 0 0 0 1.5h5.25a.75.75 0 0 0 0-1.5H12Z";
        public const string PathToolbox = "M8.5 3A1.5 1.5 0 0 0 7 4.5V6H4.5A2.5 2.5 0 0 0 2 8.5v10A2.5 2.5 0 0 0 4.5 21h15a2.5 2.5 0 0 0 2.5-2.5v-10A2.5 2.5 0 0 0 19.5 6H17V4.5A1.5 1.5 0 0 0 15.5 3h-7ZM8.5 4.5h7V6h-7V4.5ZM3.5 8.5a1 1 0 0 1 1-1h15a1 1 0 0 1 1 1V12H16v-.75a.75.75 0 0 0-1.5 0V12h-5v-.75a.75.75 0 0 0-1.5 0V12H3.5V8.5Zm0 5H8v.75a.75.75 0 0 0 1.5 0V13.5h5v.75a.75.75 0 0 0 1.5 0V13.5h4.5v5a1 1 0 0 1-1 1h-15a1 1 0 0 1-1-1v-5Z";
        public const string PathBox = "M11.15 2.21a1.75 1.75 0 0 1 1.7 0l7 3.89c.55.31.9.89.9 1.53v8.74c0 .64-.35 1.22-.9 1.53l-7 3.89a1.75 1.75 0 0 1-1.7 0l-7-3.89a1.75 1.75 0 0 1-.9-1.53V7.63c0-.64.35-1.22.9-1.53l7-3.89Zm.85 1.33L5.64 7.07 12 10.6l6.36-3.53L12 3.54Zm-7.25 4.8v8.03c0 .1.05.19.14.24l6.36 3.53v-8.27L4.75 8.34Zm8 11.8 6.36-3.53a.25.25 0 0 0 .14-.24V8.34l-6.5 3.6v8.2Z";
        public const string PathBroom = "M19.78 3.22a.75.75 0 0 1 0 1.06l-6.2 6.2 1.48 1.48a2.25 2.25 0 0 1 0 3.18l-4.5 4.5a2.25 2.25 0 0 1-2.55.44l-3.98-1.77a.75.75 0 0 1-.23-1.21l5.94-5.94a2.25 2.25 0 0 1 3.18 0l-.44-.44 6.2-6.2a.75.75 0 0 1 1.1 0ZM10.8 12.26 5.66 17.4l2.94 1.31a.75.75 0 0 0 .84-.15l1.39-1.39-1.22-1.22a.75.75 0 0 1 1.06-1.06l1.22 1.22 2.11-2.11a.75.75 0 0 0 0-1.06l-2.14-2.14a.75.75 0 0 0-1.06.46Z";
        public const string PathRocket = "M19.85 3.15a1.2 1.2 0 0 1 .99 1c.38 2.66-.35 6.08-3.03 8.76l-1.25 1.25.38 3.25a1.75 1.75 0 0 1-.5 1.44l-2.1 2.1a.75.75 0 0 1-1.27-.41l-.78-3.52-2.7-2.7-3.52-.78a.75.75 0 0 1-.41-1.27l2.1-2.1a1.75 1.75 0 0 1 1.44-.5l3.25.38 1.25-1.25c2.68-2.68 6.1-3.41 8.76-3.03l.39-.02ZM15 7.5a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3Zm-9.78 9.22a.75.75 0 0 1 1.06 0l1.5 1.5a.75.75 0 0 1 0 1.06l-2.25 2.25a.75.75 0 0 1-1.06-1.06l2.25-2.25-1.5-1.5Z";
        public const string PathFlash = "M13.84 2.21a.75.75 0 0 1 .58.88L13.1 9.5h5.15a.75.75 0 0 1 .6 1.2l-8.25 11a.75.75 0 0 1-1.33-.6L10.9 14.5H5.75a.75.75 0 0 1-.6-1.2l8.25-11a.75.75 0 0 1 .44-.09Z";
        public const string PathClipboard = "M9 2a2 2 0 0 0-1.94 1.5H5.75A2.75 2.75 0 0 0 3 6.25v13A2.75 2.75 0 0 0 5.75 22h12.5A2.75 2.75 0 0 0 21 19.25v-13A2.75 2.75 0 0 0 18.25 3.5h-1.31A2 2 0 0 0 15 2H9Zm-.5 2a.5.5 0 0 1 .5-.5h6a.5.5 0 0 1 .5.5v.5a.5.5 0 0 1-.5.5H9a.5.5 0 0 1-.5-.5V4ZM5.75 5H7.1A2 2 0 0 0 9 6h6a2 2 0 0 0 1.9-1h1.35c.69 0 1.25.56 1.25 1.25v13c0 .69-.56 1.25-1.25 1.25H5.75c-.69 0-1.25-.56-1.25-1.25v-13C4.5 5.56 5.06 5 5.75 5Zm2.5 5.25a.75.75 0 0 0 0 1.5h7.5a.75.75 0 0 0 0-1.5h-7.5Zm0 4a.75.75 0 0 0 0 1.5h5a.75.75 0 0 0 0-1.5h-5Z";
        public const string PathSettings = "M10.42 2.45a2 2 0 0 1 3.16 0l.75.98c.28.36.73.55 1.19.49l1.22-.16a2 2 0 0 1 2.23 2.23l-.16 1.22c-.06.46.13.91.49 1.19l.98.75a2 2 0 0 1 0 3.16l-.98.75c-.36.28-.55.73-.49 1.19l.16 1.22a2 2 0 0 1-2.23 2.23l-1.22-.16c-.46-.06-.91.13-1.19.49l-.75.98a2 2 0 0 1-3.16 0l-.75-.98a1.25 1.25 0 0 0-1.19-.49l-1.22.16a2 2 0 0 1-2.23-2.23l.16-1.22c.06-.46-.13-.91-.49-1.19l-.98-.75a2 2 0 0 1 0-3.16l.98-.75c.36-.28.55-.73.49-1.19L5.03 6a2 2 0 0 1 2.23-2.23l1.22.16c.46.06.91-.13 1.19-.49l.75-.99ZM12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7ZM10 12a2 2 0 1 1 4 0 2 2 0 0 1-4 0Z";
        public const string PathInfo = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm0 1.5a8.5 8.5 0 1 1 0 17 8.5 8.5 0 0 1 0-17ZM11.25 11v5.25a.75.75 0 0 0 1.5 0V11a.75.75 0 0 0-1.5 0ZM12 7.25a1 1 0 1 0 0 2 1 1 0 0 0 0-2Z";
        public const string PathDeveloperBoard = "M8.25 2.75a.75.75 0 0 1 1.5 0V5h4.5V2.75a.75.75 0 0 1 1.5 0V5h1A2.25 2.25 0 0 1 19 7.25v1h2.25a.75.75 0 0 1 0 1.5H19v4.5h2.25a.75.75 0 0 1 0 1.5H19v1A2.25 2.25 0 0 1 16.75 19h-1v2.25a.75.75 0 0 1-1.5 0V19h-4.5v2.25a.75.75 0 0 1-1.5 0V19h-1A2.25 2.25 0 0 1 5 16.75v-1H2.75a.75.75 0 0 1 0-1.5H5v-4.5H2.75a.75.75 0 0 1 0-1.5H5v-1A2.25 2.25 0 0 1 7.25 5h1V2.75ZM7.25 6.5a.75.75 0 0 0-.75.75v9.5c0 .41.34.75.75.75h9.5a.75.75 0 0 0 .75-.75v-9.5a.75.75 0 0 0-.75-.75h-9.5ZM9.5 8.75h5a.75.75 0 0 1 .75.75v5a.75.75 0 0 1-.75.75h-5a.75.75 0 0 1-.75-.75v-5a.75.75 0 0 1 .75-.75Zm.75 1.5v3.5h3.5v-3.5h-3.5Z";
        public const string PathWindows = "M3 5.25A2.25 2.25 0 0 1 5.25 3h5.25v8.25H3V5.25Zm0 7.5h7.5V21H5.25A2.25 2.25 0 0 1 3 18.75v-6Zm9-9.75h6.75A2.25 2.25 0 0 1 21 5.25v6h-9V3Zm0 9.75h9v6A2.25 2.25 0 0 1 18.75 21H12v-8.25Z";
        public const string PathHardDrive = "M4.5 4A2.5 2.5 0 0 0 2 6.5v11A2.5 2.5 0 0 0 4.5 20h15a2.5 2.5 0 0 0 2.5-2.5v-11A2.5 2.5 0 0 0 19.5 4h-15ZM3.5 6.5a1 1 0 0 1 1-1h15a1 1 0 0 1 1 1V13h-17V6.5Zm0 8h17v3a1 1 0 0 1-1 1h-15a1 1 0 0 1-1-1v-3Zm13.25 1a1.25 1.25 0 1 0 0 2.5 1.25 1.25 0 0 0 0-2.5Z";
        public const string PathGames = "M6.5 6A4.5 4.5 0 0 0 2 10.5v3.75a3.75 3.75 0 0 0 6.66 2.37L10.33 14.5h3.34l1.67 2.12A3.75 3.75 0 0 0 22 14.25V10.5A4.5 4.5 0 0 0 17.5 6h-11ZM3.5 10.5a3 3 0 0 1 3-3h11a3 3 0 0 1 3 3v3.75a2.25 2.25 0 0 1-4 1.42l-2.1-2.67H9.6l-2.1 2.67a2.25 2.25 0 0 1-4-1.42V10.5Zm4.25-.75a.75.75 0 0 0-1.5 0V11H5a.75.75 0 0 0 0 1.5h1.25v1.25a.75.75 0 0 0 1.5 0V12.5H9A.75.75 0 0 0 9 11H7.75V9.75ZM16 10a1 1 0 1 0 0 2 1 1 0 0 0 0-2Zm2 2a1 1 0 1 0 0 2 1 1 0 0 0 0-2Z";
        public const string PathPuzzlePiece = "M11.5 2.25a2.75 2.75 0 0 1 2.7 2.25h2.55A2.25 2.25 0 0 1 19 6.75v2.55a2.75 2.75 0 0 1 0 5.4v2.55A2.25 2.25 0 0 1 16.75 19.5h-2.55a2.75 2.75 0 0 0-5.4 0H6.25A2.25 2.25 0 0 1 4 17.25v-2.55a2.75 2.75 0 0 0 0-5.4V6.75A2.25 2.25 0 0 1 6.25 4.5H8.8a2.75 2.75 0 0 1 2.7-2.25Z";
        public const string PathGlobe = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm6.92 6h-3.17a15.7 15.7 0 0 0-1.38-4.12A8.53 8.53 0 0 1 18.92 8ZM12 3.58c.84 1.07 1.65 2.66 2.13 4.42H9.87c.48-1.76 1.29-3.35 2.13-4.42ZM9.63 3.88A15.7 15.7 0 0 0 8.25 8H5.08a8.53 8.53 0 0 1 4.55-4.12ZM4.26 9.5h3.68a18.8 18.8 0 0 0 0 5H4.26a8.47 8.47 0 0 1 0-5Zm.82 6.5h3.17c.31 1.52.8 2.94 1.38 4.12A8.53 8.53 0 0 1 5.08 16ZM12 20.42c-.84-1.07-1.65-2.66-2.13-4.42h4.26c-.48 1.76-1.29 3.35-2.13 4.42Zm2.37-.3c.58-1.18 1.07-2.6 1.38-4.12h3.17a8.53 8.53 0 0 1-4.55 4.12Zm1.69-5.62a18.8 18.8 0 0 0 0-5h3.68a8.47 8.47 0 0 1 0 5h-3.68Z";
        public const string PathBattery = "M4.25 6A2.25 2.25 0 0 0 2 8.25v7.5A2.25 2.25 0 0 0 4.25 18h13.5A2.25 2.25 0 0 0 20 15.75v-1.25h.75A1.25 1.25 0 0 0 22 13.25v-2.5a1.25 1.25 0 0 0-1.25-1.25H20V8.25A2.25 2.25 0 0 0 17.75 6H4.25ZM3.5 8.25c0-.41.34-.75.75-.75h13.5c.41 0 .75.34.75.75v7.5a.75.75 0 0 1-.75.75H4.25a.75.75 0 0 1-.75-.75v-7.5Zm8.32.47a.75.75 0 0 0-1.28.35l-.8 3.18h2.2l-.76 3.03a.75.75 0 0 0 1.28.69l3-3.75a.75.75 0 0 0-.59-1.22h-1.9l.77-2.28Z";
        public const string PathPlug = "M8.75 2.75a.75.75 0 0 0-1.5 0V6H5.75A1.75 1.75 0 0 0 4 7.75v3.5A5.75 5.75 0 0 0 9 16.95v4.3a.75.75 0 0 0 1.5 0v-4.2h3v4.2a.75.75 0 0 0 1.5 0v-4.3a5.75 5.75 0 0 0 5-5.7v-3.5A1.75 1.75 0 0 0 18.25 6H16.75V2.75a.75.75 0 0 0-1.5 0V6h-6.5V2.75Z";
        public const string PathTemperature = "M12 2a3.25 3.25 0 0 0-3.25 3.25v8.21a5 5 0 1 0 6.5 0V5.25A3.25 3.25 0 0 0 12 2Zm-1.75 3.25a1.75 1.75 0 1 1 3.5 0v8.87l.31.22a3.5 3.5 0 1 1-4.12 0l.31-.22V5.25ZM12 8a.75.75 0 0 0-.75.75v5.6a2.25 2.25 0 1 0 1.5 0v-5.6A.75.75 0 0 0 12 8Z";
        public const string PathFire = "M12.83 2.18a.75.75 0 0 0-1.15.48c-.56 2.14-1.88 3.63-3.32 5.26C6.8 9.68 5 11.7 5 14.75a7 7 0 1 0 14 0c0-2.55-1.26-4.77-2.48-6.37a.75.75 0 0 0-1.26.15l-.68 1.52c-.4-2.83-1.08-5.97-1.75-7.87ZM12 20.25a5.5 5.5 0 0 1-5.5-5.5c0-2.42 1.46-4.08 2.98-5.8.98-1.1 2-2.25 2.66-3.71.49 1.65.94 3.9 1.15 6.08a.75.75 0 0 0 1.43.23l1.16-2.59c.73 1.24 1.62 2.9 1.62 4.79a5.5 5.5 0 0 1-5.5 5.5Z";
        public const string PathLeaf = "M19.5 3.5c-5.8 0-10.67 2.2-13.3 5.71-2.06 2.75-2.38 6.13-1.45 8.84l-2.03 2.03a.75.75 0 1 0 1.06 1.06l2.06-2.06c2.71.93 6.09.61 8.84-1.45 3.51-2.63 5.71-7.5 5.71-13.3a.89.89 0 0 0-.89-.83Z";
        public const string PathChart = "M5.25 3A1.25 1.25 0 0 0 4 4.25v15.5C4 20.44 4.56 21 5.25 21h2.5C8.44 21 9 20.44 9 19.75V4.25C9 3.56 8.44 3 7.75 3h-2.5ZM10.75 9c-.69 0-1.25.56-1.25 1.25v9.5c0 .69.56 1.25 1.25 1.25h2.5c.69 0 1.25-.56 1.25-1.25v-9.5c0-.69-.56-1.25-1.25-1.25h-2.5ZM15 14.25c0-.69.56-1.25 1.25-1.25h2.5c.69 0 1.25.56 1.25 1.25v5.5c0 .69-.56 1.25-1.25 1.25h-2.5c-.69 0-1.25-.56-1.25-1.25v-5.5Z";
        public const string PathSparkle = "M10.5 2.5a.75.75 0 0 1 1.42 0l1.55 4.42 4.43 1.55a.75.75 0 0 1 0 1.42l-4.43 1.55-1.55 4.43a.75.75 0 0 1-1.42 0l-1.55-4.43-4.42-1.55a.75.75 0 0 1 0-1.42l4.42-1.55L10.5 2.5Zm7.25 11.25a.6.6 0 0 1 1.14 0l.77 2.19 2.19.77a.6.6 0 0 1 0 1.14l-2.19.77-.77 2.19a.6.6 0 0 1-1.14 0l-.77-2.19-2.19-.77a.6.6 0 0 1 0-1.14l2.19-.77.77-2.19Z";
        public const string PathShieldCheckmark = "M11.45 2.13a1.5 1.5 0 0 1 1.1 0l7 2.8A1.5 1.5 0 0 1 20.5 6.3v5.45c0 4.84-3.28 8.75-8.1 10.18a1.5 1.5 0 0 1-.8 0C6.78 20.5 3.5 16.59 3.5 11.75V6.3a1.5 1.5 0 0 1 .95-1.37l7-2.8Zm.55 1.4L5 6.3v5.45c0 4.14 2.77 7.49 7 8.75 4.23-1.26 7-4.61 7-8.75V6.3l-7-2.77Zm3.78 5.69a.75.75 0 0 1 0 1.06l-4.25 4.25a.75.75 0 0 1-1.06 0l-2.25-2.25a.75.75 0 1 1 1.06-1.06L11 12.94l3.72-3.72a.75.75 0 0 1 1.06 0Z";
        public const string PathLockClosed = "M12 2a4 4 0 0 0-4 4v2H6.75A2.75 2.75 0 0 0 4 10.75v8.5A2.75 2.75 0 0 0 6.75 22h10.5A2.75 2.75 0 0 0 20 19.25v-8.5A2.75 2.75 0 0 0 17.25 8H16V6a4 4 0 0 0-4-4Zm2.5 6h-5V6a2.5 2.5 0 0 1 5 0v2Zm-7.75 1.5h10.5c.69 0 1.25.56 1.25 1.25v8.5c0 .69-.56 1.25-1.25 1.25H6.75c-.69 0-1.25-.56-1.25-1.25v-8.5c0-.69.56-1.25 1.25-1.25ZM12 13a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3Z";
        public const string PathArrowSync = "M12 3.5a8.5 8.5 0 0 0-7.7 4.88H7a.75.75 0 0 1 0 1.5H2.75A.75.75 0 0 1 2 9.13V4.88a.75.75 0 0 1 1.5 0v2.1A10 10 0 0 1 21.95 11a.75.75 0 0 1-1.5.14A8.5 8.5 0 0 0 12 3.5Zm-8.45 9.36a.75.75 0 0 1 1.5-.14A8.5 8.5 0 0 0 19.7 15.6H17a.75.75 0 0 1 0-1.5h4.25c.41 0 .75.34.75.75v4.25a.75.75 0 0 1-1.5 0V17a10 10 0 0 1-18.45-4.14Z";
        public const string PathDelete = "M10 2.5a1.5 1.5 0 0 0-1.5 1.5v1H4.25a.75.75 0 0 0 0 1.5h1.06l.84 12.66A2.5 2.5 0 0 0 8.64 21.5h6.72a2.5 2.5 0 0 0 2.49-2.34l.84-12.66h1.06a.75.75 0 0 0 0-1.5H15.5V4A1.5 1.5 0 0 0 14 2.5h-4Zm4 2.5h-4V4h4v1ZM6.81 6.5h10.38l-.83 12.56a1 1 0 0 1-1 .94H8.64a1 1 0 0 1-1-.94L6.81 6.5ZM10 9.25a.75.75 0 0 0-.75.75v6.5a.75.75 0 0 0 1.5 0V10a.75.75 0 0 0-.75-.75Zm4 0a.75.75 0 0 0-.75.75v6.5a.75.75 0 0 0 1.5 0V10a.75.75 0 0 0-.75-.75Z";
        public const string PathSearch = "M10.5 3a7.5 7.5 0 0 1 5.88 12.16l4.4 4.4a.75.75 0 1 1-1.06 1.06l-4.4-4.4A7.5 7.5 0 1 1 10.5 3Zm0 1.5a6 6 0 1 0 0 12 6 6 0 0 0 0-12Z";
        public const string PathFolder = "M4.5 4A2.5 2.5 0 0 0 2 6.5v11A2.5 2.5 0 0 0 4.5 20h15a2.5 2.5 0 0 0 2.5-2.5v-8A2.5 2.5 0 0 0 19.5 7h-6.88l-1.76-2.2A2 2 0 0 0 9.3 4H4.5Zm0 1.5h4.8a.5.5 0 0 1 .39.19L11.54 8H4.5a1 1 0 0 0-1 1V6.5a1 1 0 0 1 1-1Zm-1 4h16a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1h-15a1 1 0 0 1-1-1v-8Z";
        public const string PathAdd = "M12 3.25a.75.75 0 0 1 .75.75v7.25H20a.75.75 0 0 1 0 1.5h-7.25V20a.75.75 0 0 1-1.5 0v-7.25H4a.75.75 0 0 1 0-1.5h7.25V4a.75.75 0 0 1 .75-.75Z";
        public const string PathDismiss = "M5.22 5.22a.75.75 0 0 1 1.06 0L12 10.94l5.72-5.72a.75.75 0 1 1 1.06 1.06L13.06 12l5.72 5.72a.75.75 0 1 1-1.06 1.06L12 13.06l-5.72 5.72a.75.75 0 0 1-1.06-1.06L10.94 12 5.22 6.28a.75.75 0 0 1 0-1.06Z";
        public const string PathPlay = "M6 4.75a1.5 1.5 0 0 1 2.28-1.28l11.5 7.25a1.5 1.5 0 0 1 0 2.56l-11.5 7.25A1.5 1.5 0 0 1 6 19.25V4.75Z";
        public const string PathPause = "M6.75 4A1.75 1.75 0 0 0 5 5.75v12.5C5 19.22 5.78 20 6.75 20h2.5c.97 0 1.75-.78 1.75-1.75V5.75C11 4.78 10.22 4 9.25 4h-2.5Zm8 0A1.75 1.75 0 0 0 13 5.75v12.5c0 .97.78 1.75 1.75 1.75h2.5c.97 0 1.75-.78 1.75-1.75V5.75C19 4.78 18.22 4 17.25 4h-2.5Z";
        public const string PathStop = "M6.5 4A2.5 2.5 0 0 0 4 6.5v11A2.5 2.5 0 0 0 6.5 20h11a2.5 2.5 0 0 0 2.5-2.5v-11A2.5 2.5 0 0 0 17.5 4h-11Z";
        public const string PathDocument = "M6.5 2A2.5 2.5 0 0 0 4 4.5v15A2.5 2.5 0 0 0 6.5 22h11a2.5 2.5 0 0 0 2.5-2.5V8.31a2.5 2.5 0 0 0-.73-1.77l-3.81-3.81A2.5 2.5 0 0 0 13.69 2H6.5Zm0 1.5h6.75V7A1.75 1.75 0 0 0 15 8.75h3.5V19.5a1 1 0 0 1-1 1h-11a1 1 0 0 1-1-1v-15a1 1 0 0 1 1-1Zm8.25.56 3.19 3.19H15a.25.25 0 0 1-.25-.25V4.06ZM8.25 11.5a.75.75 0 0 0 0 1.5h7.5a.75.75 0 0 0 0-1.5h-7.5Zm0 4a.75.75 0 0 0 0 1.5h5a.75.75 0 0 0 0-1.5h-5Z";
        public const string PathCalendar = "M7.75 2a.75.75 0 0 1 .75.75V4h7V2.75a.75.75 0 0 1 1.5 0V4h1.25A2.75 2.75 0 0 1 21 6.75v11.5A2.75 2.75 0 0 1 18.25 21H5.75A2.75 2.75 0 0 1 3 18.25V6.75A2.75 2.75 0 0 1 5.75 4H7V2.75A.75.75 0 0 1 7.75 2ZM4.5 9.5v8.75c0 .69.56 1.25 1.25 1.25h12.5c.69 0 1.25-.56 1.25-1.25V9.5h-15Zm1.25-4c-.69 0-1.25.56-1.25 1.25V8h15V6.75c0-.69-.56-1.25-1.25-1.25H5.75Z";
        public const string PathStore = "M8.5 4.5a3.5 3.5 0 0 1 7 0V6h3A2.5 2.5 0 0 1 21 8.5v10a2.5 2.5 0 0 1-2.5 2.5h-13A2.5 2.5 0 0 1 3 18.5v-10A2.5 2.5 0 0 1 5.5 6h3V4.5Zm1.5 0V6h4V4.5a2 2 0 1 0-4 0ZM5.5 7.5a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h13a1 1 0 0 0 1-1v-10a1 1 0 0 0-1-1h-13Zm3 3h3v3h-3v-3Zm4 0h3v3h-3v-3Zm-4 4h3v3h-3v-3Zm4 0h3v3h-3v-3Z";
        public const string PathLink = "M13.59 4.41a4.75 4.75 0 0 1 6.72 6.72l-3 3a4.75 4.75 0 0 1-6.51.2.75.75 0 0 1 1.02-1.1 3.25 3.25 0 0 0 4.43-.16l3-3a3.25 3.25 0 0 0-4.6-4.6l-1.72 1.72a.75.75 0 0 1-1.06-1.06l1.72-1.72Zm-3.41 5.26a.75.75 0 0 1 1.02 1.1 3.25 3.25 0 0 0-4.43.16l-3 3a3.25 3.25 0 0 0 4.6 4.6l1.72-1.72a.75.75 0 1 1 1.06 1.06l-1.72 1.72a4.75 4.75 0 0 1-6.72-6.72l3-3a4.75 4.75 0 0 1 6.47-.2Z";
        public const string PathBookOpen = "M2 6.25A2.25 2.25 0 0 1 4.25 4H8.5a4.5 4.5 0 0 1 3.5 1.68A4.5 4.5 0 0 1 15.5 4h4.25A2.25 2.25 0 0 1 22 6.25v11.5A2.25 2.25 0 0 1 19.75 20H15a3 3 0 0 0-2.25 1.02.98.98 0 0 1-1.5 0A3 3 0 0 0 9 20H4.25A2.25 2.25 0 0 1 2 17.75V6.25Zm2.25-.75a.75.75 0 0 0-.75.75v11.5c0 .41.34.75.75.75H9c.88 0 1.7.26 2.25.7V8.5a3 3 0 0 0-3-3H4.25Zm8.5 13.7c.55-.44 1.37-.7 2.25-.7h4.75a.75.75 0 0 0 .75-.75V6.25a.75.75 0 0 0-.75-.75H15.5a3 3 0 0 0-2.75 3v10.7Z";
        public const string PathScales = "M12 2.25a.75.75 0 0 1 .75.75v1.5h6.5a.75.75 0 0 1 .66.4l3 6a.75.75 0 0 1-.07.75A4.25 4.25 0 0 1 15.7 11.65a.75.75 0 0 1-.07-.75l2.66-4.9h-5.54v13.5h4.5a.75.75 0 0 1 0 1.5h-10.5a.75.75 0 0 1 0-1.5h4.5V6H5.71l2.66 4.9a.75.75 0 0 1-.07.75A4.25 4.25 0 0 1 1.16 11.65a.75.75 0 0 1-.07-.75l3-6A.75.75 0 0 1 4.75 4.5h6.5V3a.75.75 0 0 1 .75-.75ZM4.75 7.12 2.75 11h4l-2-3.88Zm14.5 0L17.25 11h4l-2-3.88Z";
        public const string PathCode = "M8.78 6.22a.75.75 0 0 1 0 1.06L4.06 12l4.72 4.72a.75.75 0 1 1-1.06 1.06l-5.25-5.25a.75.75 0 0 1 0-1.06l5.25-5.25a.75.75 0 0 1 1.06 0Zm6.44 0a.75.75 0 0 1 1.06 0l5.25 5.25a.75.75 0 0 1 0 1.06l-5.25 5.25a.75.75 0 1 1-1.06-1.06L19.94 12l-4.72-4.72a.75.75 0 0 1 0-1.06Z";
        public const string PathMoon = "M12.27 2.26a.75.75 0 0 1 .76.96A7.5 7.5 0 0 0 20.78 12a.75.75 0 0 1 .96.76 9.75 9.75 0 1 1-9.47-10.5Z";
        public const string PathSunny = "M12 1.75a.75.75 0 0 1 .75.75v1.5a.75.75 0 0 1-1.5 0V2.5A.75.75 0 0 1 12 1.75Zm0 5.25a5 5 0 1 0 0 10 5 5 0 0 0 0-10ZM8.5 12a3.5 3.5 0 1 1 7 0 3.5 3.5 0 0 1-7 0ZM12 19.25a.75.75 0 0 1 .75.75v1.5a.75.75 0 0 1-1.5 0V20a.75.75 0 0 1 .75-.75ZM4.22 4.22a.75.75 0 0 1 1.06 0l1.06 1.06a.75.75 0 0 1-1.06 1.06L4.22 5.28a.75.75 0 0 1 0-1.06Zm13.44 13.44a.75.75 0 0 1 1.06 0l1.06 1.06a.75.75 0 1 1-1.06 1.06l-1.06-1.06a.75.75 0 0 1 0-1.06ZM1.75 12a.75.75 0 0 1 .75-.75h1.5a.75.75 0 0 1 0 1.5H2.5a.75.75 0 0 1-.75-.75Zm17.5 0a.75.75 0 0 1 .75-.75h1.5a.75.75 0 0 1 0 1.5H20a.75.75 0 0 1-.75-.75ZM6.34 17.66a.75.75 0 0 1 0 1.06l-1.06 1.06a.75.75 0 0 1-1.06-1.06l1.06-1.06a.75.75 0 0 1 1.06 0ZM19.78 4.22a.75.75 0 0 1 0 1.06l-1.06 1.06a.75.75 0 0 1-1.06-1.06l1.06-1.06a.75.75 0 0 1 1.06 0Z";
        public const string PathDownload = "M12 2.25a.75.75 0 0 1 .75.75v11.19l3.72-3.72a.75.75 0 1 1 1.06 1.06l-5 5a.75.75 0 0 1-1.06 0l-5-5a.75.75 0 1 1 1.06-1.06l3.72 3.72V3a.75.75 0 0 1 .75-.75ZM4.25 18.5a.75.75 0 0 0 0 1.5h15.5a.75.75 0 0 0 0-1.5H4.25Z";
        public const string PathUpload = "M11.47 2.47a.75.75 0 0 1 1.06 0l5 5a.75.75 0 0 1-1.06 1.06l-3.72-3.72V16a.75.75 0 0 1-1.5 0V4.81L7.53 8.53a.75.75 0 0 1-1.06-1.06l5-5ZM4.25 18.5a.75.75 0 0 0 0 1.5h15.5a.75.75 0 0 0 0-1.5H4.25Z";
        public const string PathWarning = "M10.03 3.66a2.25 2.25 0 0 1 3.94 0l7.75 14A2.25 2.25 0 0 1 19.75 21H4.25a2.25 2.25 0 0 1-1.97-3.34l7.75-14Zm2.63.73a.75.75 0 0 0-1.32 0l-7.75 14a.75.75 0 0 0 .66 1.11h15.5a.75.75 0 0 0 .66-1.11l-7.75-14ZM12 8.75a.75.75 0 0 1 .75.75v4.25a.75.75 0 0 1-1.5 0V9.5a.75.75 0 0 1 .75-.75Zm0 7.5a1 1 0 1 1 0 2 1 1 0 0 1 0-2Z";
        public const string PathErrorCircle = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm0 1.5a8.5 8.5 0 1 1 0 17 8.5 8.5 0 0 1 0-17Zm-3.28 5.22a.75.75 0 0 0-1.06 1.06L10.94 12l-3.28 3.22a.75.75 0 1 0 1.06 1.06L12 13.06l3.22 3.22a.75.75 0 1 0 1.06-1.06L13.06 12l3.22-3.22a.75.75 0 0 0-1.06-1.06L12 10.94 8.72 7.72Z";
        public const string PathQuestionCircle = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm0 1.5a8.5 8.5 0 1 1 0 17 8.5 8.5 0 0 1 0-17Zm0 3.75a3 3 0 0 0-2.97 2.6.75.75 0 1 0 1.49.2A1.5 1.5 0 1 1 12 11.75a.75.75 0 0 0-.75.75v1.25a.75.75 0 0 0 1.5 0v-.6A3 3 0 0 0 12 7.25Zm0 8.5a1 1 0 1 0 0 2 1 1 0 0 0 0-2Z";
        public const string PathCheckmarkCircle = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm0 1.5a8.5 8.5 0 1 1 0 17 8.5 8.5 0 0 1 0-17Zm4.28 5.72a.75.75 0 0 1 0 1.06l-5.25 5.25a.75.75 0 0 1-1.06 0L7.72 13.28a.75.75 0 0 1 1.06-1.06l1.72 1.72 4.72-4.72a.75.75 0 0 1 1.06 0Z";
        public const string PathArrowUndo = "M8.03 4.22a.75.75 0 0 1 0 1.06L4.81 8.5H14.5a6.5 6.5 0 1 1 0 13H9.75a.75.75 0 0 1 0-1.5h4.75a5 5 0 0 0 0-10H4.81l3.22 3.22a.75.75 0 0 1-1.06 1.06l-4.5-4.5a.75.75 0 0 1 0-1.06l4.5-4.5a.75.75 0 0 1 1.06 0Z";
        public const string PathStar = "M11.12 2.66a.98.98 0 0 1 1.76 0l2.43 4.92 5.44.79a.98.98 0 0 1 .54 1.67l-3.93 3.84.93 5.41a.98.98 0 0 1-1.42 1.03L12 17.77l-4.87 2.55a.98.98 0 0 1-1.42-1.03l.93-5.41-3.93-3.84a.98.98 0 0 1 .54-1.67l5.44-.79 2.43-4.92Z";
        public const string PathEdit = "M18.92 2.93a2.65 2.65 0 0 1 3.75 3.75L8.95 20.4a2.25 2.25 0 0 1-1.06.59l-4.2 1a.75.75 0 0 1-.91-.91l1-4.2c.1-.4.3-.77.59-1.06L18.92 2.93Zm2.69 1.06a1.15 1.15 0 0 0-1.63 0L5.43 16.88a.75.75 0 0 0-.2.35l-.76 3.22 3.22-.76a.75.75 0 0 0 .35-.2L21.6 5.62a1.15 1.15 0 0 0 0-1.63Z";
        public const string PathColor = "M12 2a10 10 0 0 0-10 10c0 5.52 4.48 10 10 10a2.5 2.5 0 0 0 2.5-2.5c0-.62-.24-1.2-.63-1.64a1 1 0 0 1 .75-1.66h1.88A5.5 5.5 0 0 0 22 10.7C22 5.83 17.52 2 12 2ZM7.5 11a1.5 1.5 0 1 1 0-3 1.5 1.5 0 0 1 0 3Zm3-4a1.5 1.5 0 1 1 0-3 1.5 1.5 0 0 1 0 3Zm5 0a1.5 1.5 0 1 1 0-3 1.5 1.5 0 0 1 0 3Zm3 4a1.5 1.5 0 1 1 0-3 1.5 1.5 0 0 1 0 3Z";
        public const string PathImage = "M4.5 3A2.5 2.5 0 0 0 2 5.5v13A2.5 2.5 0 0 0 4.5 21h15a2.5 2.5 0 0 0 2.5-2.5v-13A2.5 2.5 0 0 0 19.5 3h-15Zm-1 2.5a1 1 0 0 1 1-1h15a1 1 0 0 1 1 1v9.38l-3.72-3.72a1.75 1.75 0 0 0-2.47 0l-5.56 5.56-2.03-2.03a1.75 1.75 0 0 0-2.47 0L3.5 16.44V5.5ZM8 9.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3Z";
        public const string PathLightbulb = "M12 2a7 7 0 0 0-4.5 12.36c.62.53 1 1.3 1 2.14v.75A2.75 2.75 0 0 0 11.25 20h1.5a2.75 2.75 0 0 0 2.75-2.75v-.75c0-.84.38-1.61 1-2.14A7 7 0 0 0 12 2ZM8.5 13.22a5.5 5.5 0 1 1 7 0c-.8.68-1.32 1.62-1.46 2.66h-4.08c-.14-1.04-.66-1.98-1.46-2.66ZM10 21.25a.75.75 0 0 0 0 1.5h4a.75.75 0 0 0 0-1.5h-4Z";
        public const string PathTimer = "M9.75 1.5a.75.75 0 0 0 0 1.5h4.5a.75.75 0 0 0 0-1.5h-4.5ZM12 4.5a8.75 8.75 0 1 0 0 17.5 8.75 8.75 0 0 0 0-17.5ZM4.75 13.25a7.25 7.25 0 1 1 14.5 0 7.25 7.25 0 0 1-14.5 0ZM12 8a.75.75 0 0 0-.75.75v4.5c0 .41.34.75.75.75h3a.75.75 0 0 0 0-1.5h-2.25V8.75A.75.75 0 0 0 12 8Z";
        public const string PathWindowNew = "M4.5 4A2.5 2.5 0 0 0 2 6.5v11A2.5 2.5 0 0 0 4.5 20h11a2.5 2.5 0 0 0 2.5-2.5V14a.75.75 0 0 0-1.5 0v3.5a1 1 0 0 1-1 1h-11a1 1 0 0 1-1-1v-11a1 1 0 0 1 1-1H10a.75.75 0 0 0 0-1.5H4.5ZM14.75 3a.75.75 0 0 0 0 1.5h3.69l-6.22 6.22a.75.75 0 1 0 1.06 1.06L19.5 5.56v3.69a.75.75 0 0 0 1.5 0V3.75A.75.75 0 0 0 20.25 3h-5.5Z";
        public const string PathBuilding = "M6.5 2A2.5 2.5 0 0 0 4 4.5v16.75c0 .41.34.75.75.75h14.5a.75.75 0 0 0 .75-.75V9.5A2.5 2.5 0 0 0 17.5 7H15V4.5A2.5 2.5 0 0 0 12.5 2h-6ZM5.5 4.5a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1V20.5h-8V4.5Zm9.5 4h2.5a1 1 0 0 1 1 1v11h-3.5v-12ZM8 6.5a.75.75 0 0 0 0 1.5h3a.75.75 0 0 0 0-1.5H8Zm0 3.5a.75.75 0 0 0 0 1.5h3a.75.75 0 0 0 0-1.5H8Zm0 3.5a.75.75 0 0 0 0 1.5h3a.75.75 0 0 0 0-1.5H8Z";
        public const string PathPerson = "M12 2.5a4.75 4.75 0 1 0 0 9.5 4.75 4.75 0 0 0 0-9.5ZM6 14a3 3 0 0 0-3 3v1.25C3 20.32 6.58 22 12 22s9-1.68 9-3.75V17a3 3 0 0 0-3-3H6Z";
        public const string PathTag = "M3 5.25A2.25 2.25 0 0 1 5.25 3h5.34a2.25 2.25 0 0 1 1.59.66l8.16 8.16a2.25 2.25 0 0 1 0 3.18l-5.34 5.34a2.25 2.25 0 0 1-3.18 0l-8.16-8.16A2.25 2.25 0 0 1 3 10.59V5.25ZM7.5 6a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3Z";
        public const string PathNavigation = "M3.75 5.25a.75.75 0 0 0 0 1.5h16.5a.75.75 0 0 0 0-1.5H3.75Zm0 6a.75.75 0 0 0 0 1.5h16.5a.75.75 0 0 0 0-1.5H3.75Zm0 6a.75.75 0 0 0 0 1.5h16.5a.75.75 0 0 0 0-1.5H3.75Z";
        public const string PathSubtract = "M4.75 11.25a.75.75 0 0 0 0 1.5h14.5a.75.75 0 0 0 0-1.5H4.75Z";
        public const string PathMaximize = "M6.25 4A2.25 2.25 0 0 0 4 6.25v11.5A2.25 2.25 0 0 0 6.25 20h11.5A2.25 2.25 0 0 0 20 17.75V6.25A2.25 2.25 0 0 0 17.75 4H6.25ZM5.5 6.25c0-.41.34-.75.75-.75h11.5c.41 0 .75.34.75.75v11.5a.75.75 0 0 1-.75.75H6.25a.75.75 0 0 1-.75-.75V6.25Z";
        public const string PathRestore = "M8 4.25A2.25 2.25 0 0 1 10.25 2h7.5A2.25 2.25 0 0 1 20 4.25v7.5A2.25 2.25 0 0 1 17.75 14H16V8.25A2.25 2.25 0 0 0 13.75 6H8V4.25ZM6.25 7.5A2.25 2.25 0 0 0 4 9.75v8A2.25 2.25 0 0 0 6.25 20h8a2.25 2.25 0 0 0 2.25-2.25v-8A2.25 2.25 0 0 0 14.25 7.5h-8Zm-.75 2.25c0-.41.34-.75.75-.75h8c.41 0 .75.34.75.75v8a.75.75 0 0 1-.75.75h-8a.75.75 0 0 1-.75-.75v-8Z";
        public const string PathChevronUp = "M4.47 15.53a.75.75 0 0 1 0-1.06l7-7a.75.75 0 0 1 1.06 0l7 7a.75.75 0 1 1-1.06 1.06L12 9.06l-6.47 6.47a.75.75 0 0 1-1.06 0Z";
        public const string PathChevronDown = "M4.47 8.47a.75.75 0 0 1 1.06 0L12 14.94l6.47-6.47a.75.75 0 1 1 1.06 1.06l-7 7a.75.75 0 0 1-1.06 0l-7-7a.75.75 0 0 1 0-1.06Z";
        public const string PathCircleFilled = "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z";

        static FluentIcons()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;

                // Sidebar & Modules
                Register("💻", PathDesktop);
                Register("🖥️", PathDesktop);
                Register("🖥", PathDesktop);
                Register("🛠️", PathWrenchScrewdriver);
                Register("🛠", PathWrenchScrewdriver);
                Register("🔧", PathWrenchScrewdriver);
                Register(">_", PathWindowConsole);
                Register("📜", PathWindowConsole);
                Register("⌨️", PathWindowConsole);
                Register("⌨", PathWindowConsole);
                Register("🧰", PathToolbox);
                Register("📦", PathBox);
                Register("🧹", PathBroom);
                Register("🚀", PathRocket);
                Register("⚡", PathFlash);
                Register("📋", PathClipboard);
                Register("⚙️", PathSettings);
                Register("⚙", PathSettings);
                Register("ℹ️", PathInfo);
                Register("ℹ", PathInfo);

                // Hardware, OS & Diagnostics
                Register("🧠", PathDeveloperBoard);
                Register("🪟", PathWindows);
                Register("💾", PathHardDrive);
                Register("💿", PathHardDrive);
                Register("📀", PathHardDrive);
                Register("🎮", PathGames);
                Register("🧩", PathPuzzlePiece);
                Register("🌐", PathGlobe);
                Register("🔋", PathBattery);
                Register("🔌", PathPlug);
                Register("🌡️", PathTemperature);
                Register("🌡", PathTemperature);
                Register("🔥", PathFire);
                Register("🪶", PathLeaf);
                Register("📊", PathChart);
                Register("📈", PathChart);

                // Actions, Status & Controls
                Register("✨", PathSparkle);
                Register("🛡️", PathShieldCheckmark);
                Register("🛡", PathShieldCheckmark);
                Register("🔒", PathLockClosed);
                Register("🔐", PathLockClosed);
                Register("🔄", PathArrowSync);
                Register("🔃", PathArrowSync);
                Register("↻", PathArrowSync);
                Register("🗑️", PathDelete);
                Register("🗑", PathDelete);
                Register("🔍", PathSearch);
                Register("🔎", PathSearch);
                Register("📁", PathFolder);
                Register("📂", PathFolder);
                Register("🗂️", PathFolder);
                Register("🗂", PathFolder);
                Register("➕", PathAdd);
                Register("✕", PathDismiss);
                Register("✖️", PathDismiss);
                Register("✖", PathDismiss);
                Register("❌", PathErrorCircle);
                Register("▶️", PathPlay);
                Register("▶", PathPlay);
                Register("⏸️", PathPause);
                Register("⏸", PathPause);
                Register("⏹️", PathStop);
                Register("⏹", PathStop);
                Register("📄", PathDocument);
                Register("📃", PathDocument);
                Register("📝", PathDocument);
                Register("📅", PathCalendar);
                Register("📆", PathCalendar);
                Register("🏪", PathStore);
                Register("🛍️", PathStore);
                Register("🔗", PathLink);
                Register("📖", PathBookOpen);
                Register("📚", PathBookOpen);
                Register("⚖️", PathScales);
                Register("⚖", PathScales);
                Register("🐙", PathCode);
                Register("🌙", PathMoon);
                Register("☀️", PathSunny);
                Register("☀", PathSunny);
                Register("📥", PathDownload);
                Register("⬇️", PathDownload);
                Register("⬇", PathDownload);
                Register("📤", PathUpload);
                Register("⬆️", PathUpload);
                Register("⬆", PathUpload);
                Register("⚠️", PathWarning);
                Register("⚠", PathWarning);
                Register("🛑", PathErrorCircle);
                Register("⛔", PathErrorCircle);
                Register("🚫", PathErrorCircle);
                Register("❓", PathQuestionCircle);
                Register("✔️", PathCheckmarkCircle);
                Register("✔", PathCheckmarkCircle);
                Register("✅", PathCheckmarkCircle);
                Register("↩️", PathArrowUndo);
                Register("↩", PathArrowUndo);
                Register("⏪", PathArrowUndo);
                Register("⭐", PathStar);
                Register("🌟", PathStar);
                Register("✏️", PathEdit);
                Register("✏", PathEdit);
                Register("🎨", PathColor);
                Register("🖼️", PathImage);
                Register("🖼", PathImage);
                Register("💡", PathLightbulb);
                Register("⏳", PathTimer);
                Register("⌛", PathTimer);
                Register("🕒", PathTimer);
                Register("🗗", PathWindowNew);
                Register("🏢", PathBuilding);
                Register("👤", PathPerson);
                Register("👥", PathPerson);
                Register("🏷️", PathTag);
                Register("🏷", PathTag);
                Register("☰", PathNavigation);
                Register("─", PathSubtract);
                Register("□", PathMaximize);
                Register("❐", PathRestore);
                Register("▲", PathChevronUp);
                Register("▼", PathChevronDown);

                // Colored Preset Swatch Tokens
                RegisterColorSwatch("🔵", "#0EA5E9");
                RegisterColorSwatch("🟢", "#10B981");
                RegisterColorSwatch("🟣", "#8B5CF6");
                RegisterColorSwatch("🟠", "#F97316");
                RegisterColorSwatch("🔴", "#EF4444");
                RegisterColorSwatch("🟡", "#F59E0B");
                RegisterColorSwatch("🌸", "#EC4899");

                // Sort tokens longest-first so variation selectors (e.g. 🛠️ vs 🛠) match greedily
                _orderedTokens.Sort((a, b) => b.Length.CompareTo(a.Length));
                _initialized = true;
            }
        }

        public static void RegisterGlobalVisualTreeHooks()
        {
            EnsureInitialized();
            if (_classHandlerRegistered) return;
            _classHandlerRegistered = true;
            try
            {
                EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnContainerLoaded));
                EventManager.RegisterClassHandler(typeof(UserControl), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnContainerLoaded));
                EventManager.RegisterClassHandler(typeof(Panel), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnContainerLoaded));
            }
            catch { }
        }

        private static void OnContainerLoaded(object sender, RoutedEventArgs e)
        {
            DependencyObject d = sender as DependencyObject;
            if (d != null)
            {
                UpgradeVisualTree(d);
            }
        }

        private static void Register(string token, string pathData)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(pathData)) return;
            if (!_iconPaths.ContainsKey(token))
            {
                _orderedTokens.Add(token);
            }
            _iconPaths[token] = pathData;
        }

        private static void RegisterColorSwatch(string token, string hexColor)
        {
            Register(token, PathCircleFilled);
            _fixedColorTokens[token] = hexColor;
        }

        public static bool TryExtractLeadingToken(string input, out string matchedToken, out string pathData, out string remainingText)
        {
            EnsureInitialized();
            matchedToken = null;
            pathData = null;
            remainingText = null;

            if (string.IsNullOrEmpty(input)) return false;

            string trimmedStart = input.TrimStart(' ');
            if (string.IsNullOrEmpty(trimmedStart)) return false;

            for (int i = 0; i < _orderedTokens.Count; i++)
            {
                string tok = _orderedTokens[i];
                if (trimmedStart.StartsWith(tok, StringComparison.Ordinal))
                {
                    matchedToken = tok;
                    pathData = _iconPaths[tok];
                    string rest = trimmedStart.Substring(tok.Length);
                    // Also strip any orphan variation selector \uFE0F if present
                    while (rest.Length > 0 && rest[0] == '\uFE0F')
                    {
                        rest = rest.Substring(1);
                    }
                    remainingText = rest.TrimStart(' ');
                    return true;
                }
            }
            return false;
        }

        public static Path CreateVectorPath(string pathData, double size, Brush explicitFill = null, DependencyObject bindForegroundTarget = null)
        {
            Path p = new Path();
            try
            {
                Geometry geom = Geometry.Parse(pathData);
                if (geom.CanFreeze) geom.Freeze();
                p.Data = geom;
            }
            catch { }

            p.Stretch = Stretch.Uniform;
            p.Width = size;
            p.Height = size;
            p.VerticalAlignment = VerticalAlignment.Center;
            p.HorizontalAlignment = HorizontalAlignment.Center;
            p.SnapsToDevicePixels = true;
            RenderOptions.SetEdgeMode(p, EdgeMode.Aliased);
            // Smooth vector anti-aliasing
            RenderOptions.SetEdgeMode(p, EdgeMode.Unspecified);

            if (explicitFill != null)
            {
                p.Fill = explicitFill;
            }
            else if (bindForegroundTarget != null)
            {
                Binding b = new Binding("Foreground");
                b.Source = bindForegroundTarget;
                p.SetBinding(Shape.FillProperty, b);
            }
            else
            {
                Binding b = new Binding("Foreground");
                b.RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1);
                p.SetBinding(Shape.FillProperty, b);
            }

            return p;
        }

        public static Brush GetExplicitBrushForToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            string hex;
            if (_fixedColorTokens.TryGetValue(token, out hex))
            {
                try
                {
                    Color c = (Color)ColorConverter.ConvertFromString(hex);
                    SolidColorBrush sb = new SolidColorBrush(c);
                    sb.Freeze();
                    return sb;
                }
                catch { }
            }
            return null;
        }

        public static UIElement CreateIconElement(string token, double size = 14.0, Brush explicitFill = null)
        {
            EnsureInitialized();
            string pathData;
            if (!_iconPaths.TryGetValue(token ?? "", out pathData))
            {
                string matched;
                string rest;
                if (!TryExtractLeadingToken(token, out matched, out pathData, out rest))
                {
                    pathData = PathInfo;
                }
            }
            Brush fill = explicitFill ?? GetExplicitBrushForToken(token);
            return CreateVectorPath(pathData, size, fill, null);
        }

        /// <summary>
        /// Walks the visual and logical tree and upgrades TextBlocks, CheckBoxes, and ComboBoxItems
        /// that begin with mapped Unicode emoji/symbol tokens into crisp Fluent UI SVG Path vectors.
        /// Safe to call repeatedly across language or theme updates.
        /// </summary>
        public static void UpgradeVisualTree(DependencyObject root)
        {
            if (root == null) return;
            EnsureInitialized();

            try
            {
                UpgradeNodeRecursive(root);
            }
            catch { }
        }

        private static void UpgradeNodeRecursive(DependencyObject node)
        {
            if (node == null) return;

            TextBlock tb = node as TextBlock;
            if (tb != null)
            {
                UpgradeTextBlock(tb);
                return;
            }

            CheckBox chk = node as CheckBox;
            if (chk != null)
            {
                UpgradeContentControl(chk, 13.0);
            }

            ComboBox cmb = node as ComboBox;
            if (cmb != null && cmb.Items != null)
            {
                for (int i = 0; i < cmb.Items.Count; i++)
                {
                    ComboBoxItem cbi = cmb.Items[i] as ComboBoxItem;
                    if (cbi != null)
                    {
                        UpgradeContentControl(cbi, 13.0);
                    }
                }
            }

            int count = 0;
            if (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
            {
                try { count = VisualTreeHelper.GetChildrenCount(node); } catch { count = 0; }
            }

            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    DependencyObject child = null;
                    try { child = VisualTreeHelper.GetChild(node, i); } catch { }
                    if (child != null)
                    {
                        UpgradeNodeRecursive(child);
                    }
                }
            }
            else
            {
                foreach (object logChild in LogicalTreeHelper.GetChildren(node))
                {
                    DependencyObject dChild = logChild as DependencyObject;
                    if (dChild != null)
                    {
                        UpgradeNodeRecursive(dChild);
                    }
                }
            }
        }

        public static void UpgradeContentControl(ContentControl cc, double iconSize = 13.0)
        {
            if (cc == null) return;
            string s = cc.Content as string;
            if (string.IsNullOrEmpty(s)) return;
            if (BindingOperations.IsDataBound(cc, ContentControl.ContentProperty)) return;

            string token;
            string pathData;
            string rest;
            if (!TryExtractLeadingToken(s, out token, out pathData, out rest)) return;

            Brush explicitFill = GetExplicitBrushForToken(token);
            Path iconPath = CreateVectorPath(pathData, iconSize, explicitFill, cc);

            if (string.IsNullOrEmpty(rest))
            {
                cc.Content = iconPath;
            }
            else
            {
                StackPanel sp = new StackPanel();
                sp.Orientation = Orientation.Horizontal;
                sp.VerticalAlignment = VerticalAlignment.Center;
                iconPath.Margin = new Thickness(0, 0, 6, 0);
                sp.Children.Add(iconPath);

                TextBlock lbl = new TextBlock();
                lbl.Text = rest;
                lbl.VerticalAlignment = VerticalAlignment.Center;
                Binding fgBind = new Binding("Foreground") { Source = cc };
                lbl.SetBinding(TextBlock.ForegroundProperty, fgBind);
                sp.Children.Add(lbl);

                cc.Content = sp;
            }
        }

        public static void UpgradeTextBlock(TextBlock tb)
        {
            if (tb == null) return;
            // Never overwrite data-bound TextBlock.TextProperty or multi-Run TextBlocks (such as lblCpuTag + lblCpuDetails)
            if (BindingOperations.IsDataBound(tb, TextBlock.TextProperty)) return;
            if (tb.Inlines.Count > 1) return;
            if (tb.Inlines.Count == 1 && !(tb.Inlines.FirstInline is Run)) return;

            string rawText = tb.Text;
            if (string.IsNullOrEmpty(rawText)) return;

            string token;
            string pathData;
            string rest;
            if (!TryExtractLeadingToken(rawText, out token, out pathData, out rest)) return;

            double fontSize = tb.FontSize > 0 ? tb.FontSize : 12.0;
            double iconSize = Math.Max(11.0, Math.Min(22.0, Math.Round(fontSize * 1.08)));

            Brush explicitFill = GetExplicitBrushForToken(token);
            Path iconPath = CreateVectorPath(pathData, iconSize, explicitFill, tb);

            tb.Inlines.Clear();

            if (string.IsNullOrEmpty(rest))
            {
                InlineUIContainer container = new InlineUIContainer(iconPath);
                container.BaselineAlignment = BaselineAlignment.Center;
                tb.Inlines.Add(container);
            }
            else
            {
                iconPath.Margin = new Thickness(0, 0, 6, -1.5);
                InlineUIContainer container = new InlineUIContainer(iconPath);
                container.BaselineAlignment = BaselineAlignment.Center;
                tb.Inlines.Add(container);
                tb.Inlines.Add(new Run(rest));
            }
        }
    }

    /// <summary>
    /// ValueConverter used inside Button and RadioButton ControlTemplates in App.xaml and MainWindow.xaml.
    /// Automatically converts any leading emoji/symbol token in Content into a Fluent UI System Icon SVG Path vector.
    /// </summary>
    public class FluentContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;
            if (string.IsNullOrEmpty(str))
            {
                return value;
            }

            string token;
            string pathData;
            string rest;
            if (!FluentIcons.TryExtractLeadingToken(str, out token, out pathData, out rest))
            {
                return value;
            }

            double iconSize = 14.0;
            if (parameter != null)
            {
                double parsed;
                if (double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) && parsed > 0)
                {
                    iconSize = parsed;
                }
            }
            else if (token == "─" || token == "□" || token == "❐")
            {
                iconSize = 11.0;
            }
            else if (token == "✕" && string.IsNullOrEmpty(rest))
            {
                iconSize = 11.0;
            }
            else if (token == "▲" || token == "▼")
            {
                iconSize = 10.5;
            }

            Brush explicitFill = FluentIcons.GetExplicitBrushForToken(token);
            Path iconPath = FluentIcons.CreateVectorPath(pathData, iconSize, explicitFill, null);

            if (string.IsNullOrEmpty(rest))
            {
                return iconPath;
            }

            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Center;

            iconPath.Margin = new Thickness(0, 0, 7, 0);
            sp.Children.Add(iconPath);

            TextBlock tb = new TextBlock();
            tb.Text = rest;
            tb.VerticalAlignment = VerticalAlignment.Center;
            tb.TextTrimming = TextTrimming.CharacterEllipsis;

            Binding fgBind = new Binding("Foreground");
            fgBind.RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1);
            tb.SetBinding(TextBlock.ForegroundProperty, fgBind);

            Binding fsBind = new Binding("FontSize");
            fsBind.RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1);
            tb.SetBinding(TextBlock.FontSizeProperty, fsBind);

            Binding fwBind = new Binding("FontWeight");
            fwBind.RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1);
            tb.SetBinding(TextBlock.FontWeightProperty, fwBind);

            Binding ffBind = new Binding("FontFamily");
            ffBind.RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1);
            tb.SetBinding(TextBlock.FontFamilyProperty, ffBind);

            sp.Children.Add(tb);
            return sp;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
