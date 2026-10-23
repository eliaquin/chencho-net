param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repositoryDirectory = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryDirectory 'artifacts\release' }
$stagingDirectory = Join-Path $repositoryDirectory ('artifacts\package-' + [guid]::NewGuid().ToString('N'))
$appDirectory = Join-Path $stagingDirectory 'app'
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Push-Location $repositoryDirectory
try {
    dotnet publish src/Carnets.Desktop/Carnets.Desktop.csproj -c Release -r win-x64 --self-contained true -p:DebugType=None -o $appDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install.ps1') -Destination $stagingDirectory
    Copy-Item -LiteralPath (Join-Path $repositoryDirectory 'docs\guia-operacion.md') -Destination (Join-Path $stagingDirectory 'LEEME.md')
    Copy-Item -LiteralPath (Join-Path $repositoryDirectory 'docs\licencias.md') -Destination $stagingDirectory
    Copy-Item -LiteralPath (Join-Path $repositoryDirectory 'docs\third-party') -Destination $stagingDirectory -Recurse
    $archive = Join-Path $OutputDirectory 'Carnets-1.0.0-preview.1-win-x64.zip'
    Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $stagingDirectory -Force).FullName -DestinationPath $archive -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    Set-Content -LiteralPath ($archive + '.sha256') -Value ($hash + '  ' + [IO.Path]::GetFileName($archive))
    Write-Output "Paquete: $archive"
} finally { Pop-Location }
