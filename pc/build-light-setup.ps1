param(
    [Parameter(Mandatory=$true)][string]$DotNetPath,
    [Parameter(Mandatory=$true)][string]$InnoCompilerPath,
    [string]$OutputDirectory=(Join-Path $PSScriptRoot 'bin\light-setup'),
    [string]$NuGetConfig,
    [string]$NuGetPackages
)
$ErrorActionPreference='Stop'
$sdkExecutable=[IO.Path]::GetFullPath($DotNetPath)
if(-not(Test-Path -LiteralPath $sdkExecutable -PathType Leaf)){throw 'SDK .NET non trovato.'}
$innoExecutable=[IO.Path]::GetFullPath($InnoCompilerPath)
$compilerPackage=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'LightSetup\InstallerCompiler.json') -Raw | ConvertFrom-Json
if(-not(Test-Path -LiteralPath $innoExecutable -PathType Leaf) -or $compilerPackage.version -ne '7.1.0' -or
    (Get-FileHash -LiteralPath $innoExecutable -Algorithm SHA256).Hash.ToLowerInvariant() -cne $compilerPackage.compiler_sha256){throw 'Compilatore Inno Setup non corrispondente alla versione verificata.'}
$buildRoot=[IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($buildRoot) | Out-Null
$package=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'LightSetup\RuntimePackage.json') -Raw | ConvertFrom-Json
if($package.version -notmatch '^10\.0\.[0-9]+$' -or $package.architecture -ne 'x64' -or $package.sha512 -notmatch '^[0-9a-f]{128}$'){throw 'Manifest del runtime non valido.'}
$expectedUrl='https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/'+$package.version+'/windowsdesktop-runtime-'+$package.version+'-win-x64.exe'
if($package.url -cne $expectedUrl){throw 'Origine del runtime non valida.'}
$appOutput=Join-Path $buildRoot 'application'
$publishArgs=@('publish',(Join-Path $PSScriptRoot 'Frociart.csproj'),'-c','Release','-r','win-x64','--self-contained','false','--disable-build-servers','-v','minimal',
    ('-p:RuntimeFrameworkVersion='+$package.version),'-p:RollForward=LatestPatch','-p:EnableCompressionInSingleFile=false',
    ('-p:BaseIntermediateOutputPath='+(Join-Path $buildRoot 'obj')+[IO.Path]::DirectorySeparatorChar),
    ('-p:BaseOutputPath='+(Join-Path $buildRoot 'bin')+[IO.Path]::DirectorySeparatorChar),
    ('-p:PublishDir='+$appOutput+[IO.Path]::DirectorySeparatorChar),'-p:UseSharedCompilation=false','-p:GenerateRuntimeConfigDevFile=false')
