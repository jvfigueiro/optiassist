using System;
using System.Collections.Generic;
using System.IO;
using OptiAssist;

namespace OptiAssist.Tests
{
    /// <summary>
    /// Validates AppDeploymentHelper.DeployApplicationFiles in an isolated temporary
    /// directory (simulated installation) without modifying %ProgramFiles% or the Registry.
    /// Strict .NET Framework 4.8 / C# 5 compatible.
    /// </summary>
    public static class DeploymentComplianceTests
    {
        private static int _passed = 0;
        private static int _failed = 0;

        private static void Assert(bool condition, string message)
        {
            if (condition)
            {
                Console.WriteLine("  [PASS] " + message);
                _passed++;
            }
            else
            {
                Console.WriteLine("  [FAIL] " + message);
                _failed++;
            }
        }

        public static int Main(string[] args)
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine(" OptiAssist Simulated Installation Compliance Tests  ");
            Console.WriteLine("=====================================================");
            Console.WriteLine();

            string sourceDir = Directory.GetCurrentDirectory();
            string simRoot = Path.Combine(Path.GetTempPath(), "OptiAssist_SimInstall_" + Guid.NewGuid().ToString("N").Substring(0, 8));

            try
            {
                Directory.CreateDirectory(simRoot);
                AppDeploymentHelper.DeployApplicationFiles(sourceDir, simRoot, null, null);

                string[] requiredFiles = new string[]
                {
                    "OptiAssist.exe",
                    "LICENSE",
                    "THIRD-PARTY-NOTICES.md",
                    "SOURCE-INFO.md",
                    @"THIRD-PARTY\MPL-2.0.txt",
                    @"THIRD-PARTY\Apache-2.0.txt",
                    @"THIRD-PARTY\BSD-3-Clause.txt",
                    @"THIRD-PARTY\MIT.txt",
                    @"Config\settings.json",
                    @"Config\Languages\en-US.json",
                    @"Config\Languages\pt-BR.json",
                    @"Config\Data\pci.ids.min",
                    @"Config\Data\microarch.json",
                    @"Config\Data\known_issues.json",
                    @"Config\Addons\AppManager\AppManager.dll",
                    @"Config\Addons\MaintenanceReport\MaintenanceReport.dll",
                    @"Config\Addons\PCAssist\PCAssist.dll",
                    @"Config\Addons\PCAssist\LibreHardwareMonitorLib.dll",
                    @"Config\Addons\PCAssist\BlackSharp.Core.dll",
                    @"Config\Addons\PCAssist\DiskInfoToolkit.dll",
                    @"Config\Addons\PCAssist\RAMSPDToolkit-NDD.dll",
                    @"Config\Addons\PCAssist\HidSharp.dll",
                    @"Config\Addons\PCAssist\System.Buffers.dll",
                    @"Config\Addons\PCAssist\System.Memory.dll",
                    @"Config\Addons\PCAssist\System.Numerics.Vectors.dll",
                    @"Config\Addons\PCAssist\System.Runtime.CompilerServices.Unsafe.dll",
                    @"Config\Addons\StartupManager\StartupManager.dll",
                    @"Config\Addons\SystemCleaner\SystemCleaner.dll",
                    @"Config\Addons\Toolbox\Toolbox.dll"
                };

                foreach (string rel in requiredFiles)
                {
                    string full = Path.Combine(simRoot, rel);
                    bool existsAndNonEmpty = File.Exists(full) && (new FileInfo(full).Length > 0);
                    Assert(existsAndNonEmpty, "Installed file preserved: " + rel);
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(simRoot))
                    {
                        Directory.Delete(simRoot, true);
                    }
                }
                catch { }
            }

            Console.WriteLine();
            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine(string.Format("Install Compliance Results: {0} Passed, {1} Failed", _passed, _failed));
            Console.WriteLine("-----------------------------------------------------");

            return _failed > 0 ? 1 : 0;
        }
    }
}
