param([string]$Package)
$ErrorActionPreference = 'Stop'
$repositoryDirectory = Split-Path $PSScriptRoot -Parent
if (-not $Package) { $Package = Join-Path $repositoryDirectory 'artifacts\release\Carnets-1.0.0-preview.1-win-x64.zip' }
$checkRoot = Join-Path $repositoryDirectory ('artifacts\install-test-' + [guid]::NewGuid().ToString('N'))
$extracted = Join-Path $checkRoot 'extracted'
$isolatedLocalAppData = Join-Path $checkRoot 'local-app-data'
New-Item -ItemType Directory -Path $isolatedLocalAppData -Force | Out-Null
Expand-Archive -LiteralPath $Package -DestinationPath $extracted
$settingsDirectory = Join-Path $isolatedLocalAppData 'ChenchoNet'
New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
$settingsPath = Join-Path $settingsDirectory 'settings.json'
Set-Content -LiteralPath $settingsPath -Encoding UTF8 -Value '{"SchemaVersion":1,"ColumnHeaders":{"nombre":"NOMBRE"},"PhotosFolder":"C:\\demo-photos","FrontX":1.25,"BackRotation":180,"Mode":0}'
$originalSettingsHash = (Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash
$priorLocalAppData = $env:LOCALAPPDATA
try {
    # Change the child script's default install root, never the user's real settings or shortcuts.
    $env:LOCALAPPDATA = $isolatedLocalAppData
    & (Join-Path $extracted 'Install.ps1') -SkipShortcuts
    & (Join-Path $extracted 'Install.ps1') -SkipShortcuts
    if ((Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash -ne $originalSettingsHash) { throw 'La reinstalación cambió los ajustes del usuario.' }
    $installedExe = Join-Path $isolatedLocalAppData 'Programs\ChenchoNet\1.0.0-preview.1\Carnets.Desktop.exe'
    $diagnosticOutput = Join-Path $checkRoot 'verification'
    $process = Start-Process -FilePath $installedExe -ArgumentList @('--self-check', ('"' + $diagnosticOutput + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "El diagnóstico instalado falló. Consulte $diagnosticOutput" }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $isAdministrator = (New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    $result = [ordered]@{
        InstalledFromZip = $true
        ReinstalledWithoutAdmin = -not $isAdministrator
        CompatibleSettingsPreservedByteForByte = $true
        ShortcutsSkippedForIsolation = $true
        InstalledSelfCheckExitCode = $process.ExitCode
        PackageSha256 = (Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash
    }
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $checkRoot 'install-check.json') -Encoding UTF8
    Write-Output "Verificación: $checkRoot"
} finally { $env:LOCALAPPDATA = $priorLocalAppData }