if($NuGetConfig){$publishArgs+=@('--configfile',[IO.Path]::GetFullPath($NuGetConfig))}
if($NuGetPackages){$publishArgs+=('-p:RestorePackagesPath='+[IO.Path]::GetFullPath($NuGetPackages))}
& $sdkExecutable @publishArgs
if($LASTEXITCODE -ne 0){throw 'Build app non riuscita.'}
$payload=Join-Path $appOutput 'Frociart.exe'
if(-not(Test-Path -LiteralPath $payload -PathType Leaf)){throw 'App mancante.'}
if((Get-Item -LiteralPath $payload).Length -gt 16MB){throw 'Il pacchetto contiene componenti inattesi.'}
$icon=Join-Path $PSScriptRoot 'Frociart.Document.ico'
$appHash=(Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash.ToLowerInvariant()
$iconHash=(Get-FileHash -LiteralPath $icon -Algorithm SHA256).Hash.ToLowerInvariant()
$applicationVersion=(Get-Item -LiteralPath $payload).VersionInfo.FileVersion
if($applicationVersion -notmatch '^\d+\.\d+\.\d+\.\d+$'){throw 'Versione del programma non valida.'}
$managedOutput=Join-Path $buildRoot 'managed-payload'
[IO.Directory]::CreateDirectory($managedOutput) | Out-Null
$innoArgs=@('--no-ide-signtools','--quiet',('--define=AppPayload='+$payload),('--define=DocumentIcon='+$icon),
    ('--define=VendorLicense='+(Join-Path $PSScriptRoot 'LightSetup\Licenze\Inno-Setup-LICENSE.txt')),('--define=OutputDir='+$managedOutput),
    ('--define=ApplicationHash='+$appHash),('--define=DocumentHash='+$iconHash),
    (Join-Path $PSScriptRoot 'LightSetup\FrociartPayload.iss'))
& $innoExecutable @innoArgs
if($LASTEXITCODE -ne 0){throw 'Build del payload di installazione non riuscita.'}
$managedPayload=Join-Path $managedOutput 'Frociart.ApplicationPayload.exe'
if(-not(Test-Path -LiteralPath $managedPayload -PathType Leaf) -or (Get-Item -LiteralPath $managedPayload).Length -gt 16MB){throw 'Payload di installazione non valido.'}
$managedHash=(Get-FileHash -LiteralPath $managedPayload -Algorithm SHA256).Hash.ToLowerInvariant()
$probeOutput=Join-Path $buildRoot 'probe'
$probeArgs=@('build',(Join-Path $PSScriptRoot 'LightSetup\RuntimeProbe\RuntimeProbe.csproj'),'-c','Release','--disable-build-servers','-v','minimal',
    ('-p:RuntimeFrameworkVersion='+$package.version),('-p:OutputPath='+$probeOutput+[IO.Path]::DirectorySeparatorChar),
    ('-p:BaseIntermediateOutputPath='+(Join-Path $buildRoot 'probe-obj')+[IO.Path]::DirectorySeparatorChar),'-p:UseSharedCompilation=false')
if($NuGetConfig){$probeArgs+=@('--configfile',[IO.Path]::GetFullPath($NuGetConfig))}
if($NuGetPackages){$probeArgs+=('-p:RestorePackagesPath='+[IO.Path]::GetFullPath($NuGetPackages))}
& $sdkExecutable @probeArgs
if($LASTEXITCODE -ne 0){throw 'Build verifica runtime non riuscita.'}
$probeAssembly=Join-Path $probeOutput 'Frociart.RuntimeProbe.dll'
$probeConfig=Join-Path $probeOutput 'Frociart.RuntimeProbe.runtimeconfig.json'
$probeHash=(Get-FileHash -LiteralPath $probeAssembly -Algorithm SHA256).Hash.ToLowerInvariant()
$probeConfigHash=(Get-FileHash -LiteralPath $probeConfig -Algorithm SHA256).Hash.ToLowerInvariant()
$identity='namespace Frociart.LightSetup { internal static class PackageIdentity {'+
    ' internal const string RuntimeVersion="'+$package.version+'";'+
    ' internal const string RuntimeUrl="'+$package.url+'";'+
    ' internal const string RuntimeSha512="'+$package.sha512+'";'+
    ' internal const string ApplicationSha256="'+$appHash+'";'+
    ' internal const string ApplicationVersion="'+$applicationVersion+'";'+
    ' internal const string ManagedPayloadSha256="'+$managedHash+'";'+
    ' internal const string IconSha256="'+$iconHash+'";'+
    ' internal const string ProbeSha256="'+$probeHash+'";'+
    ' internal const string ProbeConfigSha256="'+$probeConfigHash+'"; } }'
$identityPath=Join-Path $buildRoot 'PackageIdentity.cs'
[IO.File]::WriteAllText($identityPath,$identity,[Text.UTF8Encoding]::new($false))
$sdkVersion=(& $sdkExecutable --version).Trim()
if($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$'){throw 'Versione SDK non valida.'}
$compiler=Join-Path ([IO.Path]::GetDirectoryName($sdkExecutable)) ('sdk\'+$sdkVersion+'\Roslyn\bincore\csc.dll')
if(-not(Test-Path -LiteralPath $compiler)){throw 'Compilatore SDK non trovato.'}
$frameworkReferences=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if(-not(Test-Path -LiteralPath $frameworkReferences)){$frameworkReferences=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'}
$setupOutput=Join-Path $buildRoot 'Frociart_v1_Setup_Leggero.exe'
$setupSources=@('LightSetup\LightSetupProgram.cs','LightSetup\DownloadProgress.cs','LightSetup\SetupEngine.cs','LightSetup\WindowsSetupHost.cs','LightSetup\ManagedInstallation.cs','LightSetup\ShellLink.cs','LightSetup\RuntimeDetection.cs','LightSetup\MicrosoftDownload.cs','LightSetup\MicrosoftSignature.cs','LightSetup\AssemblyInfo.cs','BrandArtwork.cs','FileRegistration.cs','InstallIntegration.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
$compilerArgs=@('exec',$compiler,'/nologo','/noconfig','/nostdlib','/langversion:5','/deterministic+','/debug:embedded','/codepage:65001','/target:winexe','/optimize+','/platform:anycpu',('/out:'+$setupOutput),
    ('/pathmap:'+$buildRoot+'=/_/SetupBuild,'+$PSScriptRoot+'=/_/Frociart'),
    ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')),('/win32icon:'+(Join-Path $PSScriptRoot 'Frociart.ico')),
    ('/resource:'+$icon+',Frociart.Document.ico'),
    ('/resource:'+$managedPayload+',Frociart.ManagedPayload.exe'),
    ('/resource:'+$probeAssembly+',Frociart.RuntimeProbe.dll'),('/resource:'+$probeConfig+',Frociart.RuntimeProbe.runtimeconfig.json'),
    ('/r:'+(Join-Path $frameworkReferences 'mscorlib.dll')),('/r:'+(Join-Path $frameworkReferences 'System.dll')),('/r:'+(Join-Path $frameworkReferences 'System.Core.dll')),('/r:'+(Join-Path $frameworkReferences 'System.Drawing.dll')),('/r:'+(Join-Path $frameworkReferences 'System.Windows.Forms.dll')))+$setupSources+@($identityPath)
& $sdkExecutable @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Build installer non riuscita.'}
$result=[ordered]@{status='BUILT_NOT_RELEASE_CERTIFIED';runtime=$package;installer_compiler=$compilerPackage;application=[ordered]@{path=$payload;version=$applicationVersion;bytes=(Get-Item -LiteralPath $payload).Length;sha256=$appHash};managed_payload=[ordered]@{path=$managedPayload;bytes=(Get-Item -LiteralPath $managedPayload).Length;sha256=$managedHash};setup=[ordered]@{path=$setupOutput;bytes=(Get-Item -LiteralPath $setupOutput).Length;sha256=(Get-FileHash -LiteralPath $setupOutput -Algorithm SHA256).Hash.ToLowerInvariant()};system_components_installed=$false}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $buildRoot 'build-manifest.json') -Encoding UTF8
Write-Output 'Installer leggero compilato. Nessun componente di sistema installato.'
