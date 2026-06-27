$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$icon = Join-Path $root "assets\codex-peek.ico"
$out = Join-Path $root "CodexPeek.exe"

$refs = @(
    "System.dll",
    "System.Core.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll")
)

$sources = Get-ChildItem -LiteralPath (Join-Path $root "src") -Filter "*.cs" | Sort-Object Name | ForEach-Object { $_.FullName }
$args = @("/nologo", "/target:winexe", "/win32icon:$icon", "/out:$out")
$args += $refs | ForEach-Object { "/reference:$_" }
$args += $sources

& $csc @args
Write-Host $out
