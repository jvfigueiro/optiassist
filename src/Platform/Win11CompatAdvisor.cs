using System;
using System.Collections.Generic;
using System.Globalization;

namespace OptiAssist.Platform
{
    public static class Win11CompatAdvisor
    {
        public static Win11CompatReport Evaluate(PlatformInfo info)
        {
            Win11CompatReport report = new Win11CompatReport();
            if (info == null)
            {
                report.Tier = Win11CompatTier.Unsupported;
                report.HardGatesPassed = false;
                report.HardGateFailures.Add("No platform data available to evaluate Windows 11 compatibility.");
                report.Title = "Platform Data Unavailable";
                return report;
            }

            CpuInfo cpu = info.Cpu;
            OsFirmwareInfo os = info.OsFirmware;
            MemoryInfo mem = info.Memory;
            StorageInfo storage = info.Storage;

            // 1. HARD-GATES
            bool hardGatePassed = true;

            // Hard-Gate 1: SSE4.2 and CMPXCHG16B (Mandatory for Windows 11 24H2+)
            if (cpu != null)
            {
                if (!cpu.HasSse42)
                {
                    hardGatePassed = false;
                    report.HardGateFailures.Add("CPU lacks SSE4.2 instruction set (mandatory for Windows 11 24H2+).");
                }
                if (!cpu.HasCmpxchg16b)
                {
                    hardGatePassed = false;
                    report.HardGateFailures.Add("CPU lacks CMPXCHG16B instruction support (mandatory 64-bit atomic operation).");
                }
                if (cpu.IsArm64Emulated)
                {
                    hardGatePassed = false;
                    report.HardGateFailures.Add("Emulated x64 runtime on ARM64 processor is not supported for native system debloating.");
                }
            }

            // Hard-Gate 2: Firmware UEFI requirement
            if (os != null && os.Firmware == SystemFirmwareType.Bios)
            {
                hardGatePassed = false;
                report.HardGateFailures.Add("System is running in Legacy BIOS mode. Windows 11 strictly requires UEFI firmware.");
            }

            // Hard-Gate 3: Bay Trail / Cherry Trail 32-bit UEFI
            if (os != null && os.IsBayTrail32BitUefi)
            {
                hardGatePassed = false;
                report.HardGateFailures.Add("Intel Bay Trail / Cherry Trail 32-bit UEFI detected. 64-bit Windows 11 cannot boot on 32-bit UEFI firmware.");
            }

            report.HardGatesPassed = hardGatePassed;

            // If hard gates failed, classify as Layer C or Unsupported
            if (!hardGatePassed)
            {
                report.Tier = Win11CompatTier.LayerC;
                report.Title = "Windows 11 Compatibility: Legacy Hardware (Windows 10 IoT LTSC Recommended)";
                report.RecommendedOsEdition = "Windows 10 IoT Enterprise LTSC 2021";
                report.LifecycleSummary = "Windows 10 IoT LTSC 2021 is fully supported with security updates until January 13, 2032. Standard Windows 10 EOS is October 14, 2025.";
                return report;
            }

            // 2. LAYER EVALUATION (A, B, C)
            int cpuYear = (cpu != null) ? cpu.ReleaseYear : 0;
            string uarch = (cpu != null && cpu.Microarchitecture != null) ? cpu.Microarchitecture : "";
            double ramGb = (mem != null) ? mem.TotalPhysicalGb : 0.0;
            bool isSsd = (storage != null && string.Equals(storage.SystemDiskMediaType, "SSD", StringComparison.OrdinalIgnoreCase));
            bool hasDiscreteGpu = false;
            bool hasLegacyGmaOrSandyIgpu = false;

            if (info.Gpus != null)
            {
                for (int i = 0; i < info.Gpus.Count; i++)
                {
                    GpuDeviceInfo g = info.Gpus[i];
                    if (g == null) continue;
                    if (g.IsDiscrete)
                    {
                        hasDiscreteGpu = true;
                    }
                    if (g.VendorId == "8086" && (g.DeviceId == "0102" || g.DeviceId == "0112" || g.DeviceId == "0122" || g.DeviceId == "0152"))
                    {
                        hasLegacyGmaOrSandyIgpu = true;
                    }
                }
            }

            // Check if Legacy Chipsets (nForce, SiS, VIA) are present
            bool hasLegacyChipset = false;
            if (info.PnpDevices != null)
            {
                for (int i = 0; i < info.PnpDevices.Count; i++)
                {
                    PnpDeviceItem p = info.PnpDevices[i];
                    if (p == null) continue;
                    if (p.VendorId == "1039" || p.VendorId == "1106") hasLegacyChipset = true;
                    if (p.VendorId == "10de" && (p.PciDeviceId == "0369" || p.PciDeviceId == "03f0" || p.PciDeviceId == "0aa3")) hasLegacyChipset = true;
                }
            }

            // Layer C: Core 1st gen, Core 2, AM2/Phenom/Athlon <= 2010, Vision C/E, legacy chipsets
            if (hasLegacyChipset || cpuYear < 2011 || string.Equals(uarch, "Core 2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uarch, "Nehalem", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uarch, "Bonnell", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uarch, "NetBurst", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uarch, "K10", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uarch, "K8", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uarch, "Bobcat", StringComparison.OrdinalIgnoreCase))
            {
                report.Tier = Win11CompatTier.LayerC;
                report.Title = "Windows 11 Compatibility: Legacy Hardware (Windows 10 IoT LTSC Recommended)";
                report.Warnings.Add("Legacy CPU microarchitecture (" + (string.IsNullOrEmpty(uarch) ? "pre-2011" : uarch) + ") detected.");
                if (hasLegacyChipset) report.Warnings.Add("Legacy nForce / SiS / VIA chipset present; lacks modern WDDM drivers.");
                if (ramGb < 4.0) report.Warnings.Add(string.Format("System has only {0:F1} GB RAM (minimum 4 GB recommended for modern Windows).", ramGb));
                if (!isSsd) report.Warnings.Add("System drive is not an SSD. Mechanical HDDs cause severe indexing stutter on Windows 11.");
                report.RecommendedOsEdition = "Windows 10 IoT Enterprise LTSC 2021";
                report.LifecycleSummary = "Windows 10 IoT LTSC 2021 receives official monthly security updates until January 13, 2032 (vs Oct 2025 for regular Win10).";
                return report;
            }

            // Layer B: Core 2nd gen (Sandy Bridge 2011) / AMD Bulldozer/Piledriver, UEFI required, >= 4 GB RAM, SSD
            if (string.Equals(uarch, "Sandy Bridge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uarch, "Bulldozer", StringComparison.OrdinalIgnoreCase) ||
                cpuYear == 2011)
            {
                report.Tier = Win11CompatTier.LayerB;
                report.Title = "Windows 11 Compatibility: Partial (Bypass Required — See Warnings)";
                if (hasLegacyGmaOrSandyIgpu && !hasDiscreteGpu)
                {
                    report.Warnings.Add("Intel HD Graphics 2000/3000 detected without discrete GPU. Full-screen DWM stuttering occurs at 1080p under Windows 11.");
                }
                if (ramGb < 4.0) report.Warnings.Add(string.Format("System has {0:F1} GB RAM (4 GB minimum recommended).", ramGb));
                if (!isSsd) report.Warnings.Add("Mechanical HDD detected. An SSD upgrade is strongly recommended for Windows 11.");
                report.RecommendedOsEdition = "Windows 10 IoT Enterprise LTSC 2021 or Windows 11 (with bypass)";
                report.LifecycleSummary = "Windows 10 IoT LTSC 2021 supported until 2032. If deploying Windows 11, apply OptiAssist Debloater tweaks to reduce DWM load.";
                return report;
            }

            // Layer A: Core 3rd gen (Ivy Bridge 2012)+ / AMD Zen, UEFI, >= 4 GB RAM, SSD, GPU >= 2012
            report.Tier = Win11CompatTier.LayerA;
            report.Title = "Windows 11 Compatibility: Ready (Windows 11 24H2 / LTSC 2024)";
            if (ramGb < 8.0) report.Warnings.Add(string.Format("System has {0:F1} GB RAM. 8 GB or more is recommended for optimal multitasking.", ramGb));
            if (!isSsd) report.Warnings.Add("Mechanical HDD detected. Upgrading to an SSD provides 5x faster boot and responsiveness.");
            report.RecommendedOsEdition = "Windows 11 23H2 / 24H2 or Windows 11 IoT Enterprise LTSC 2024";
            report.LifecycleSummary = "Windows 11 IoT Enterprise LTSC 2024 is supported through 2034. Standard Windows 11 supported under current annual channel.";
            return report;
        }
    }
}
