using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using OptiAssist.Platform;

namespace OptiAssist.Tests
{
    public class PlatformDiagnosticTests
    {
        private int _passed = 0;
        private int _failed = 0;

        public static int Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine(" OptiAssist Fase F2 Platform & Diagnostics Tests ");
            Console.WriteLine("=================================================");
            Console.WriteLine();

            PlatformDiagnosticTests suite = new PlatformDiagnosticTests();
            suite.RunAll();

            Console.WriteLine();
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine(string.Format("Test Results: {0} Passed, {1} Failed", suite._passed, suite._failed));
            Console.WriteLine("-------------------------------------------------");

            return suite._failed == 0 ? 0 : 1;
        }

        public void RunAll()
        {
            TestCpuMicroarchDetection();
            TestPciResolutionAndGpuFamilies();
            TestProfileRecommenderAll7Profiles();
            TestWin11CompatAdvisorLayersAndHardGates();
            TestKnownIssuesEngineTriggers();
        }

        private void Assert(bool condition, string testName, string details = "")
        {
            if (condition)
            {
                _passed++;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  [PASS] ");
                Console.ResetColor();
                Console.WriteLine(testName);
            }
            else
            {
                _failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("  [FAIL] ");
                Console.ResetColor();
                Console.WriteLine(testName + (string.IsNullOrEmpty(details) ? "" : " -> " + details));
            }
        }

        #region 1. CPU MICROARCHITECTURE & DISAMBIGUATION TESTS

