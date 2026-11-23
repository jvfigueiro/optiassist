using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Win32;
using OptiAssist.Platform;
using OptiAssist.Debloater;

namespace OptiAssist.Tests
{
    public class DebloaterEndToEndTests
    {
        private const string TestRegSubKey = @"Software\OptiAssist_Test";
        private const string TestRegFull = @"HKCU\Software\OptiAssist_Test";
        private int _passed = 0;
        private int _failed = 0;

        public static int Main(string[] args)
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine(" OptiAssist Fase F4/F5 End-to-End Debloater QA Suite ");
            Console.WriteLine("=====================================================");
            Console.WriteLine();

            DebloaterEndToEndTests suite = new DebloaterEndToEndTests();
            string tempBackupDir = Path.Combine(Path.GetTempPath(), "OptiAssist_QA_Backups_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempBackupDir);

            try
            {
                DebloaterCatalogLoader.CustomBackupsDirectoryOverride = tempBackupDir;
                suite.RunAll(tempBackupDir);
            }
            finally
            {
                DebloaterCatalogLoader.CustomBackupsDirectoryOverride = null;
                CleanupTestRegistry();
                try { if (Directory.Exists(tempBackupDir)) Directory.Delete(tempBackupDir, true); } catch { }
            }

            Console.WriteLine();
            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine(string.Format("QA Results: {0} Passed, {1} Failed", suite._passed, suite._failed));
            Console.WriteLine("-----------------------------------------------------");
            return suite._failed == 0 ? 0 : 1;
        }

