using System;
using System.Collections.Generic;

namespace OptiAssist.Platform
{
    public enum CpuArchitecture
    {
        Unknown = 0,
        X86 = 1,
        X64 = 2,
        Arm32 = 3,
        Arm64 = 4
    }

    public enum SystemFirmwareType
    {
        Unknown = 0,
        Bios = 1,
        Uefi = 2,
        Max = 3
    }

    public enum ChassisFormFactor
    {
        Unknown = 0,
        Desktop = 1,
        Laptop = 2,
        Ultraportable = 3,
        Server = 4,
        VirtualMachine = 5
    }

    public enum DebloaterProfileId
    {
        Generic = 0,
        Gaming = 1,
        Workstation = 2,
        Hybrid = 3,
        Conventional = 4,
        Laptop = 5,
        LaptopGaming = 6,
        Ultraportable = 7
    }

    public enum RecommendationSeverity
    {
        Info = 0,
        Warning = 1,
        Critical = 2
    }

    public enum Win11CompatTier
    {
        Unsupported = 0,
        LayerC = 1, // Legacy (Core 1st gen, Core 2, AM2/Phenom, Legacy BIOS)
        LayerB = 2, // Sandy Bridge / Bulldozer, UEFI required, SSD
        LayerA = 3  // Ivy Bridge+ / Zen+, UEFI, SSD, modern GPU
    }

    public class CpuInfo
    {
        public string VendorIdentifier { get; set; }
        public string ProcessorNameString { get; set; }
        public string RawIdentifier { get; set; }
        public int Family { get; set; }
        public int Model { get; set; }
        public int Stepping { get; set; }
        public int PhysicalCores { get; set; }
        public int LogicalProcessors { get; set; }
        public bool HasHybridCores { get; set; }
        public int PerformanceCores { get; set; }
        public int EfficientCores { get; set; }
        public bool HasSse42 { get; set; }
        public bool HasCmpxchg16b { get; set; }
        public string Microarchitecture { get; set; }
        public int ReleaseYear { get; set; }
        public string MarketingGen { get; set; }
        public string Suffix { get; set; }
        public bool IsArm64Emulated { get; set; }
        public CpuArchitecture NativeArchitecture { get; set; }

        public CpuInfo()
        {
            VendorIdentifier = "";
            ProcessorNameString = "";
            RawIdentifier = "";
            Microarchitecture = "Unknown";
            MarketingGen = "";
            Suffix = "";
            NativeArchitecture = CpuArchitecture.X64;
        }
    }

    public class GpuDeviceInfo
    {
        public string PnpDeviceId { get; set; }
        public string VendorId { get; set; }
        public string DeviceId { get; set; }
        public string SubsystemId { get; set; }
        public string Name { get; set; }
        public string VendorName { get; set; }
        public string ArchitectureFamily { get; set; }
        public string MarketingSeries { get; set; }
        public bool IsIntegrated { get; set; }
        public bool IsDiscrete { get; set; }
        public string DriverVersion { get; set; }
        public string DriverDate { get; set; }

        public GpuDeviceInfo()
        {
            PnpDeviceId = "";
            VendorId = "";
            DeviceId = "";
            SubsystemId = "";
            Name = "";
            VendorName = "";
            ArchitectureFamily = "";
            MarketingSeries = "";
            DriverVersion = "";
            DriverDate = "";
        }
    }

    public class MemoryInfo
    {
        public ulong TotalPhysicalBytes { get; set; }
        public double TotalPhysicalGb { get; set; }
        public string SmbiosMemoryType { get; set; } // DDR2, DDR3, DDR4, DDR5, LPDDR4...
        public bool IsEcc { get; set; }
        public int SlotsUsed { get; set; }
        public int TotalSlots { get; set; }

        public MemoryInfo()
        {
            SmbiosMemoryType = "Unknown";
        }
    }

    public class StorageInfo
    {
        public string SystemDriveLetter { get; set; }
        public string SystemDiskMediaType { get; set; } // SSD, HDD, SCM, Unspecified
        public string SystemDiskBusType { get; set; }   // NVMe, SATA, USB, MMC, SD...
        public bool IsSystemEmmcOrSd { get; set; }
        public int TotalNvmeDrivesCount { get; set; }
        public int TotalPhysicalDisksCount { get; set; }
        public double SystemDriveTotalGb { get; set; }
        public double SystemDriveFreeGb { get; set; }

        public StorageInfo()
        {
            SystemDriveLetter = "C:";
            SystemDiskMediaType = "Unknown";
            SystemDiskBusType = "Unknown";
        }
    }

    public class OsFirmwareInfo
    {
        public string EditionId { get; set; }
        public string DisplayVersion { get; set; }
        public string ReleaseId { get; set; }
        public int CurrentBuild { get; set; }
        public int Ubr { get; set; }
        public string ProductName { get; set; }
        public SystemFirmwareType Firmware { get; set; }
        public bool IsBayTrail32BitUefi { get; set; }
        public bool IsTamperProtectionActive { get; set; }
        public string ActivePowerSchemeGuid { get; set; }

        public OsFirmwareInfo()
        {
            EditionId = "";
            DisplayVersion = "";
            ReleaseId = "";
            ProductName = "";
            Firmware = SystemFirmwareType.Unknown;
            ActivePowerSchemeGuid = "";
        }
    }