        private void TestCpuMicroarchDetection()
        {
            Console.WriteLine("[1/5] Testing CPU Microarchitecture & Disambiguation...");
            MicroarchDatabase db = MicroarchDatabase.Default;

            // Test 1: Raptor Lake i5-13400
            MicroarchMatchResult r1 = db.Match("GenuineIntel", 6, 191, 2, "13th Gen Intel(R) Core(TM) i5-13400");
            Assert(r1 != null && r1.Microarchitecture == "Raptor Lake" && r1.HasSse42 && r1.HasCmpxchg16b,
                "Intel Core i5-13400 matches Raptor Lake (2022)",
                r1 != null ? r1.Microarchitecture : "null");

            // Test 2: Intel 8E Stepping 9 Disambiguation -> Kaby Lake Mobile
            MicroarchMatchResult r2 = db.Match("GenuineIntel", 6, 142, 9, "Intel(R) Core(TM) i5-7200U CPU @ 2.50GHz");
            Assert(r2 != null && r2.Microarchitecture == "Kaby Lake" && r2.Suffix == "U",
                "Intel 6/142/9 i5-7200U disambiguated as Kaby Lake (U suffix)",
                r2 != null ? (r2.Microarchitecture + " " + r2.Suffix) : "null");

            // Test 3: Intel 8E Stepping 10 Disambiguation -> Coffee Lake Mobile (Kaby Lake-R)
            MicroarchMatchResult r3 = db.Match("GenuineIntel", 6, 142, 10, "Intel(R) Core(TM) i5-8250U CPU @ 1.60GHz");
            Assert(r3 != null && r3.Microarchitecture == "Coffee Lake" && r3.Suffix == "U",
                "Intel 6/142/10 i5-8250U disambiguated as Coffee Lake (U suffix)",
                r3 != null ? (r3.Microarchitecture + " " + r3.Suffix) : "null");

            // Test 4: Intel 8E Stepping 11 Disambiguation -> Whiskey Lake Mobile
            MicroarchMatchResult r4 = db.Match("GenuineIntel", 6, 142, 11, "Intel(R) Core(TM) i5-8265U CPU @ 1.60GHz");
            Assert(r4 != null && r4.Microarchitecture == "Whiskey Lake" && r4.Suffix == "U",
                "Intel 6/142/11 i5-8265U disambiguated as Whiskey Lake (U suffix)",
                r4 != null ? (r4.Microarchitecture + " " + r4.Suffix) : "null");

            // Test 5: Intel 9E Stepping 9 Disambiguation -> Kaby Lake Desktop
            MicroarchMatchResult r5 = db.Match("GenuineIntel", 6, 158, 9, "Intel(R) Core(TM) i7-7700K CPU @ 4.20GHz");
            Assert(r5 != null && r5.Microarchitecture == "Kaby Lake" && r5.Suffix == "K",
                "Intel 6/158/9 i7-7700K disambiguated as Kaby Lake (K suffix)",
                r5 != null ? (r5.Microarchitecture + " " + r5.Suffix) : "null");

            // Test 6: Intel 9E Stepping 10 Disambiguation -> Coffee Lake Desktop
            MicroarchMatchResult r6 = db.Match("GenuineIntel", 6, 158, 10, "Intel(R) Core(TM) i7-8700K CPU @ 3.70GHz");
            Assert(r6 != null && r6.Microarchitecture == "Coffee Lake" && r6.Suffix == "K",
                "Intel 6/158/10 i7-8700K disambiguated as Coffee Lake (K suffix)",
                r6 != null ? (r6.Microarchitecture + " " + r6.Suffix) : "null");

            // Test 7: Intel Alder Lake-N Gracemont (Intel Processor N100)
            MicroarchMatchResult r7 = db.Match("GenuineIntel", 6, 190, 0, "Intel(R) Processor N100");
            Assert(r7 != null && r7.Microarchitecture == "Gracemont" && r7.Suffix == "N",
                "Intel Processor N100 matches Gracemont (N suffix)",
                r7 != null ? (r7.Microarchitecture + " " + r7.Suffix) : "null");

            // Test 8: Intel Silvermont Bay Trail (Atom Z3735F)
            MicroarchMatchResult r8 = db.Match("GenuineIntel", 6, 55, 8, "Intel(R) Atom(TM) CPU  Z3735F @ 1.33GHz");
            Assert(r8 != null && r8.Microarchitecture == "Silvermont",
                "Intel Atom Z3735F matches Silvermont",
                r8 != null ? r8.Microarchitecture : "null");

            // Test 9: AMD Zen 4 Ryzen 7 7800X3D
            MicroarchMatchResult r9 = db.Match("AuthenticAMD", 25, 97, 2, "AMD Ryzen 7 7800X3D 8-Core Processor");
            Assert(r9 != null && r9.Microarchitecture == "Zen 4" && r9.Suffix == "X3D",
                "AMD Ryzen 7 7800X3D matches Zen 4 (X3D suffix)",
                r9 != null ? (r9.Microarchitecture + " " + r9.Suffix) : "null");

            // Test 10: Legacy Intel Core 2 Quad Q6600 (no SSE4.2)
            MicroarchMatchResult r10 = db.Match("GenuineIntel", 6, 15, 11, "Intel(R) Core(TM)2 Quad CPU Q6600 @ 2.40GHz");
            Assert(r10 != null && r10.Microarchitecture == "Core 2" && !r10.HasSse42,
                "Intel Core 2 Quad Q6600 matches Core 2 and lacks SSE4.2",
                r10 != null ? ("Uarch=" + r10.Microarchitecture + " SSE42=" + r10.HasSse42) : "null");

            Console.WriteLine();
        }

        #endregion

        #region 2. PCI RESOLUTION & GPU ARCHITECTURES TESTS

