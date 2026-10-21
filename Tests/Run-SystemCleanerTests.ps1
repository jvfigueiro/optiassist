# ==============================================================================
# OptiAssist - SystemCleaner v2.0 Automated Unit Tests & Dry-Run Simulation
# ==============================================================================
# Loads OptiAssist.exe and Config\Addons\SystemCleaner\SystemCleaner.dll directly
# into the .NET runtime and runs:
#   1. PathSanitizer Security & Scope Boundary Unit Tests
#   2. Sandboxed Deep Scan Dry-Run (Scan), Clean(), & Locked File (EBUSY) Tests
#   3. Chromium SQLite Database VACUUM Defragmentation Unit Test
#   4. Live Non-Destructive Dry-Run Scan Across All 23 BaseCleaner Modules
# ==============================================================================

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ExePath  = Join-Path $RepoRoot "OptiAssist.exe"
$DllPath  = Join-Path $RepoRoot "Config\Addons\SystemCleaner\SystemCleaner.dll"

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "  OptiAssist SystemCleaner v2.0 - Unit Tests & Simulated Dry-Run Scan Report  " -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Cyan

if (-not (Test-Path $ExePath) -or -not (Test-Path $DllPath)) {
    throw "Required binaries not found. Please build OptiAssist.csproj first."
}

[System.Reflection.Assembly]::LoadFrom($ExePath) | Out-Null
[System.Reflection.Assembly]::LoadFrom($DllPath) | Out-Null

$passed = 0
$failed = 0

function Assert-True([bool]$condition, [string]$testName) {
    if ($condition) {
        Write-Host ("  [PASS] " + $testName) -ForegroundColor Green
        $script:passed++
    } else {
        Write-Host ("  [FAIL] " + $testName) -ForegroundColor Red
        $script:failed++
    }
}

# ------------------------------------------------------------------------------
# 1. UNIT TESTS: PathSanitizer Security & Scope Boundaries
# ------------------------------------------------------------------------------
Write-Host "`n[1/4] Running PathSanitizer Security & Scope Boundary Tests..." -ForegroundColor Yellow

