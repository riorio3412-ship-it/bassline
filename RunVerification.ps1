param([string]$UnityCli = "$env:LOCALAPPDATA/Unity/bin/unity.exe", [string]$ProjectPath = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
if ($projectRoot.Length -gt 120) { throw 'Use a short project path, for example -ProjectPath C:/Users/리오/OneDrive/Desktop/BASSLINE, to avoid Unity package import path limits.' }
if (-not (Test-Path -LiteralPath $UnityCli)) { throw 'Pass -UnityCli with the installed Unity CLI path.' }
Push-Location $projectRoot
try {
    & $UnityCli run $projectRoot --timeout 600 --format json -- -executeMethod BASSLINE.Authoring.VerificationExporter.BuildAndExport -logFile (Join-Path $projectRoot 'Verification/reproduce-builder.log')
    if ($LASTEXITCODE -ne 0) { throw 'Unity builder failed; see Verification/reproduce-builder.log.' }
    foreach ($mode in @('EditMode', 'PlayMode')) {
        & $UnityCli test $projectRoot --mode $mode --timeout 600 --format json --output (Join-Path $projectRoot "Verification/reproduce-$mode.xml") -- -logFile (Join-Path $projectRoot "Verification/reproduce-$mode.log")
        if ($LASTEXITCODE -ne 0) { throw "$mode failed; inspect the XML and log." }
    }
    Write-Output 'PASS: implemented Unity test subsets. Full production acceptance remains incomplete; see ImplementationStatus.md.'
} finally { Pop-Location }