        private void TestPciResolutionAndGpuFamilies()
        {
            Console.WriteLine("[2/5] Testing PCI Resolution & GPU Architecture Derivation...");
            PciIdsDatabase pci = PciIdsDatabase.Default;

            // NVIDIA RTX 2070 (TU106)
            PciLookupResult n1 = pci.Lookup("10de", "1f07");
            Assert(n1 != null && n1.ArchitectureFamily == "Turing" && n1.IsDiscreteGpu,
                "PCI 10de:1f07 -> NVIDIA RTX 2070 (Turing, Discrete)",
                n1 != null ? n1.ArchitectureFamily : "null");

            // NVIDIA RTX 3090 (GA102)
            PciLookupResult n2 = pci.Lookup("10de", "2204");
            Assert(n2 != null && n2.ArchitectureFamily == "Ampere" && n2.IsDiscreteGpu,
                "PCI 10de:2204 -> NVIDIA RTX 3090 (Ampere, Discrete)",
                n2 != null ? n2.ArchitectureFamily : "null");

            // NVIDIA RTX 4090 (AD102)
            PciLookupResult n3 = pci.Lookup("10de", "2684");
            Assert(n3 != null && n3.ArchitectureFamily == "Ada Lovelace" && n3.IsDiscreteGpu,
                "PCI 10de:2684 -> NVIDIA RTX 4090 (Ada Lovelace, Discrete)",
                n3 != null ? n3.ArchitectureFamily : "null");

            // NVIDIA RTX A6000 Workstation (GA102GL)
            PciLookupResult n4 = pci.Lookup("10de", "2230");
            Assert(n4 != null && n4.ArchitectureFamily == "Ampere" && n4.DeviceName.IndexOf("RTX A6000", StringComparison.OrdinalIgnoreCase) >= 0,
                "PCI 10de:2230 -> NVIDIA RTX A6000 Workstation",
                n4 != null ? n4.DeviceName : "null");

            // AMD Radeon HD 5870 (Cypress / TeraScale)
            PciLookupResult a1 = pci.Lookup("1002", "6898");
            Assert(a1 != null && a1.ArchitectureFamily == "TeraScale" && a1.IsDiscreteGpu,
                "PCI 1002:6898 -> AMD Radeon HD 5870 (TeraScale, Discrete)",
                a1 != null ? a1.ArchitectureFamily : "null");

            // AMD Radeon RX 7900 XTX (Navi 31 / RDNA 3)
            PciLookupResult a2 = pci.Lookup("1002", "744c");
            Assert(a2 != null && a2.ArchitectureFamily == "RDNA 3" && a2.IsDiscreteGpu,
                "PCI 1002:744c -> AMD Radeon RX 7900 XTX (RDNA 3, Discrete)",
                a2 != null ? a2.ArchitectureFamily : "null");

            // Intel HD Graphics 2000 (Sandy Bridge iGPU)
            PciLookupResult i1 = pci.Lookup("8086", "0102");
            Assert(i1 != null && i1.IsIntegratedGpu && i1.DeviceName.IndexOf("HD Graphics 2000", StringComparison.OrdinalIgnoreCase) >= 0,
                "PCI 8086:0102 -> Intel HD Graphics 2000 (Integrated)",
                i1 != null ? i1.DeviceName : "null");

            // Intel Arc A750 (Alchemist discrete)
            PciLookupResult i2 = pci.Lookup("8086", "56a0");
            Assert(i2 != null && i2.ArchitectureFamily == "Arc Alchemist" && i2.IsDiscreteGpu,
                "PCI 8086:56a0 -> Intel Arc A750 (Arc Alchemist, Discrete)",
                i2 != null ? i2.ArchitectureFamily : "null");

            Console.WriteLine();
        }

        #endregion

        #region 3. 7 PROFILES CLASSIFICATION TESTS