Assert-True (-not [OptiAssist.Addons.SystemCleaner.PathSanitizer]::IsSafeToDelete("C:\")) `
    "Rejects root drive deletion (C:\)"

Assert-True (-not [OptiAssist.Addons.SystemCleaner.PathSanitizer]::IsSafeToDelete("C:\Windows")) `
    "Rejects Windows OS root directory (C:\Windows)"

Assert-True (-not [OptiAssist.Addons.SystemCleaner.PathSanitizer]::IsSafeToDelete("C:\Windows\System32\kernel32.dll")) `
    "Rejects critical system binaries (C:\Windows\System32\kernel32.dll)"

Assert-True (-not [OptiAssist.Addons.SystemCleaner.PathSanitizer]::IsSafeToDelete("C:\Program Files\Common Files")) `
    "Rejects Program Files directory (C:\Program Files\Common Files)"

Assert-True ([OptiAssist.Addons.SystemCleaner.PathSanitizer]::IsSafeToDelete("C:\Windows\Temp\dummy_installer.tmp")) `
    "Allows whitelisted Windows Temp path (C:\Windows\Temp\dummy_installer.tmp)"

Assert-True ([OptiAssist.Addons.SystemCleaner.PathSanitizer]::IsSafeToDelete("C:\Windows\Prefetch\CALC.EXE-12345.pf")) `
    "Allows whitelisted Windows Prefetch path (C:\Windows\Prefetch\CALC.EXE-12345.pf)"

Assert-True ([OptiAssist.Addons.SystemCleaner.PathSanitizer]::IsSafeToDelete((Join-Path $env:TEMP "opti_test.tmp"))) `
    "Allows user %TEMP% files"

# ------------------------------------------------------------------------------
# 2. UNIT TESTS: Sandboxed Deep Scan, Clean(), & EBUSY Locked File Handling
# ------------------------------------------------------------------------------
Write-Host "`n[2/4] Running Sandboxed Deep Scan, Clean() & Locked File (EBUSY) Tests..." -ForegroundColor Yellow

$sandboxRoot = Join-Path $env:TEMP ("OptiAssist_Sandbox_" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Path $sandboxRoot -Force | Out-Null

try {
    # Create simulated developer project structure
    $projectDir  = Join-Path $sandboxRoot "MyWebApp"
    $nodeModules = Join-Path $projectDir "node_modules\lodash"
    $pycacheDir  = Join-Path $projectDir "backend\__pycache__"
    $angularDir  = Join-Path $projectDir ".angular\cache"
    $venvDir     = Join-Path $projectDir ".venv\Lib"

    New-Item -ItemType Directory -Path $nodeModules -Force | Out-Null
    New-Item -ItemType Directory -Path $pycacheDir  -Force | Out-Null
    New-Item -ItemType Directory -Path $angularDir  -Force | Out-Null
    New-Item -ItemType Directory -Path $venvDir     -Force | Out-Null

    # Legitimate source code file that MUST NOT be deleted
    $sourceFile = Join-Path $projectDir "index.ts"
    [System.IO.File]::WriteAllBytes($sourceFile, (New-Object byte[] 2048))

    # Orphaned OS & Vim swap files
    $dsStoreFile = Join-Path $projectDir ".DS_Store"
    $thumbsFile  = Join-Path $projectDir "Thumbs.db"
    $vimSwapFile = Join-Path $projectDir ".index.ts.swp"
    [System.IO.File]::WriteAllBytes($dsStoreFile, (New-Object byte[] 4096))
    [System.IO.File]::WriteAllBytes($thumbsFile,  (New-Object byte[] 8192))
    [System.IO.File]::WriteAllBytes($vimSwapFile, (New-Object byte[] 16384))

    # Populate developer cache folders
    [System.IO.File]::WriteAllBytes((Join-Path $pycacheDir "app.cpython-311.pyc"), (New-Object byte[] 32768))
    [System.IO.File]::WriteAllBytes((Join-Path $angularDir "bundle.pack"),         (New-Object byte[] 65536))
    [System.IO.File]::WriteAllBytes((Join-Path $nodeModules "index.js"),           (New-Object byte[] 131072))
    [System.IO.File]::WriteAllBytes((Join-Path $venvDir "site.py"),                (New-Object byte[] 65536))

    $ctx = New-Object OptiAssist.Addons.SystemCleaner.CleanerContext
    $ctx.DeepScanRootPath = $sandboxRoot
    $ctx.DeepScanMaxDepth = 5

    $osJunkCleaner   = New-Object OptiAssist.Addons.SystemCleaner.DeepScanOrphanFilesCleaner
    $nodeCleaner     = New-Object OptiAssist.Addons.SystemCleaner.DeepScanDirectoryCleaner -ArgumentList @(
        "deep_node_modules", "Node and Angular Cache", "Test Node and Angular", "[N]", "", [string[]]@("node_modules", ".angular"))
    $pythonCleaner   = New-Object OptiAssist.Addons.SystemCleaner.DeepScanDirectoryCleaner -ArgumentList @(
        "deep_python_cache", "Python Cache and Venv", "Test Python and Venv", "[P]", "", [string[]]@("__pycache__", "venv", ".venv"))

    # A. Dry-run Scan() test
    $scanOs     = $osJunkCleaner.Scan($ctx)
    $scanNode   = $nodeCleaner.Scan($ctx)
    $scanPython = $pythonCleaner.Scan($ctx)

    Assert-True ($scanOs.FoundItems.Count -eq 3 -and $scanOs.EstimatedBytes -eq 28672) `
        ("DeepScanOrphanFilesCleaner.Scan() found 3 items (.DS_Store, Thumbs.db, *.swp) totaling 28 KB without deleting")

    Assert-True ((Test-Path $dsStoreFile) -and (Test-Path $vimSwapFile)) `
        ("Dry-run Scan() is strictly non-destructive (files remain intact on disk)")

    Assert-True ($scanNode.FoundItems.Count -eq 2 -and $scanNode.EstimatedBytes -eq 196608) `
        ("DeepScanDirectoryCleaner (node_modules/.angular) found 2 folders totaling 192 KB")

    Assert-True ($scanPython.FoundItems.Count -eq 2 -and $scanPython.EstimatedBytes -eq 98304) `
        ("DeepScanDirectoryCleaner (__pycache__/.venv) found 2 folders totaling 96 KB")

    # B. Locked file (EBUSY / FileShare.None) resilience test during Clean()
    $lockedStream = [System.IO.File]::Open($vimSwapFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    try {
        $cleanOs = $osJunkCleaner.Clean($ctx)
        Assert-True ($cleanOs.DeletedCount -eq 2 -and $cleanOs.ErrorOrLockedCount -eq 1) `
            ("Locked file (.index.ts.swp) gracefully skipped & logged without crashing; unlocked files deleted")
    } finally {
        $lockedStream.Close()
    }

    # C. Clean developer caches & node_modules/venv and verify source code preservation
    $cleanNode   = $nodeCleaner.Clean($ctx)
    $cleanPython = $pythonCleaner.Clean($ctx)

    Assert-True ($cleanNode.FreedBytes -eq 196608 -and $cleanPython.FreedBytes -eq 98304) `
        ("Clean() freed 100% of targeted developer caches and virtualenvs (288 KB)")

    Assert-True (Test-Path $sourceFile) `
        ("Non-targeted project source file (index.ts) preserved untouched")
}
finally {
    if (Test-Path $sandboxRoot) {
        Remove-Item -Path $sandboxRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------------------
# 3. UNIT TEST: Native SQLite VACUUM (winsqlite3.dll)
# ------------------------------------------------------------------------------
Write-Host "`n[3/4] Running Chromium SQLite VACUUM Defragmentation Test..." -ForegroundColor Yellow

$tempSqlite = Join-Path $env:TEMP ("OptiAssist_VacuumTest_" + [Guid]::NewGuid().ToString("N").Substring(0, 8) + ".sqlite")
try {
    # Create a real SQLite 3 database via winsqlite3.dll, insert 800 rows of padding, then delete them
    $created = [OptiAssist.Addons.SystemCleaner.ChromiumHelper]::ExecuteSql($tempSqlite,
        "CREATE TABLE urls (id INTEGER PRIMARY KEY, url TEXT, payload TEXT); " +
        "WITH RECURSIVE cnt(x) AS (SELECT 1 UNION ALL SELECT x+1 FROM cnt LIMIT 800) " +
        "INSERT INTO urls (url, payload) SELECT 'https://example.com/' || x, zeroblob(1024) FROM cnt; " +
        "DELETE FROM urls;")

    $sizeBefore = (Get-Item $tempSqlite).Length
    $bytesSaved = [long]0
    $err        = ""
    $vacuumOk   = [OptiAssist.Addons.SystemCleaner.ChromiumHelper]::VacuumSqliteDatabase($tempSqlite, [ref]$bytesSaved, [ref]$err)
    $sizeAfter  = (Get-Item $tempSqlite).Length

    Assert-True ($created -and $vacuumOk -and $bytesSaved -gt 500000 -and [OptiAssist.Addons.SystemCleaner.ChromiumHelper]::IsValidSqliteFile($tempSqlite)) `
        ("SQLite VACUUM succeeded without corruption: reduced DB from " +
         [OptiAssist.Addons.SystemCleaner.BaseCleaner]::FormatBytes($sizeBefore) + " to " +
         [OptiAssist.Addons.SystemCleaner.BaseCleaner]::FormatBytes($sizeAfter) +
         " (Saved " + [OptiAssist.Addons.SystemCleaner.BaseCleaner]::FormatBytes($bytesSaved) + ")")
}
finally {
    if (Test-Path $tempSqlite) {
        Remove-Item $tempSqlite -Force -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------------------
# 4. LIVE NON-DESTRUCTIVE DRY-RUN SCAN REPORT ACROSS ALL 23 MODULES
# ------------------------------------------------------------------------------
Write-Host "`n[4/4] Executing Live Dry-Run Scan() Across All 23 BaseCleaner Modules..." -ForegroundColor Yellow

$liveCtx = New-Object OptiAssist.Addons.SystemCleaner.CleanerContext
# Scope Deep Scan root to the OptiAssist workspace for a fast, deterministic dry-run report
$liveCtx.DeepScanRootPath = $RepoRoot
$liveCtx.DeepScanMaxDepth = 3

$allCleaners = [OptiAssist.Addons.SystemCleaner.CleanerRegistry]::CreateAllCleaners($null)
$reportRows  = @()
$totalBytes  = [long]0
$totalItems  = 0

foreach ($cleaner in $allCleaners) {
    $sr = $cleaner.Scan($liveCtx)
    $totalBytes += $sr.EstimatedBytes
    $totalItems += $sr.FoundItems.Count

    $flags = @()
    if ($cleaner.RequiresAdmin) { $flags += "Admin" }
    if ($cleaner.IsDangerous)   { $flags += "Caution" }
    $flagStr = if ($flags.Count -gt 0) { $flags -join "," } else { "Safe" }

    $reportRows += [PSCustomObject]@{
        Category       = $cleaner.Category
        Id             = $cleaner.Id
        ModuleName     = $cleaner.Name
        Flags          = $flagStr
        FoundItems     = $sr.FoundItems.Count
        EstimatedSpace = [OptiAssist.Addons.SystemCleaner.BaseCleaner]::FormatBytes($sr.EstimatedBytes)
        BytesRaw       = $sr.EstimatedBytes
    }
}

$reportRows | Format-Table -AutoSize Category, Id, ModuleName, Flags, FoundItems, EstimatedSpace

Write-Host "------------------------------------------------------------------------------" -ForegroundColor Cyan
Write-Host ("  TOTAL MODULES SCANNED        : " + $allCleaners.Count) -ForegroundColor White
Write-Host ("  TOTAL ITEMS FOUND (DRY-RUN)  : " + $totalItems) -ForegroundColor White
Write-Host ("  TOTAL ESTIMATED RECLAIMABLE  : " + [OptiAssist.Addons.SystemCleaner.BaseCleaner]::FormatBytes($totalBytes) + " (" + $totalBytes + " bytes)") -ForegroundColor Green
Write-Host ("  UNIT TEST ASSERTIONS         : " + $passed + " passed, " + $failed + " failed") -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host "------------------------------------------------------------------------------" -ForegroundColor Cyan

if ($failed -gt 0) {
    exit 1
}
