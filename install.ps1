param([string]$ApplicationDirectory = "$PSScriptRoot/artifacts/Macaroni")
$ErrorActionPreference = 'Stop'

$executable = Join-Path (Resolve-Path -LiteralPath $ApplicationDirectory).Path 'Macaroni.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw 'Build Macaroni with ./build.ps1 before installing its Start menu shortcut.'
}

$programs = [Environment]::GetFolderPath('Programs')
$shell = New-Object -ComObject WScript.Shell
try {
    $shortcut = $shell.CreateShortcut((Join-Path $programs 'Macaroni.lnk'))
    $shortcut.TargetPath = $executable
    $shortcut.WorkingDirectory = Split-Path -Parent $executable
    $shortcut.Description = 'Macaroni numpad application shortcuts'
    $shortcut.IconLocation = "$executable,0"
    $shortcut.Save()
} finally {
    if ($null -ne $shortcut) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) }
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
}
Write-Host "Start menu shortcut installed for $executable"
