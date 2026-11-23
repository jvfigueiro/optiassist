<#
.SYNOPSIS
    Validates an OptiAssist release staging directory or .7z archive for runtime,
    data, and licensing compliance completeness.

.PARAMETER PackagePath
    Path to a staging directory OR a .7z release archive.

.EXAMPLE
    .\tests\Verify-ReleasePackage.ps1 -PackagePath .\staging
    .\tests\Verify-ReleasePackage.ps1 -PackagePath .\OptiAssist-v1.2.S7.7z
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PackagePath)) {
    Write-Error "Package path not found: $PackagePath"
    exit 1
}

$resolvedPath = (Resolve-Path -LiteralPath $PackagePath).Path

$requiredFiles = @(
    # Core executable
    'OptiAssist.exe',

    # Licensing & compliance documentation
    'LICENSE',
    'THIRD-PARTY-NOTICES.md',
    'SOURCE-INFO.md',
    'THIRD-PARTY\MPL-2.0.txt',
    'THIRD-PARTY\Apache-2.0.txt',
    'THIRD-PARTY\BSD-3-Clause.txt',
    'THIRD-PARTY\MIT.txt',

    # Core configuration & databases
    'Config\settings.json',
    'Config\Languages\en-US.json',
    'Config\Languages\pt-BR.json',
    'Config\Data\pci.ids.min',
    'Config\Data\microarch.json',
    'Config\Data\known_issues.json',

    # Modular Add-on DLLs
    'Config\Addons\AppManager\AppManager.dll',
    'Config\Addons\MaintenanceReport\MaintenanceReport.dll',
    'Config\Addons\PCAssist\PCAssist.dll',
    'Config\Addons\StartupManager\StartupManager.dll',
    'Config\Addons\SystemCleaner\SystemCleaner.dll',
    'Config\Addons\Toolbox\Toolbox.dll',

    # PCAssist (Hardware Monitor) runtime dependencies (LibreHardwareMonitor + transitive DLLs)
    'Config\Addons\PCAssist\LibreHardwareMonitorLib.dll',
    'Config\Addons\PCAssist\BlackSharp.Core.dll',
    'Config\Addons\PCAssist\DiskInfoToolkit.dll',
    'Config\Addons\PCAssist\RAMSPDToolkit-NDD.dll',
    'Config\Addons\PCAssist\HidSharp.dll',
    'Config\Addons\PCAssist\System.Buffers.dll',
    'Config\Addons\PCAssist\System.Memory.dll',
    'Config\Addons\PCAssist\System.Numerics.Vectors.dll',
    'Config\Addons\PCAssist\System.Runtime.CompilerServices.Unsafe.dll'
)

$missing = New-Object System.Collections.Generic.List[string]
$invalid = New-Object System.Collections.Generic.List[string]

$item = Get-Item -LiteralPath $resolvedPath
if ($item.PSIsContainer) {
    Write-Host "[Verify-ReleasePackage] Validating staging directory: $resolvedPath" -ForegroundColor Cyan

    foreach ($rel in $requiredFiles) {
        $full = Join-Path $resolvedPath $rel
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
            $missing.Add($rel)
        }
        else {
            $len = (Get-Item -LiteralPath $full).Length
            if ($len -le 0) {
                $invalid.Add("$rel (0 bytes)")
            }
        }
    }
}
elseif ($resolvedPath -match '\.7z$') {
    Write-Host "[Verify-ReleasePackage] Validating .7z archive: $resolvedPath" -ForegroundColor Cyan

    $sevenZip = $null
    $cmd7z = Get-Command 7z.exe -ErrorAction SilentlyContinue
    if ($cmd7z) {
        $sevenZip = $cmd7z.Source
    }
    else {
        $candidates = @(
            (Join-Path $env:ProgramFiles '7-Zip\7z.exe'),
            (Join-Path ${env:ProgramFiles(x86)} '7-Zip\7z.exe'),
            (Join-Path $env:LocalAppData 'Microsoft\WindowsApps\7z.exe')
        )
        foreach ($c in $candidates) {
            if ($c -and (Test-Path -LiteralPath $c -PathType Leaf)) {
                $sevenZip = $c
                break
            }
        }
    }

    $archiveFiles = @{}

    if ($sevenZip) {
        $sltOutput = & $sevenZip l -slt $resolvedPath 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to list .7z archive '$resolvedPath': $($sltOutput -join "`n")"
            exit 1
        }

        $curPath = $null
        $curSize = -1
        foreach ($line in $sltOutput) {
            if ($line -match '^Path = (.+)$') {
                $curPath = $matches[1].Trim().Replace('/', '\')
                $curSize = -1
            }
            elseif ($line -match '^Size = (\d+)$') {
                $curSize = [long]$matches[1]
                if ($curPath) {
                    $archiveFiles[$curPath] = $curSize
                }
            }
        }
    }
    else {
        $tarCmd = Get-Command tar.exe -ErrorAction SilentlyContinue
        if (-not $tarCmd) {
            Write-Error "Neither 7z.exe nor tar.exe was found. Cannot inspect .7z archive."
            exit 1
        }

        $tarOutput = & $tarCmd.Source -tvf $resolvedPath 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to list .7z archive via tar.exe '$resolvedPath': $($tarOutput -join "`n")"
            exit 1
        }

        foreach ($line in $tarOutput) {
            # bsdtar -tvf format: "<perms> <links> <user> <group> <size> <date> <time/year> <path>"
            if ($line -match '^\S+\s+\d+\s+\S+\s+\S+\s+(\d+)\s+\S+\s+\S+\s+\S+\s+(.+)$') {
                $sz = [long]$matches[1]
                $p = $matches[2].Trim().TrimStart('.', '\', '/').Replace('/', '\')
                if ($p) {
                    $archiveFiles[$p] = $sz
                }
            }
        }
    }

    # Support archives rooted directly at OptiAssist.exe OR under a single top folder (e.g. OptiAssist\OptiAssist.exe)
    $rootPrefix = ''
    if (-not $archiveFiles.ContainsKey('OptiAssist.exe')) {
        $exeMatch = $archiveFiles.Keys | Where-Object { $_ -match '^([^\\]+)\\OptiAssist\.exe$' } | Select-Object -First 1
        if ($exeMatch -and ($exeMatch -match '^([^\\]+)\\OptiAssist\.exe$')) {
            $rootPrefix = $matches[1] + '\'
        }
    }

    foreach ($rel in $requiredFiles) {
        $key = $rootPrefix + $rel
        if (-not $archiveFiles.ContainsKey($key)) {
            $missing.Add($rel)
        }
        elseif ($archiveFiles[$key] -le 0) {
            $invalid.Add("$rel (0 bytes in archive)")
        }
    }
}
else {
    Write-Error "Unsupported PackagePath '$resolvedPath'. Provide a directory or a .7z archive."
    exit 1
}

if ($missing.Count -gt 0 -or $invalid.Count -gt 0) {
    Write-Host ""
    Write-Host "[FAIL] Release package validation failed!" -ForegroundColor Red
    foreach ($m in $missing) {
        Write-Host "  [MISSING] $m" -ForegroundColor Red
    }
    foreach ($inv in $invalid) {
        Write-Host "  [INVALID] $inv" -ForegroundColor Red
    }
    exit 1
}

Write-Host "[PASS] All $($requiredFiles.Count) required runtime and compliance files verified." -ForegroundColor Green
exit 0
