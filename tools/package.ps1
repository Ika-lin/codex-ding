$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$version = "v0.1.0"
$dist = Join-Path $root "dist"
$stage = Join-Path $dist "CodexPeek-$version"
$zip = Join-Path $dist "CodexPeek-$version-portable.zip"

if (Test-Path $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stage | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage "assets") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage "emojis") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage "sounds") | Out-Null

Copy-Item -LiteralPath (Join-Path $root "CodexPeek.exe") -Destination $stage
Copy-Item -LiteralPath (Join-Path $root "CodexPeek.ini") -Destination $stage
Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination $stage
Copy-Item -LiteralPath (Join-Path $root "USAGE.zh-CN.md") -Destination $stage
Copy-Item -LiteralPath (Join-Path $root "VERSION.txt") -Destination $stage
Copy-Item -LiteralPath (Join-Path $root "Run Codex Peek.bat") -Destination $stage
Copy-Item -LiteralPath (Join-Path $root "Test Peek.bat") -Destination $stage
Copy-Item -LiteralPath (Join-Path $root "Package Codex Peek.bat") -Destination $stage
Copy-Item -Path (Join-Path $root "assets\*.png") -Destination (Join-Path $stage "assets")
Copy-Item -Path (Join-Path $root "assets\*.ico") -Destination (Join-Path $stage "assets") -ErrorAction SilentlyContinue
Copy-Item -Path (Join-Path $root "assets\*.wav") -Destination (Join-Path $stage "assets") -ErrorAction SilentlyContinue
Copy-Item -Path (Join-Path $root "assets\*.mp3") -Destination (Join-Path $stage "assets") -ErrorAction SilentlyContinue
Copy-Item -Path (Join-Path $root "emojis\*.png") -Destination (Join-Path $stage "emojis") -ErrorAction SilentlyContinue
Copy-Item -Path (Join-Path $root "sounds\*.wav") -Destination (Join-Path $stage "sounds") -ErrorAction SilentlyContinue
Copy-Item -Path (Join-Path $root "sounds\*.mp3") -Destination (Join-Path $stage "sounds") -ErrorAction SilentlyContinue

if (Test-Path $zip) {
    Remove-Item -LiteralPath $zip -Force
}
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -Force
Write-Host $zip
