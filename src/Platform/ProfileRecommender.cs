using System;
using System.Collections.Generic;
using System.Globalization;

namespace OptiAssist.Platform
{
    public static class ProfileRecommender
    {
        public static ProfileRecommendation RecommendProfile(PlatformInfo info, string userOverrideProfile = null)
        {
            ProfileRecommendation rec = new ProfileRecommendation();
            if (info == null)
            {
                rec.ProfileId = DebloaterProfileId.Generic;
                rec.ProfileCode = "generic";
                rec.ConfidenceLevel = "Low";
                rec.Title = "Generic / Virtual Machine";
                rec.Reasons.Add("No platform hardware data could be collected.");
                return rec;
            }

            // Check VM first
            if (info.IsVirtualMachine)
            {
                rec.ProfileId = DebloaterProfileId.Generic;
                rec.ProfileCode = "generic";
                rec.ConfidenceLevel = "High";
                rec.Title = "Generic / Virtual Machine";
                rec.Reasons.Add("Virtual Machine environment detected (" + (string.IsNullOrEmpty(info.HypervisorName) ? "Hypervisor" : info.HypervisorName) + "). Default system services and standard scheduling Boost preserved.");
                ApplyUserOverrideIfNeeded(rec, userOverrideProfile);
                return rec;
            }

            bool isMobile = (info.FormFactor == ChassisFormFactor.Laptop || info.FormFactor == ChassisFormFactor.Ultraportable || info.HasBattery);
            bool hasDiscreteGpu = false;
            bool isWorkstationGpu = false;
            string gpuSummary = "";

            if (info.Gpus != null && info.Gpus.Count > 0)
            {
                for (int i = 0; i < info.Gpus.Count; i++)
                {
                    GpuDeviceInfo g = info.Gpus[i];
                    if (g == null) continue;
                    if (g.IsDiscrete) hasDiscreteGpu = true;
                    if (g.ArchitectureFamily != null && g.ArchitectureFamily.IndexOf("Workstation", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isWorkstationGpu = true;
                    }
                    if (!string.IsNullOrEmpty(g.Name) && string.IsNullOrEmpty(gpuSummary))
                    {
                        gpuSummary = g.Name;
                    }
                }
            }

            string cpuSuffix = (info.Cpu != null && info.Cpu.Suffix != null) ? info.Cpu.Suffix.ToUpperInvariant() : "";
            string cpuName = (info.Cpu != null) ? (info.Cpu.ProcessorNameString ?? "") : "";
            string uarch = (info.Cpu != null) ? (info.Cpu.Microarchitecture ?? "") : "";
            int cores = (info.Cpu != null) ? info.Cpu.PhysicalCores : Environment.ProcessorCount;
            double ramGb = (info.Memory != null) ? info.Memory.TotalPhysicalGb : 8.0;
            bool isSsd = (info.Storage != null && string.Equals(info.Storage.SystemDiskMediaType, "SSD", StringComparison.OrdinalIgnoreCase));
            bool isEmmc = (info.Storage != null && info.Storage.IsSystemEmmcOrSd);
            bool hasHybridCores = (info.Cpu != null && info.Cpu.HasHybridCores);

            // ==========================================
            // 1. MOBILE PLATFORMS (Laptop / Gaming / Ultra)
            // ==========================================
            if (isMobile)
            {
                // A) Gaming Laptop
                if (hasDiscreteGpu || cpuSuffix == "H" || cpuSuffix == "HK" || cpuSuffix == "HX")
                {
                    rec.ProfileId = DebloaterProfileId.LaptopGaming;
                    rec.ProfileCode = "laptopGaming";
                    rec.ConfidenceLevel = "High";
                    rec.Title = "Gaming Laptop";
                    rec.Reasons.Add("Mobile chassis with high-performance hardware detected.");
                    if (hasDiscreteGpu) rec.Reasons.Add("Discrete GPU present: " + (string.IsNullOrEmpty(gpuSummary) ? "Dedicated Graphics" : gpuSummary) + ".");
                    if (!string.IsNullOrEmpty(cpuSuffix)) rec.Reasons.Add("High-performance mobile CPU series (" + cpuSuffix + ") identified.");
                    rec.Reasons.Add("Optimizes background telemetry and Cortana while preserving Wi-Fi, battery management, and thermal daemon services.");
                    ApplyUserOverrideIfNeeded(rec, userOverrideProfile);
                    return rec;
                }

                // B) Ultraportable / Thin & Light / Netbook
                if (cpuSuffix == "U" || cpuSuffix == "Y" || cpuSuffix == "N" || cpuSuffix == "P" || isEmmc ||
                    string.Equals(uarch, "Silvermont", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(uarch, "Gracemont", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(uarch, "Bobcat", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(uarch, "Jaguar", StringComparison.OrdinalIgnoreCase))
                {
                    rec.ProfileId = DebloaterProfileId.Ultraportable;
                    rec.ProfileCode = "ultraportable";
                    rec.ConfidenceLevel = "High";
                    rec.Title = "Ultraportable Laptop";
                    rec.Reasons.Add("Ultra-low-power or compact mobile hardware detected (" + (string.IsNullOrEmpty(cpuSuffix) ? uarch : "Suffix " + cpuSuffix) + ").");
                    if (isEmmc) rec.Reasons.Add("System storage uses flash eMMC/SD media (I/O optimization critical).");
                    rec.Reasons.Add("Applies aggressive energy saver tweaks, background process pruning, and disables non-essential indexing to maximize battery life.");
                    ApplyUserOverrideIfNeeded(rec, userOverrideProfile);
                    return rec;
                }

                // C) Standard Conventional Laptop
                rec.ProfileId = DebloaterProfileId.Laptop;
                rec.ProfileCode = "laptop";
                rec.ConfidenceLevel = "Medium";
                rec.Title = "Laptop (General Productivity)";
                rec.Reasons.Add("Mobile portable PC detected with balanced hardware configuration.");
                rec.Reasons.Add("Keeps standard power plans and mobile connectivity services intact while eliminating diagnostic telemetry.");
                ApplyUserOverrideIfNeeded(rec, userOverrideProfile);
                return rec;
            }

            // ==========================================
            // 2. DESKTOP PLATFORMS
            // ==========================================

            // A) Workstation
            bool isXeonOrThreadripper = (cpuName.IndexOf("Xeon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         cpuName.IndexOf("Threadripper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         cpuName.IndexOf("EPYC", StringComparison.OrdinalIgnoreCase) >= 0);

            if (isXeonOrThreadripper || isWorkstationGpu || (info.Memory != null && info.Memory.IsEcc) || (cores >= 16 && ramGb >= 64.0))
            {
                rec.ProfileId = DebloaterProfileId.Workstation;
                rec.ProfileCode = "workstation";
                rec.ConfidenceLevel = "High";
                rec.Title = "Professional Workstation";
                if (isXeonOrThreadripper) rec.Reasons.Add("Enterprise-grade CPU detected: " + cpuName + ".");
                if (isWorkstationGpu) rec.Reasons.Add("Workstation GPU (Quadro/RTX A/Radeon Pro) detected.");
                if (info.Memory != null && info.Memory.IsEcc) rec.Reasons.Add("ECC Memory error correction enabled.");
                if (cores >= 16) rec.Reasons.Add(string.Format("High parallel core count ({0} cores) suitable for rendering/compilation.", cores));
                rec.Reasons.Add("Configures scheduler priority for Background Services (Win32PrioritySeparation 0x18) and protects spooler/developer daemons.");
                ApplyUserOverrideIfNeeded(rec, userOverrideProfile);
                return rec;
            }

            // B) Gaming PC
            bool isX3D = (cpuSuffix == "X3D" || cpuName.IndexOf("X3D", StringComparison.OrdinalIgnoreCase) >= 0);
            if (hasDiscreteGpu || isX3D)
            {
                // Check if Hybrid is a better fit (Hybrid P/E cores + Development / Multitasking)
                if (hasHybridCores && ramGb >= 32.0)
                {
                    rec.ProfileId = DebloaterProfileId.Hybrid;
                    rec.ProfileCode = "hybrid";
                    rec.ConfidenceLevel = "Medium";
                    rec.Title = "Hybrid Power Desktop";
                    rec.Reasons.Add("Intel/AMD Hybrid P/E core architecture detected (" + info.Cpu.PerformanceCores + "P + " + info.Cpu.EfficientCores + "E cores).");
                    rec.Reasons.Add(string.Format("High RAM capacity ({0:F0} GB) with discrete graphics.", ramGb));
                    rec.Reasons.Add("Balanced gaming boost and background compiling responsiveness.");
                    ApplyUserOverrideIfNeeded(rec, userOverrideProfile);
                    return rec;
                }

                rec.ProfileId = DebloaterProfileId.Gaming;
                rec.ProfileCode = "gaming";
                rec.ConfidenceLevel = "High";
                rec.Title = "Gaming Desktop";
                if (isX3D) rec.Reasons.Add("AMD 3D V-Cache enthusiast gaming CPU detected (" + cpuName + ").");
                if (hasDiscreteGpu) rec.Reasons.Add("Discrete gaming GPU active: " + (string.IsNullOrEmpty(gpuSummary) ? "Dedicated Graphics" : gpuSummary) + ".");
                rec.Reasons.Add("Enforces Best Performance power plan, keeps foreground boost (0x26), and strips non-essential telemetry and background bloat.");
                ApplyUserOverrideIfNeeded(rec, userOverrideProfile);
                return rec;
            }

            // C) Conventional Desktop
            rec.ProfileId = DebloaterProfileId.Conventional;
            rec.ProfileCode = "conventional";
            rec.ConfidenceLevel = "Medium";
            rec.Title = "Conventional Desktop";
            rec.Reasons.Add("Standard desktop computer with integrated graphics or general-purpose hardware.");
            rec.Reasons.Add(string.Format("CPU: {0} ({1} cores), RAM: {2:F0} GB.", (string.IsNullOrEmpty(cpuName) ? "Standard CPU" : cpuName), cores, ramGb));
            rec.Reasons.Add("Maintains Windows Search, Print Spooler, Themes, and default foreground priority intact for home and office safety.");
            ApplyUserOverrideIfNeeded(rec, userOverrideProfile);
            return rec;
        }

        private static void ApplyUserOverrideIfNeeded(ProfileRecommendation rec, string userOverrideProfile)
        {
            if (string.IsNullOrWhiteSpace(userOverrideProfile)) return;
            string ov = userOverrideProfile.Trim().ToLowerInvariant();

            DebloaterProfileId targetId;
            if (ov == "gaming") targetId = DebloaterProfileId.Gaming;
            else if (ov == "workstation") targetId = DebloaterProfileId.Workstation;
            else if (ov == "hybrid") targetId = DebloaterProfileId.Hybrid;
            else if (ov == "conventional") targetId = DebloaterProfileId.Conventional;
            else if (ov == "laptop") targetId = DebloaterProfileId.Laptop;
            else if (ov == "laptopgaming") targetId = DebloaterProfileId.LaptopGaming;
            else if (ov == "ultraportable") targetId = DebloaterProfileId.Ultraportable;
            else if (ov == "generic") targetId = DebloaterProfileId.Generic;
            else return;

            if (rec.ProfileId != targetId)
            {
                rec.Reasons.Insert(0, string.Format("User manually selected '{0}' profile override (Hardware auto-detected: '{1}').", ov, rec.ProfileCode));
                rec.ProfileId = targetId;
                rec.ProfileCode = ov;
                rec.IsUserOverridden = true;
            }
        }
    }
}