        private void TestProfileRecommenderAll7Profiles()
        {
            Console.WriteLine("[3/5] Testing ProfileRecommender for All 7 Profiles + Generic...");

            // 1. Gaming Desktop Profile
            PlatformInfo pGaming = new PlatformInfo();
            pGaming.FormFactor = ChassisFormFactor.Desktop;
            pGaming.Cpu.ProcessorNameString = "AMD Ryzen 7 7800X3D 8-Core Processor";
            pGaming.Cpu.Suffix = "X3D";
            pGaming.Gpus.Add(new GpuDeviceInfo { Name = "NVIDIA GeForce RTX 3080", IsDiscrete = true });
            pGaming.Storage.SystemDiskMediaType = "SSD";
            pGaming.Memory.TotalPhysicalGb = 32.0;

            ProfileRecommendation rGaming = ProfileRecommender.RecommendProfile(pGaming);
            Assert(rGaming.ProfileId == DebloaterProfileId.Gaming && rGaming.ProfileCode == "gaming",
                "Desktop + X3D CPU + Discrete GPU -> Gaming Profile",
                rGaming.ProfileCode);

            // 2. Workstation Profile
            PlatformInfo pWork = new PlatformInfo();
            pWork.FormFactor = ChassisFormFactor.Desktop;
            pWork.Cpu.ProcessorNameString = "AMD Ryzen Threadripper PRO 3995WX 64-Cores";
            pWork.Cpu.PhysicalCores = 64;
            pWork.Memory.TotalPhysicalGb = 128.0;
            pWork.Memory.IsEcc = true;
            pWork.Gpus.Add(new GpuDeviceInfo { Name = "NVIDIA RTX A6000", IsDiscrete = true, ArchitectureFamily = "Quadro Workstation" });
            pWork.Storage.TotalNvmeDrivesCount = 3;

            ProfileRecommendation rWork = ProfileRecommender.RecommendProfile(pWork);
            Assert(rWork.ProfileId == DebloaterProfileId.Workstation && rWork.ProfileCode == "workstation",
                "Desktop + Threadripper + ECC RAM + Workstation GPU -> Workstation Profile",
                rWork.ProfileCode);

            // 3. Hybrid Profile (P/E cores + High RAM)
            PlatformInfo pHybrid = new PlatformInfo();
            pHybrid.FormFactor = ChassisFormFactor.Desktop;
            pHybrid.Cpu.ProcessorNameString = "13th Gen Intel(R) Core(TM) i7-13700K";
            pHybrid.Cpu.HasHybridCores = true;
            pHybrid.Cpu.PerformanceCores = 8;
            pHybrid.Cpu.EfficientCores = 8;
            pHybrid.Memory.TotalPhysicalGb = 64.0;
            pHybrid.Gpus.Add(new GpuDeviceInfo { Name = "NVIDIA GeForce RTX 4070", IsDiscrete = true });

            ProfileRecommendation rHybrid = ProfileRecommender.RecommendProfile(pHybrid);
            Assert(rHybrid.ProfileId == DebloaterProfileId.Hybrid && rHybrid.ProfileCode == "hybrid",
                "Desktop + Hybrid P/E CPU + 64GB RAM -> Hybrid Profile",
                rHybrid.ProfileCode);

            // 4. Conventional Desktop Profile
            PlatformInfo pConv = new PlatformInfo();
            pConv.FormFactor = ChassisFormFactor.Desktop;
            pConv.Cpu.ProcessorNameString = "Intel(R) Core(TM) i3-10100 CPU @ 3.60GHz";
            pConv.Cpu.PhysicalCores = 4;
            pConv.Memory.TotalPhysicalGb = 16.0;
            pConv.Gpus.Add(new GpuDeviceInfo { Name = "Intel(R) UHD Graphics 630", IsIntegrated = true });

            ProfileRecommendation rConv = ProfileRecommender.RecommendProfile(pConv);
            Assert(rConv.ProfileId == DebloaterProfileId.Conventional && rConv.ProfileCode == "conventional",
                "Desktop + i3 CPU + iGPU only -> Conventional Desktop Profile",
                rConv.ProfileCode);

            // 5. Laptop Profile (General)
            PlatformInfo pLap = new PlatformInfo();
            pLap.FormFactor = ChassisFormFactor.Laptop;
            pLap.HasBattery = true;
            pLap.Cpu.ProcessorNameString = "Intel(R) Core(TM) i5-1135G7 @ 2.40GHz";
            pLap.Memory.TotalPhysicalGb = 16.0;
            pLap.Gpus.Add(new GpuDeviceInfo { Name = "Intel(R) Iris(R) Xe Graphics", IsIntegrated = true });

            ProfileRecommendation rLap = ProfileRecommender.RecommendProfile(pLap);
            Assert(rLap.ProfileId == DebloaterProfileId.Laptop || rLap.ProfileId == DebloaterProfileId.Ultraportable,
                "Mobile Laptop + Balanced Core i5 -> Laptop Profile",
                rLap.ProfileCode);

            // 6. Gaming Laptop Profile
            PlatformInfo pLapGame = new PlatformInfo();
            pLapGame.FormFactor = ChassisFormFactor.Laptop;
            pLapGame.HasBattery = true;
            pLapGame.Cpu.ProcessorNameString = "13th Gen Intel(R) Core(TM) i7-13700HX";
            pLapGame.Cpu.Suffix = "HX";
            pLapGame.Gpus.Add(new GpuDeviceInfo { Name = "NVIDIA GeForce RTX 4060 Laptop GPU", IsDiscrete = true });

            ProfileRecommendation rLapGame = ProfileRecommender.RecommendProfile(pLapGame);
            Assert(rLapGame.ProfileId == DebloaterProfileId.LaptopGaming && rLapGame.ProfileCode == "laptopGaming",
                "Mobile Laptop + HX CPU + Discrete GPU -> Gaming Laptop Profile",
                rLapGame.ProfileCode);

            // 7. Ultraportable Laptop Profile
            PlatformInfo pUltra = new PlatformInfo();
            pUltra.FormFactor = ChassisFormFactor.Ultraportable;
            pUltra.HasBattery = true;
            pUltra.Cpu.ProcessorNameString = "Intel(R) Processor N100";
            pUltra.Cpu.Suffix = "N";
            pUltra.Cpu.Microarchitecture = "Gracemont";
            pUltra.Storage.IsSystemEmmcOrSd = true;

            ProfileRecommendation rUltra = ProfileRecommender.RecommendProfile(pUltra);
            Assert(rUltra.ProfileId == DebloaterProfileId.Ultraportable && rUltra.ProfileCode == "ultraportable",
                "Mobile + N100 Gracemont + eMMC -> Ultraportable Profile",
                rUltra.ProfileCode);

            // 8. Generic / Virtual Machine
            PlatformInfo pVm = new PlatformInfo();
            pVm.IsVirtualMachine = true;
            pVm.HypervisorName = "VMware";

            ProfileRecommendation rVm = ProfileRecommender.RecommendProfile(pVm);
            Assert(rVm.ProfileId == DebloaterProfileId.Generic && rVm.ProfileCode == "generic",
                "Virtual Machine (VMware) -> Generic Profile",
                rVm.ProfileCode);

            // 9. Persisted User Override
            ProfileRecommendation rOver = ProfileRecommender.RecommendProfile(pGaming, "workstation");
            Assert(rOver.ProfileId == DebloaterProfileId.Workstation && rOver.IsUserOverridden,
                "Persisted user override switches recommended profile to 'workstation'",
                rOver.ProfileCode);

            Console.WriteLine();
        }

