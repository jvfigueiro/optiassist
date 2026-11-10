using System;
using System.Runtime.InteropServices;

namespace OptiAssist.Addons.PCAssist.Core
{
    internal static class NativeMethods
    {
        // 1. Memory Status
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        // 2. Battery Status
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;        // 0: Offline, 1: Online, 255: Unknown
            public byte BatteryFlag;         // 1: High, 2: Low, 4: Critical, 8: Charging, 128: No battery, 255: Unknown
            public byte BatteryLifePercent;  // 0-100%, 255: Unknown
            public byte SystemStatusFlag;    // Battery saver status
            public int BatteryLifeTime;      // Seconds remaining, -1 if unknown or on AC
            public int BatteryFullLifeTime;  // Full life time, -1 if unknown
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        // 3. Display Settings & Devices
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public short dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        // 4. Processor Features (Instruction Sets)
        [DllImport("kernel32.dll")]
        public static extern bool IsProcessorFeaturePresent(uint processorFeature);

        public const uint PF_FLOATING_POINT_PRECISION_ERRATA = 0;
        public const uint PF_FLOATING_POINT_EMULATION = 1;
        public const uint PF_COMPARE_EXCHANGE_DOUBLE = 2;
        public const uint PF_MMX_INSTRUCTIONS_AVAILABLE = 3;
        public const uint PF_XMMI_INSTRUCTIONS_AVAILABLE = 6;     // SSE
        public const uint PF_3DNOW_INSTRUCTIONS_AVAILABLE = 7;
        public const uint PF_RDTSC_INSTRUCTION_AVAILABLE = 9;
        public const uint PF_XMMI64_INSTRUCTIONS_AVAILABLE = 10;   // SSE2
        public const uint PF_SSE3_INSTRUCTIONS_AVAILABLE = 13;
        public const uint PF_COMPARE_EXCHANGE128 = 14;
        public const uint PF_COMPARE64_EXCHANGE128 = 15;
        public const uint PF_NX_ENABLED = 12;
        public const uint PF_SSSE3_INSTRUCTIONS_AVAILABLE = 36;
        public const uint PF_SSE4_1_INSTRUCTIONS_AVAILABLE = 37;
        public const uint PF_SSE4_2_INSTRUCTIONS_AVAILABLE = 38;
        public const uint PF_AVX_INSTRUCTIONS_AVAILABLE = 40;
        public const uint PF_AVX2_INSTRUCTIONS_AVAILABLE = 41;
        public const uint PF_AVX512F_INSTRUCTIONS_AVAILABLE = 49;
        public const uint PF_FASTFAIL_AVAILABLE = 23;
        public const uint PF_ARM_V8_CRYPTO_INSTRUCTIONS_AVAILABLE = 34;

        // 5. Storage Property Query (Direct NVMe / SATA / USB bus detection)
        public const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
        public const uint GENERIC_READ = 0x80000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint OPEN_EXISTING = 3;

        [StructLayout(LayoutKind.Sequential)]
        public struct STORAGE_PROPERTY_QUERY
        {
            public int PropertyId;
            public int QueryType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] AdditionalParameters;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STORAGE_DEVICE_DESCRIPTOR_HEADER
        {
            public int Version;
            public int Size;
            public byte DeviceType;
            public byte DeviceTypeModifier;
            [MarshalAs(UnmanagedType.I1)]
            public bool RemovableMedia;
            [MarshalAs(UnmanagedType.I1)]
            public bool CommandQueueing;
            public int VendorIdOffset;
            public int ProductIdOffset;
            public int ProductRevisionOffset;
            public int SerialNumberOffset;
            public int BusType; // 17 = NVMe, 11 = SATA, 7 = USB, 8 = RAID, 1 = SCSI
            public int RawPropertiesLength;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            ref STORAGE_PROPERTY_QUERY lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);
    }
}
