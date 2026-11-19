param([switch]$SkipAppBuild)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    if(-not $SkipAppBuild){
        & .\build-framework.cmd
        if($LASTEXITCODE -ne 0){throw 'Build app non riuscita.'}
    }
    if(-not(Test-Path -LiteralPath 'bin\framework\Frociart.exe')){throw 'EXE app mancante.'}
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if(-not(Test-Path -LiteralPath $compiler)){$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'}
    & $compiler /nologo /codepage:65001 /target:winexe /optimize+ /out:bin\framework\Frociart_Setup.exe /win32manifest:app.manifest /win32icon:Frociart.ico /resource:bin\framework\Frociart.exe,Frociart.Payload.exe /resource:Frociart.Document.ico,Frociart.Document.ico /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll SetupProgram.cs BrandArtwork.cs SetupAssemblyInfo.cs FileRegistration.cs InstallIntegration.cs
    if($LASTEXITCODE -ne 0){throw 'Build della configurazione non riuscita.'}
    Write-Output 'Build app e configurazione completata.'
} finally {Pop-Location}
