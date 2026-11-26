$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Source') -Filter '*.cs' | ForEach-Object FullName)
$target = Join-Path $PSScriptRoot 'ASUS Utility LITE Tests.exe'
try {
    & $compiler /nologo /target:exe /platform:x64 /optimize+ "/out:$target" "/win32icon:$PSScriptRoot\Source\App.ico" "/resource:$PSScriptRoot\Source\App.ico,ASUSUtilityLite.AppIcon" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.ServiceProcess.dll /r:Microsoft.CSharp.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
    & $target --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
} finally {
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
}