        #endregion

        #region 4. WIN11 COMPAT ADVISOR LAYERS & HARD-GATES TESTS

        private void TestWin11CompatAdvisorLayersAndHardGates()
        {
            Console.WriteLine("[4/5] Testing Win11CompatAdvisor Layers (A, B, C) and Hard-Gates...");

            // Hard Gate 1 Failure: Missing SSE4.2 (Core 2 Quad)
            PlatformInfo pNoSse = new PlatformInfo();
            pNoSse.Cpu.HasSse42 = false;
            pNoSse.Cpu.HasCmpxchg16b = true;
            pNoSse.OsFirmware.Firmware = SystemFirmwareType.Uefi;

            Win11CompatReport rNoSse = Win11CompatAdvisor.Evaluate(pNoSse);
            Assert(!rNoSse.HardGatesPassed && rNoSse.Tier == Win11CompatTier.LayerC,
                "Lacks SSE4.2 -> Hard gate fails, classified as Layer C",
                "HardGatesPassed=" + rNoSse.HardGatesPassed);

            // Hard Gate 2 Failure: Legacy BIOS mode
            PlatformInfo pBios = new PlatformInfo();
            pBios.Cpu.HasSse42 = true;
            pBios.Cpu.HasCmpxchg16b = true;
            pBios.OsFirmware.Firmware = SystemFirmwareType.Bios;

            Win11CompatReport rBios = Win11CompatAdvisor.Evaluate(pBios);
            Assert(!rBios.HardGatesPassed && rBios.Tier == Win11CompatTier.LayerC,
                "Legacy BIOS Firmware -> Hard gate fails, classified as Layer C",
                "HardGatesPassed=" + rBios.HardGatesPassed);

            // Hard Gate 3 Failure: Bay Trail 32-bit UEFI
            PlatformInfo pBay = new PlatformInfo();
            pBay.Cpu.HasSse42 = true;
            pBay.Cpu.HasCmpxchg16b = true;
            pBay.Cpu.Microarchitecture = "Silvermont";
            pBay.OsFirmware.Firmware = SystemFirmwareType.Uefi;
            pBay.OsFirmware.IsBayTrail32BitUefi = true;

            Win11CompatReport rBay = Win11CompatAdvisor.Evaluate(pBay);
            Assert(!rBay.HardGatesPassed && rBay.Tier == Win11CompatTier.LayerC,
                "Bay Trail 32-bit UEFI -> Hard gate fails, classified as Layer C",
                "HardGatesPassed=" + rBay.HardGatesPassed);

            // Layer B: Sandy Bridge (2011) with UEFI, SSD, 8GB RAM
            PlatformInfo pSandy = new PlatformInfo();
            pSandy.Cpu.HasSse42 = true;
            pSandy.Cpu.HasCmpxchg16b = true;
            pSandy.Cpu.Microarchitecture = "Sandy Bridge";
            pSandy.Cpu.ReleaseYear = 2011;
            pSandy.OsFirmware.Firmware = SystemFirmwareType.Uefi;
            pSandy.Memory.TotalPhysicalGb = 8.0;
            pSandy.Storage.SystemDiskMediaType = "SSD";
            pSandy.Gpus.Add(new GpuDeviceInfo { VendorId = "8086", DeviceId = "0102" }); // Intel HD 2000

            Win11CompatReport rSandy = Win11CompatAdvisor.Evaluate(pSandy);
            Assert(rSandy.HardGatesPassed && rSandy.Tier == Win11CompatTier.LayerB && rSandy.Warnings.Count > 0,
                "Sandy Bridge + HD 2000 + UEFI -> Layer B with HD 2000 DWM stutter warning",
                rSandy.Title);

            // Layer A: Ivy Bridge+ / 8th Gen+ with UEFI, SSD, 16GB RAM
            PlatformInfo pLayerA = new PlatformInfo();
            pLayerA.Cpu.HasSse42 = true;
            pLayerA.Cpu.HasCmpxchg16b = true;
            pLayerA.Cpu.Microarchitecture = "Coffee Lake";
            pLayerA.Cpu.ReleaseYear = 2018;
            pLayerA.OsFirmware.Firmware = SystemFirmwareType.Uefi;
            pLayerA.Memory.TotalPhysicalGb = 16.0;
            pLayerA.Storage.SystemDiskMediaType = "SSD";
            pLayerA.Gpus.Add(new GpuDeviceInfo { VendorId = "10de", DeviceId = "1f07", IsDiscrete = true });

            Win11CompatReport rLayerA = Win11CompatAdvisor.Evaluate(pLayerA);
            Assert(rLayerA.HardGatesPassed && rLayerA.Tier == Win11CompatTier.LayerA,
                "8th Gen Core + UEFI + SSD + RTX 2070 -> Layer A (Highly Compatible)",
                rLayerA.Title);

            Console.WriteLine();
        }