    public class PnpDeviceItem
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public string ClassGuid { get; set; }
        public string Service { get; set; }
        public string VendorId { get; set; }
        public string PciDeviceId { get; set; }

        public PnpDeviceItem()
        {
            DeviceId = "";
            Name = "";
            ClassGuid = "";
            Service = "";
            VendorId = "";
            PciDeviceId = "";
        }
    }

    public class OemInfo
    {
        public string Manufacturer { get; set; }
        public string ProductName { get; set; }
        public string Family { get; set; }
        public string MotherboardManufacturer { get; set; }
        public string MotherboardProduct { get; set; }
        public string BiosReleaseDate { get; set; }
        public int BiosReleaseYear { get; set; }

        public OemInfo()
        {
            Manufacturer = "";
            ProductName = "";
            Family = "";
            MotherboardManufacturer = "";
            MotherboardProduct = "";
            BiosReleaseDate = "";
        }
    }

    public class PlatformInfo
    {
        public CpuInfo Cpu { get; set; }
        public List<GpuDeviceInfo> Gpus { get; set; }
        public MemoryInfo Memory { get; set; }
        public StorageInfo Storage { get; set; }
        public OsFirmwareInfo OsFirmware { get; set; }
        public OemInfo Oem { get; set; }
        public ChassisFormFactor FormFactor { get; set; }
        public bool HasBattery { get; set; }
        public bool IsVirtualMachine { get; set; }
        public string HypervisorName { get; set; }
        public List<PnpDeviceItem> PnpDevices { get; set; }
        public DateTime CollectedAtUtc { get; set; }

        public PlatformInfo()
        {
            Cpu = new CpuInfo();
            Gpus = new List<GpuDeviceInfo>();
            Memory = new MemoryInfo();
            Storage = new StorageInfo();
            OsFirmware = new OsFirmwareInfo();
            Oem = new OemInfo();
            FormFactor = ChassisFormFactor.Unknown;
            PnpDevices = new List<PnpDeviceItem>();
            CollectedAtUtc = DateTime.UtcNow;
            HypervisorName = "";
        }
    }

    public class ProfileRecommendation
    {
        public DebloaterProfileId ProfileId { get; set; }
        public string ProfileCode { get; set; } // "gaming", "workstation", "hybrid", "conventional", "laptop", "laptopGaming", "ultraportable", "generic"
        public string ConfidenceLevel { get; set; } // "High", "Medium", "Low"
        public string Title { get; set; }
        public List<string> Reasons { get; set; }
        public bool IsUserOverridden { get; set; }

        public ProfileRecommendation()
        {
            ProfileId = DebloaterProfileId.Generic;
            ProfileCode = "generic";
            ConfidenceLevel = "Low";
            Title = "";
            Reasons = new List<string>();
        }
    }

    public class Win11CompatReport
    {
        public Win11CompatTier Tier { get; set; }
        public bool HardGatesPassed { get; set; }
        public List<string> HardGateFailures { get; set; }
        public List<string> Warnings { get; set; }
        public string RecommendedOsEdition { get; set; }
        public string LifecycleSummary { get; set; }
        public string Title { get; set; }

        public Win11CompatReport()
        {
            Tier = Win11CompatTier.Unsupported;
            HardGateFailures = new List<string>();
            Warnings = new List<string>();
            RecommendedOsEdition = "";
            LifecycleSummary = "";
            Title = "";
        }
    }

    public class DiagnosticRecommendationItem
    {
        public string Id { get; set; }
        public string Category { get; set; } // "Profile", "Win11", "HardwareQuirk"
        public RecommendationSeverity Severity { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ActionTag { get; set; } // "open_debloater", "fix_transparency", "fix_ulps", "inspect_drivers", etc.
        public string ActionLabel { get; set; }
        public string TargetProfileId { get; set; }
        public string TargetTweakId { get; set; }
        public string TargetRegistryPath { get; set; }
        public string TargetValueName { get; set; }
        public string TargetValueKind { get; set; }
        public string TargetValueData { get; set; }
        public string Notes { get; set; }

        public DiagnosticRecommendationItem()
        {
            Id = "";
            Category = "HardwareQuirk";
            Severity = RecommendationSeverity.Info;
            Title = "";
            Description = "";
            ActionTag = "";
            ActionLabel = "";
            TargetProfileId = "";
            TargetTweakId = "";
            TargetRegistryPath = "";
            TargetValueName = "";
            TargetValueKind = "DWord";
            TargetValueData = "";
            Notes = "";
        }
    }

    public class PlatformDiagnosticSnapshot
    {
        public PlatformInfo Platform { get; set; }
        public ProfileRecommendation ProfileRecommendation { get; set; }
        public Win11CompatReport Win11Compat { get; set; }
        public List<DiagnosticRecommendationItem> Recommendations { get; set; }
        public DateTime GeneratedAtUtc { get; set; }

        public PlatformDiagnosticSnapshot()
        {
            Platform = new PlatformInfo();
            ProfileRecommendation = new ProfileRecommendation();
            Win11Compat = new Win11CompatReport();
            Recommendations = new List<DiagnosticRecommendationItem>();
            GeneratedAtUtc = DateTime.UtcNow;
        }
    }
}
