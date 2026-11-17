param(
    [ValidatePattern('^[A-Za-z0-9_.-]+\.exe$')]
    [string]$OutputName = 'SnowDesk.exe'
)

$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    throw 'The Windows .NET Framework 4.x C# compiler was not found.'
}

$output = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references = @(
    'WPF\WindowsBase.dll'
    'WPF\UIAutomationClient.dll'
    'WPF\UIAutomationTypes.dll'
    'System.Drawing.dll'
    'System.Windows.Forms.dll'
)
$arguments = @(
    '/nologo'
    '/target:winexe'
    '/optimize+'
    '/warnaserror+'
    "/win32manifest:$PSScriptRoot\app.manifest"
    "/out:$output\$OutputName"
)
$arguments += $references | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$arguments += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' |
    ForEach-Object FullName
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "Build failed (compiler exit code $LASTEXITCODE)." }