        #endregion

        #region 5. KNOWN ISSUES ENGINE TRIGGERS TESTS

        private void TestKnownIssuesEngineTriggers()
        {
            Console.WriteLine("[5/5] Testing KnownIssuesEngine for 13 Documented Scenarios...");
            KnownIssuesEngine engine = KnownIssuesEngine.Default;

            // Scenario 1: nForce Chipset DWM Stutter
            PlatformInfo p1 = new PlatformInfo();
            p1.OsFirmware.CurrentBuild = 19045;
            p1.PnpDevices.Add(new PnpDeviceItem { VendorId = "10de", PciDeviceId = "0369" }); // nForce 430
            List<DiagnosticRecommendationItem> i1 = engine.EvaluateIssues(p1);
            Assert(HasIssue(i1, "ki-nforce-transparency"), "1. ki-nforce-transparency triggered for nForce 10de:0369");

            // Scenario 2: Legacy SiS GPU
            PlatformInfo p2 = new PlatformInfo();
            p2.OsFirmware.CurrentBuild = 19045;
            p2.PnpDevices.Add(new PnpDeviceItem { VendorId = "1039", PciDeviceId = "0671" }); // SiS 671
            List<DiagnosticRecommendationItem> i2 = engine.EvaluateIssues(p2);
            Assert(HasIssue(i2, "ki-sis-via-powervr-bsod"), "2. ki-sis-via-powervr-bsod triggered for SiS 1039:0671");

            // Scenario 3: Intel GMA 950
            PlatformInfo p3 = new PlatformInfo();
            p3.OsFirmware.CurrentBuild = 19045;
            p3.PnpDevices.Add(new PnpDeviceItem { VendorId = "8086", PciDeviceId = "2772" }); // GMA 950
            List<DiagnosticRecommendationItem> i3 = engine.EvaluateIssues(p3);
            Assert(HasIssue(i3, "ki-intel-gma-legacy"), "3. ki-intel-gma-legacy triggered for Intel GMA 950 8086:2772");

            // Scenario 4: Broadcom Wi-Fi
            PlatformInfo p4 = new PlatformInfo();
            p4.OsFirmware.CurrentBuild = 19045;
            p4.PnpDevices.Add(new PnpDeviceItem { VendorId = "14e4", PciDeviceId = "4365" }); // BCM43142
            List<DiagnosticRecommendationItem> i4 = engine.EvaluateIssues(p4);
            Assert(HasIssue(i4, "ki-broadcom-wifi-win10"), "4. ki-broadcom-wifi-win10 triggered for Broadcom 14e4:4365");

            // Scenario 5: IDT Audio Codec
            PlatformInfo p5 = new PlatformInfo();
            p5.OsFirmware.CurrentBuild = 19045;
            p5.PnpDevices.Add(new PnpDeviceItem { VendorId = "111d", PciDeviceId = "7675" }); // IDT 92HD73E
            List<DiagnosticRecommendationItem> i5 = engine.EvaluateIssues(p5);
            Assert(HasIssue(i5, "ki-idt-sigmatel-audio"), "5. ki-idt-sigmatel-audio triggered for IDT 111d:7675");

            // Scenario 6: AMD Switchable dGPU on Laptop
            PlatformInfo p6 = new PlatformInfo();
            p6.FormFactor = ChassisFormFactor.Laptop;
            p6.HasBattery = true;
            p6.OsFirmware.CurrentBuild = 19045;
            p6.PnpDevices.Add(new PnpDeviceItem { VendorId = "1002", PciDeviceId = "6900" }); // Topaz XT / R7 M440
            List<DiagnosticRecommendationItem> i6 = engine.EvaluateIssues(p6);
            Assert(HasIssue(i6, "ki-amd-switchable-dgpu-freeze"), "6. ki-amd-switchable-dgpu-freeze triggered for mobile AMD 1002:6900");

            // Scenario 7: ABNT2 Keyboard Scancode Map
            PlatformInfo p7 = new PlatformInfo();
            p7.FormFactor = ChassisFormFactor.Laptop;
            p7.HasBattery = true;
            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-BR");
            List<DiagnosticRecommendationItem> i7 = engine.EvaluateIssues(p7);
            Assert(HasIssue(i7, "ki-abnt2-scancode-map"), "7. ki-abnt2-scancode-map triggered for laptop under pt-BR");

            // Scenario 8: Radeon HD 5000/6000 ULPS
            PlatformInfo p8 = new PlatformInfo();
            p8.OsFirmware.CurrentBuild = 19045;
            p8.PnpDevices.Add(new PnpDeviceItem { VendorId = "1002", PciDeviceId = "6898" }); // HD 5870
            List<DiagnosticRecommendationItem> i8 = engine.EvaluateIssues(p8);
            Assert(HasIssue(i8, "ki-radeon-hd5000-hd6000-ulps"), "8. ki-radeon-hd5000-hd6000-ulps triggered for Radeon HD 5870 1002:6898");

            // Scenario 9: Lenovo BT & Energy Management
            PlatformInfo p9 = new PlatformInfo();
            p9.FormFactor = ChassisFormFactor.Laptop;
            p9.HasBattery = true;
            p9.Oem.Manufacturer = "LENOVO";
            List<DiagnosticRecommendationItem> i9 = engine.EvaluateIssues(p9);
            Assert(HasIssue(i9, "ki-lenovo-bt-energy-management"), "9. ki-lenovo-bt-energy-management triggered for Lenovo Laptop");

            // Scenario 10: HP Pavilion x360 TXE
            PlatformInfo p10 = new PlatformInfo();
            p10.Oem.Manufacturer = "Hewlett-Packard";
            p10.Oem.ProductName = "HP Pavilion x360 11 Notebook PC";
            p10.Cpu.Microarchitecture = "Silvermont";
            List<DiagnosticRecommendationItem> i10 = engine.EvaluateIssues(p10);
            Assert(HasIssue(i10, "ki-hp-pavilion-x360-txe"), "10. ki-hp-pavilion-x360-txe triggered for HP Pavilion x360 Bay Trail");

            // Scenario 11: Intel AC 7260 Bluetooth
            PlatformInfo p11 = new PlatformInfo();
            p11.OsFirmware.CurrentBuild = 19045;
            p11.PnpDevices.Add(new PnpDeviceItem { VendorId = "8086", PciDeviceId = "08b1" }); // Intel AC 7260
            List<DiagnosticRecommendationItem> i11 = engine.EvaluateIssues(p11);
            Assert(HasIssue(i11, "ki-intel-ac7260-bluetooth"), "11. ki-intel-ac7260-bluetooth triggered for Intel AC 7260 8086:08b1");

            // Scenario 12: Acer Aspire E5-553G Win11 Block
            PlatformInfo p12 = new PlatformInfo();
            p12.Oem.Manufacturer = "Acer";
            p12.Oem.ProductName = "Aspire E5-553G";
            p12.PnpDevices.Add(new PnpDeviceItem { VendorId = "1002", PciDeviceId = "6900" });
            List<DiagnosticRecommendationItem> i12 = engine.EvaluateIssues(p12);
            Assert(HasIssue(i12, "ki-acer-e5-553g-win11-block"), "12. ki-acer-e5-553g-win11-block triggered for Acer E5-553G + R7 M440");

            // Scenario 13: HP MediaTek / Ralink Wi-Fi Win11 Block
            PlatformInfo p13 = new PlatformInfo();
            p13.Oem.Manufacturer = "HP";
            p13.PnpDevices.Add(new PnpDeviceItem { VendorId = "1814", PciDeviceId = "3290" }); // Ralink RT3290
            List<DiagnosticRecommendationItem> i13 = engine.EvaluateIssues(p13);
            Assert(HasIssue(i13, "ki-hp-mediatek-ralink-win11"), "13. ki-hp-mediatek-ralink-win11 triggered for HP + Ralink RT3290");

            Console.WriteLine();
        }

        private static bool HasIssue(List<DiagnosticRecommendationItem> items, string issueId)
        {
            if (items == null) return false;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && string.Equals(items[i].Id, issueId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion
    }
}