        private static void CleanupTestRegistry()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(TestRegSubKey, false); } catch { }
        }

        private void Assert(bool condition, string name, string details = "")
        {
            if (condition)
            {
                _passed++;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  [PASS] ");
                Console.ResetColor();
                Console.WriteLine(name);
            }
            else
            {
                _failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("  [FAIL] ");
                Console.ResetColor();
                Console.WriteLine(name + (string.IsNullOrEmpty(details) ? "" : " -> " + details));
            }
        }

        private void RunAll(string tempBackupDir)
        {
            List<DebloatTweakDefinition> catalog = DebloaterCatalogLoader.LoadAllTweaks();
            TestSevenProfilesMatrix(catalog);
            TestBuildAndEditionSimulation(catalog);
            TestApplyDetectRevertLifecycle(tempBackupDir);
            TestQuickFixSnapshotRollback(tempBackupDir);
        }

        private static DebloatTweakDefinition FindById(List<DebloatTweakDefinition> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && string.Equals(list[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return list[i];
                }
            }
            return null;
        }

        #region 1. 7-PROFILES MATRIX QA

        private void TestSevenProfilesMatrix(List<DebloatTweakDefinition> catalog)
        {
            Console.WriteLine("[1/4] Validating 7-Profiles Matrix & Critical Guardrails...");

            Assert(catalog != null && catalog.Count >= 55,
                "All 7 modular catalog JSON files loaded (>= 55 tweaks)",
                "Count=" + (catalog != null ? catalog.Count.ToString() : "0"));

            // 1. Hibernation: off in laptop & laptopGaming, on in desktops & ultraportable
            DebloatTweakDefinition hib = FindById(catalog, "pwr-hibernation-off");
            Assert(hib != null &&
                hib.profiles.gaming == "on" &&
                hib.profiles.workstation == "on" &&
                hib.profiles.hybrid == "on" &&
                hib.profiles.conventional == "on" &&
                hib.profiles.ultraportable == "on" &&
                hib.profiles.laptop == "off" &&
                hib.profiles.laptopGaming == "off",
                "Hibernation disabled in Desktops & Ultraportable, preserved in Laptop & Gaming Laptop");

            // 2. Win32PrioritySeparation=0x18: on ONLY in workstation & hybrid, ask in gaming, off in others
            DebloatTweakDefinition sched = FindById(catalog, "ui-scheduler-bg-services");
            Assert(sched != null &&
                sched.profiles.workstation == "on" &&
                sched.profiles.hybrid == "on" &&
                sched.profiles.gaming == "ask" &&
                sched.profiles.conventional == "off" &&
                sched.profiles.laptop == "off" &&
                sched.profiles.laptopGaming == "off" &&
                sched.profiles.ultraportable == "off",
                "Win32PrioritySeparation=0x18 applied only in Workstation & Hybrid (ask in Gaming)");

            // 3. Smart App Control: ALWAYS ask in ALL 7 profiles + doubleConfirm=true
            DebloatTweakDefinition sac = FindById(catalog, "sec-smart-app-control");
            Assert(sac != null &&
                sac.doubleConfirm &&
                sac.profiles.gaming == "ask" &&
                sac.profiles.workstation == "ask" &&
                sac.profiles.hybrid == "ask" &&
                sac.profiles.conventional == "ask" &&
                sac.profiles.laptop == "ask" &&
                sac.profiles.laptopGaming == "ask" &&
                sac.profiles.ultraportable == "ask",
                "Smart App Control is always 'ask' across all 7 profiles with doubleConfirm=true");

            // 4. SysMain: blocked on HDD & eMMC, allowed on SSD/NVMe
            DebloatTweakDefinition sysmain = FindById(catalog, "svc-sysmain-ssd");
            DebloaterEngine engine = new DebloaterEngine();
            string reason;

            PlatformInfo pEmmc = new PlatformInfo();
            pEmmc.FormFactor = ChassisFormFactor.Ultraportable;
            pEmmc.Storage.SystemDiskMediaType = "SSD";
            pEmmc.Storage.IsSystemEmmcOrSd = true;
            bool okEmmc = engine.EvaluateGatingSimulated(sysmain, 22631, "Professional", pEmmc, out reason);
            Assert(!okEmmc, "SysMain blocked on eMMC storage", reason);

            PlatformInfo pHdd = new PlatformInfo();
            pHdd.FormFactor = ChassisFormFactor.Desktop;
            pHdd.Storage.SystemDiskMediaType = "HDD";
            bool okHdd = engine.EvaluateGatingSimulated(sysmain, 22631, "Professional", pHdd, out reason);
            Assert(!okHdd, "SysMain blocked on mechanical HDD storage", reason);

            PlatformInfo pSsd = new PlatformInfo();
            pSsd.FormFactor = ChassisFormFactor.Desktop;
            pSsd.Storage.SystemDiskMediaType = "SSD";
            pSsd.Storage.IsSystemEmmcOrSd = false;
            bool okSsd = engine.EvaluateGatingSimulated(sysmain, 22631, "Professional", pSsd, out reason);
            Assert(okSsd, "SysMain allowed on confirmed SSD/NVMe storage", reason);

            // 5. WSearch intact in Workstation, Laptop, Conventional
            DebloatTweakDefinition wsearch = FindById(catalog, "svc-wsearch");
            Assert(wsearch != null &&
                wsearch.profiles.workstation == "off" &&
                wsearch.profiles.laptop == "off" &&
                wsearch.profiles.conventional == "off" &&
                wsearch.profiles.gaming == "on" &&
                wsearch.profiles.hybrid == "on" &&
                wsearch.profiles.ultraportable == "on",
                "WSearch preserved in Workstation, Laptop & Conventional Desktop");

            // 6. Store AutoDownload pre-selected in Gaming, Hybrid, Ultraportable; ask in others
            DebloatTweakDefinition store = FindById(catalog, "pol-store-auto-download");
            Assert(store != null &&
                store.profiles.gaming == "on" &&
                store.profiles.hybrid == "on" &&
                store.profiles.ultraportable == "on" &&
                store.profiles.workstation == "ask" &&
                store.profiles.conventional == "ask" &&
                store.profiles.laptop == "ask" &&
                store.profiles.laptopGaming == "ask",
                "Store AutoDownload pre-selected in Gaming/Hybrid/Ultraportable and opt-in in others");

            // 7. Safe Temp Cleanup uses systemCleaner engine (no cleanmgr.exe)
            DebloatTweakDefinition cleaner = FindById(catalog, "app-safe-temp-cleanup");
            Assert(cleaner != null && cleaner.apply.Count == 1 &&
                string.Equals(cleaner.apply[0].type, "systemCleaner", StringComparison.OrdinalIgnoreCase),
                "Temp cleanup integrated via native systemCleaner action (no cleanmgr.exe)");

            Console.WriteLine();
        }

        #endregion

        #region 2. BUILD & EDITION SIMULATION

        private void TestBuildAndEditionSimulation(List<DebloatTweakDefinition> catalog)
        {
            Console.WriteLine("[2/4] Simulating OS Builds & Editions (17763, 19044, 22621, 26100)...");
            DebloaterEngine engine = new DebloaterEngine();
            PlatformInfo desktopSsd = new PlatformInfo();
            desktopSsd.FormFactor = ChassisFormFactor.Desktop;
            desktopSsd.Storage.SystemDiskMediaType = "SSD";

            DebloatTweakDefinition reserved = FindById(catalog, "ui-reserved-storage-off"); // minBuild 18362
            DebloatTweakDefinition widgets = FindById(catalog, "dll-debloat-widgets");      // minBuild 19041
            DebloatTweakDefinition dynLight = FindById(catalog, "ui-dynamic-lighting-off"); // minBuild 22621
            DebloatTweakDefinition endTask = FindById(catalog, "ui-taskbar-end-task");      // minBuild 22631
            DebloatTweakDefinition vbscript = FindById(catalog, "cap-vbscript");            // minBuild 26100

            string r;

            // Win10 1809 (17763)
            Assert(!engine.EvaluateGatingSimulated(reserved, 17763, "Professional", desktopSsd, out r) &&
                   !engine.EvaluateGatingSimulated(widgets, 17763, "Professional", desktopSsd, out r) &&
                   !engine.EvaluateGatingSimulated(endTask, 17763, "Professional", desktopSsd, out r) &&
                   !engine.EvaluateGatingSimulated(vbscript, 17763, "Professional", desktopSsd, out r),
                "Win10 1809 (17763): properly gates out 18362+, 19041+, 22631+, 26100+ tweaks");

            // Win10 IoT LTSC 2021 (19044)
            Assert(engine.EvaluateGatingSimulated(reserved, 19044, "IoTEnterpriseS", desktopSsd, out r) &&
                   engine.EvaluateGatingSimulated(widgets, 19044, "IoTEnterpriseS", desktopSsd, out r) &&
                   !engine.EvaluateGatingSimulated(dynLight, 19044, "IoTEnterpriseS", desktopSsd, out r) &&
                   !engine.EvaluateGatingSimulated(vbscript, 19044, "IoTEnterpriseS", desktopSsd, out r),
                "Win10 IoT LTSC 2021 (19044): enables ReservedStorage/Widgets, blocks Win11 tweaks");

            // Win11 22H2 (22621)
            Assert(engine.EvaluateGatingSimulated(dynLight, 22621, "Professional", desktopSsd, out r) &&
                   !engine.EvaluateGatingSimulated(endTask, 22621, "Professional", desktopSsd, out r) &&
                   !engine.EvaluateGatingSimulated(vbscript, 22621, "Professional", desktopSsd, out r),
                "Win11 22H2 (22621): enables Dynamic Lighting, blocks 23H2 TaskbarEndTask & 24H2 VBSCRIPT");

            // Win11 24H2 (26100)
            Assert(engine.EvaluateGatingSimulated(dynLight, 26100, "Enterprise", desktopSsd, out r) &&
                   engine.EvaluateGatingSimulated(endTask, 26100, "Enterprise", desktopSsd, out r) &&
                   engine.EvaluateGatingSimulated(vbscript, 26100, "Enterprise", desktopSsd, out r),
                "Win11 24H2 (26100): enables Dynamic Lighting, TaskbarEndTask & VBSCRIPT");

            Console.WriteLine();
        }

        #endregion

        #region 3. APPLY -> DETECT -> REVERT FULL LIFECYCLE

        private void TestApplyDetectRevertLifecycle(string tempBackupDir)
        {
            Console.WriteLine("[3/4] Testing Full Apply -> Detect -> Revert Lifecycle (HKCU\\Software\\OptiAssist_Test)...");
            CleanupTestRegistry();

            // Pre-seed existing values in isolated HKCU test key
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(TestRegSubKey))
            {
                k.SetValue("ExistingDWord", 7, RegistryValueKind.DWord);
                k.SetValue("SettingsPageVisibility", "hide:cortana", RegistryValueKind.String);
            }

            // Tweak A: overwrite existing DWord + create new String
            DebloatTweakDefinition tweakA = new DebloatTweakDefinition();
            tweakA.id = "qa-test-reg-types";
            tweakA.titleFallback = "QA Registry Types Tweak";
            tweakA.needsReboot = true;
            tweakA.detect.Add(new DebloatDetectRule { type = "registry", path = TestRegFull, name = "ExistingDWord", equals = "1" });
            tweakA.detect.Add(new DebloatDetectRule { type = "registry", path = TestRegFull, name = "NewStringVal", equals = "applied_ok" });
            tweakA.apply.Add(new DebloatActionStep { type = "registry", operation = "set", path = TestRegFull, name = "ExistingDWord", kind = "DWord", value = "1" });
            tweakA.apply.Add(new DebloatActionStep { type = "registry", operation = "set", path = TestRegFull, name = "NewStringVal", kind = "String", value = "applied_ok" });

            // Tweak B: non-destructive mergeHideList on SettingsPageVisibility
            DebloatTweakDefinition tweakB = new DebloatTweakDefinition();
            tweakB.id = "qa-test-merge-hide";
            tweakB.titleFallback = "QA Merge SettingsPageVisibility";
            tweakB.detect.Add(new DebloatDetectRule { type = "registry", path = TestRegFull, name = "SettingsPageVisibility", equals = "hide:cortana;aicomponents" });
            tweakB.apply.Add(new DebloatActionStep { type = "registry", operation = "mergeHideList", path = TestRegFull, name = "SettingsPageVisibility", kind = "String", value = "aicomponents" });

            DebloaterEngine engine = new DebloaterEngine();
            int curBuild = DebloaterEngine.GetWindowsBuildNumber();

            // 1. Pre-Apply state must be Pending
            Assert(engine.EvaluateTweakState(tweakA, curBuild) == DebloatTweakState.Pending,
                "Pre-apply state of Tweak A is Pending");
            Assert(engine.EvaluateTweakState(tweakB, curBuild) == DebloatTweakState.Pending,
                "Pre-apply state of Tweak B (mergeHideList) is Pending");

            // 2. Apply batch with snapshot
            List<DebloatTweakDefinition> batch = new List<DebloatTweakDefinition>() { tweakA, tweakB };
            DebloatBackupSession session = engine.ApplyTweaksWithSnapshot(batch, null, null, "gaming");

            Assert(session != null && session.entries.Count == 3,
                "ApplyTweaksWithSnapshot captured 3 pre-change entries",
                session != null ? ("Entries=" + session.entries.Count) : "null");

            // 3. Post-Apply Detect must be Applied
            Assert(tweakA.CurrentState == DebloatTweakState.Applied,
                "Post-apply state of Tweak A transitioned Pending -> Applied");
            Assert(tweakB.CurrentState == DebloatTweakState.Applied,
                "Post-apply state of Tweak B (mergeHideList) transitioned Pending -> Applied");

            // Verify exact merged string
            object mergedObj;
            RegistryValueKind mergedKind;
            DebloaterEngine.TryReadRegistryValue(TestRegFull, "SettingsPageVisibility", out mergedObj, out mergedKind);
            Assert(string.Equals(Convert.ToString(mergedObj), "hide:cortana;aicomponents", StringComparison.Ordinal),
                "SettingsPageVisibility merged non-destructively to 'hide:cortana;aicomponents'",
                Convert.ToString(mergedObj));

            // Verify Maintenance Report summary file generated
            string reportFile = Path.Combine(tempBackupDir, "last_debloat_report.txt");
            string reportContent = File.Exists(reportFile) ? File.ReadAllText(reportFile) : "";
            Assert(reportContent.IndexOf("Applied Profile    : gaming", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   reportContent.IndexOf("Pending Reboot     : YES", StringComparison.OrdinalIgnoreCase) >= 0,
                "last_debloat_report.txt generated with profile, hardware & pending reboot status");

            // 4. Granular RevertSingleTweak on Tweak B
            bool revSingleOk = engine.RevertSingleTweak(tweakB.id, null);
            DebloaterEngine.TryReadRegistryValue(TestRegFull, "SettingsPageVisibility", out mergedObj, out mergedKind);
            Assert(revSingleOk && string.Equals(Convert.ToString(mergedObj), "hide:cortana", StringComparison.Ordinal),
                "RevertSingleTweak restored SettingsPageVisibility back to 'hide:cortana'",
                Convert.ToString(mergedObj));

            // 5. Full Session Rollback via RevertLatestBackupSession
            string revSessionId;
            bool revSessionOk = engine.RevertLatestBackupSession(null, out revSessionId);
            object dwObj;
            RegistryValueKind dwKind;
            bool dwExists = DebloaterEngine.TryReadRegistryValue(TestRegFull, "ExistingDWord", out dwObj, out dwKind);
            object strObj;
            RegistryValueKind strKind;
            bool strExists = DebloaterEngine.TryReadRegistryValue(TestRegFull, "NewStringVal", out strObj, out strKind);

            Assert(revSessionOk && dwExists && dwKind == RegistryValueKind.DWord && Convert.ToInt32(dwObj) == 7,
                "RevertLatestBackupSession restored ExistingDWord back to 7 (DWord)");
            Assert(!strExists,
                "RevertLatestBackupSession deleted newly created NewStringVal (existedBefore=false)");

            Console.WriteLine();
        }

        #endregion

        #region 4. QUICKFIX SNAPSHOT ROLLBACK (KNOWN ISSUES)

        private void TestQuickFixSnapshotRollback(string tempBackupDir)
        {
            Console.WriteLine("[4/4] Testing KnownIssues QuickFix Snapshot & Rollback Integration...");
            string quirkKey = TestRegSubKey + @"\Quirks";
            string quirkFull = TestRegFull + @"\Quirks";

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(quirkKey))
            {
                k.SetValue("EnableUlps", 1, RegistryValueKind.DWord);
            }

            DiagnosticRecommendationItem ulpsFix = new DiagnosticRecommendationItem();
            ulpsFix.Id = "ki-radeon-hd5000-hd6000-ulps";
            ulpsFix.TargetRegistryPath = quirkFull;
            ulpsFix.TargetValueName = "EnableUlps";
            ulpsFix.TargetValueKind = "DWord";
            ulpsFix.TargetValueData = "0";

            bool applied = PlatformDiagnosticService.Instance.ApplyQuickRegistryFix(ulpsFix, null, tempBackupDir);
            object valAfter;
            RegistryValueKind kindAfter;
            DebloaterEngine.TryReadRegistryValue(quirkFull, "EnableUlps", out valAfter, out kindAfter);
            Assert(applied && Convert.ToInt32(valAfter) == 0,
                "PlatformDiagnosticService.ApplyQuickRegistryFix set EnableUlps = 0 with snapshot");

            // Revert using DebloaterEngine!
            DebloaterEngine engine = new DebloaterEngine();
            bool reverted = engine.RevertSingleTweak("ki-radeon-hd5000-hd6000-ulps", null);
            object valRestored;
            RegistryValueKind kindRestored;
            DebloaterEngine.TryReadRegistryValue(quirkFull, "EnableUlps", out valRestored, out kindRestored);
            Assert(reverted && Convert.ToInt32(valRestored) == 1,
                "DebloaterEngine.RevertSingleTweak reverted QuickFix snapshot back to EnableUlps = 1",
                Convert.ToString(valRestored));

            Console.WriteLine();
        }

        #endregion
    }
}
