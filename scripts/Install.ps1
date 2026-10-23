param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\ChenchoNet'),
    [switch]$DesktopShortcut,
    [switch]$SkipShortcuts
)
$ErrorActionPreference = 'Stop'
$packageSource = Join-Path $PSScriptRoot 'app'
if (-not (Test-Path -LiteralPath (Join-Path $packageSource 'Carnets.Desktop.exe'))) { throw 'Falta la carpeta app del paquete. Extraiga el ZIP completo antes de instalar.' }
$applicationDestination = [IO.Path]::GetFullPath((Join-Path $Destination '1.0.0-preview.1'))
New-Item -ItemType Directory -Path $applicationDestination -Force | Out-Null
foreach ($packageFile in Get-ChildItem -LiteralPath $packageSource -Force) {
    Copy-Item -LiteralPath $packageFile.FullName -Destination $applicationDestination -Recurse -Force
}
$applicationExe = Join-Path $applicationDestination 'Carnets.Desktop.exe'
if (-not $SkipShortcuts) {
    $shortcutShell = New-Object -ComObject WScript.Shell
    $shortcutLocations = @([Environment]::GetFolderPath('Programs'))
    if ($DesktopShortcut) { $shortcutLocations += [Environment]::GetFolderPath('Desktop') }
    foreach ($shortcutLocation in $shortcutLocations) {
        $shortcut = $shortcutShell.CreateShortcut((Join-Path $shortcutLocation 'Carnets ICPARD.lnk'))
        $shortcut.TargetPath = $applicationExe
        $shortcut.WorkingDirectory = $applicationDestination
        $shortcut.Description = 'Preparación local de carnets y simulación de impresión'
        $shortcut.Save()
    }
}
Write-Output "Instalado: $applicationExe"
Write-Output 'La configuración permanece en %LocalAppData%\ChenchoNet, separada del programa. No se necesita administrador para operar.'
