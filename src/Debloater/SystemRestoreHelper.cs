using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace OptiAssist.Debloater
{
    /// <summary>
    /// Creates an optional Windows System Restore Point prior to applying a Debloater batch.
    /// Strict .NET Framework 4.8 / C# 5 compatible.
    /// </summary>
    public static class SystemRestoreHelper
    {
        private const int BEGIN_SYSTEM_CHANGE = 100;
        private const int END_SYSTEM_CHANGE = 101;
        private const int MODIFY_SETTINGS = 12;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RESTOREPOINTINFOW
        {
            public int dwEventType;
            public int dwRestorePtType;
            public long llSequenceNumber;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STATEMGRSTATUS
        {
            public int nStatus;
            public long llSequenceNumber;
        }

        [DllImport("srclient.dll", CharSet = CharSet.Unicode)]
        private static extern bool SRSetRestorePointW(ref RESTOREPOINTINFOW pRestorePtSpec, out STATEMGRSTATUS pSMgrStatus);

        public static bool CreateRestorePoint(string description, Action<string> log)
        {
            string desc = string.IsNullOrWhiteSpace(description)
                ? "OptiAssist Windows Debloater 2.0"
                : description.Trim();

            if (log != null) log("[RESTORE POINT] Creating Windows System Restore Point: '" + desc + "'...");

            // 1. Primary native call via srclient.dll (fast, no PowerShell overhead)
            try
            {
                RESTOREPOINTINFOW info = new RESTOREPOINTINFOW();
                info.dwEventType = BEGIN_SYSTEM_CHANGE;
                info.dwRestorePtType = MODIFY_SETTINGS;
                info.llSequenceNumber = 0;
                info.szDescription = desc.Length > 250 ? desc.Substring(0, 250) : desc;

                STATEMGRSTATUS status;
                if (SRSetRestorePointW(ref info, out status) && status.nStatus == 0)
                {
                    RESTOREPOINTINFOW endInfo = new RESTOREPOINTINFOW();
                    endInfo.dwEventType = END_SYSTEM_CHANGE;
                    endInfo.dwRestorePtType = MODIFY_SETTINGS;
                    endInfo.llSequenceNumber = status.llSequenceNumber;
                    endInfo.szDescription = "";
                    STATEMGRSTATUS endStatus;
                    SRSetRestorePointW(ref endInfo, out endStatus);

                    if (log != null) log(string.Format("[RESTORE POINT] Created successfully (Seq #{0}).", status.llSequenceNumber));
                    return true;
                }
                else
                {
                    if (log != null) log(string.Format("[RESTORE POINT:INFO] Native srclient returned status {0}; trying PowerShell fallback...", status.nStatus));
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("[RESTORE POINT:INFO] Native srclient unavailable (" + ex.Message + "); trying PowerShell fallback...");
            }

            // 2. Fallback via PowerShell Checkpoint-Computer
            try
            {
                string safeDesc = desc.Replace("'", "''");
                string ps = string.Format(
                    "Checkpoint-Computer -Description '{0}' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop",
                    safeDesc);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + ps.Replace("\"", "\\\"") + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                using (Process p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        p.WaitForExit(60000);
                        if (p.HasExited && p.ExitCode == 0)
                        {
                            if (log != null) log("[RESTORE POINT] Created successfully via Checkpoint-Computer.");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("[RESTORE POINT:WARN] Fallback failed: " + ex.Message);
            }

            if (log != null) log("[RESTORE POINT:WARN] Could not create Restore Point (System Protection may be disabled or 24h cooldown active). Proceeding with JSON snapshot backup.");
            return false;
        }
    }
}
