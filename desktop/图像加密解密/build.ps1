$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$references = @('WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll', 'WPF\WindowsBase.dll', 'System.Xaml.dll')
$compilerArgs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001')
$compilerArgs += $references | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$compilerArgs += '/resource:' + (Join-Path $PSScriptRoot 'MainWindow.xaml') + ',MainWindow.xaml'
$compilerArgs += '/resource:' + (Join-Path $PSScriptRoot 'ThirdPartyNotices.txt') + ',ThirdPartyNotices.txt'
$compilerArgs += '/out:' + (Join-Path $PSScriptRoot ((Split-Path -Leaf $PSScriptRoot) + '.exe'))
$compilerArgs += (Join-Path $PSScriptRoot 'App.cs'), (Join-Path $PSScriptRoot 'PixelScrambler.cs')
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Compiler exited with code $LASTEXITCODE" }
