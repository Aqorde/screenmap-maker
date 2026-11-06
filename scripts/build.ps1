param(
 [ValidateSet('Debug','Release')][string]$Configuration='Release',
 [switch]$Both,[switch]$Publish,[Alias('Smoke')][switch]$Verify,[switch]$DesktopRender
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$logs=Join-Path $root 'out\logs';[IO.Directory]::CreateDirectory($logs)|Out-Null
$vswhere='C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(-not$vs){throw 'MSVC is required'}
$cmake=Join-Path $vs 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
$ctest=Join-Path (Split-Path $cmake) 'ctest.exe'
$native=Join-Path $root 'out\build\native-v0512rc'
$project=Join-Path $root 'desktop\Screenmap.App\Screenmap.App.csproj'
function Run([string]$name,[scriptblock]$command){
 & $command 2>&1|Tee-Object -FilePath (Join-Path $logs "0512rc-$name.log")
 if($LASTEXITCODE -ne 0){throw "$name failed: $LASTEXITCODE"}
}
function Verify-App([string]$exe,[string[]]$arguments,[string]$report){
 $p=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
 if(-not$p.WaitForExit(60000)){$p.Kill();throw "Verification timeout: $report"}
 if($p.ExitCode -ne 0){throw "Verification failed: $report"}
 if($report -and -not(Test-Path -LiteralPath $report)){throw "Missing verification report: $report"}
}
Push-Location $root
try{
 $active=@(Get-Process -Name ScreenmapMaker -ErrorAction SilentlyContinue|Where-Object{$_.Path -and ($_.Path -like "$root\out\desktop-v0512rc\*\net10.0\*" -or $_.Path -like "$root\releases\0.5.12rc\*")})
 if($active.Count){throw 'Close Screenmap Maker v0.5.12rc before rebuilding or publishing.'}
 Run 'configure' {& $cmake -S $root -B $native -G 'Visual Studio 18 2026' -A x64 "-DCMAKE_GENERATOR_INSTANCE=$vs"}
 $configs=if($Both){@('Debug','Release')}else{@($Configuration)}
 foreach($config in $configs){
  Run "native-$config" {& $cmake --build $native --config $config --parallel 4}
  Run "native-tests-$config" {& $ctest --test-dir $native -C $config --output-on-failure}
  Run "desktop-$config" {dotnet build $project -c $config --nologo}
 }
 $release=Join-Path $root 'releases\0.5.12rc\win-x64'
 if($Publish){
  if(-not($configs -contains 'Release')){throw '-Publish requires Release or -Both'}
  Run 'publish' {dotnet publish $project -c Release -r win-x64 --self-contained true -o $release --nologo}
  Copy-Item -LiteralPath (Join-Path $root 'README-RU.md') -Destination $release -Force
  Copy-Item -LiteralPath (Join-Path $root 'LICENSE.txt'),(Join-Path $root 'screenmap.ico') -Destination $release -Force
  $help=Join-Path $release 'docs';[IO.Directory]::CreateDirectory($help)|Out-Null
  Copy-Item -LiteralPath (Join-Path $root 'docs\TESTING-0.5.md') -Destination $help -Force
  $licenses=Join-Path $release 'licenses';[IO.Directory]::CreateDirectory($licenses)|Out-Null
  Copy-Item -LiteralPath (Join-Path $root 'assets\licenses\Avalonia-LICENSE.txt') -Destination $licenses -Force
  Copy-Item -LiteralPath (Join-Path $root 'assets\licenses\THIRD-PARTY-0512rc.md') -Destination $licenses -Force
  $cache=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget\packages'}
  $deps=Get-Content -LiteralPath (Join-Path $release 'ScreenmapMaker.deps.json') -Raw|ConvertFrom-Json
  foreach($item in $deps.libraries.PSObject.Properties){
   if($item.Value.type -ne 'package'){continue}
   $path=Join-Path $cache $item.Name.ToLowerInvariant()
   if(-not(Test-Path -LiteralPath $path)){continue}
   $label=$item.Name.Replace('/','-')
   foreach($file in Get-ChildItem -LiteralPath $path -File -Recurse|Where-Object{$_.Name -match '(?i)^(license|third.party.notices|notice)'}){
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $licenses ($label+'-'+$file.Name)) -Force
   }
  }
  $runtime=Get-Content -LiteralPath (Join-Path $release 'ScreenmapMaker.runtimeconfig.json') -Raw|ConvertFrom-Json
  foreach($f in $runtime.runtimeOptions.includedFrameworks){
   if($f.name -eq 'Microsoft.NETCore.App'){
    $path=Join-Path $cache "microsoft.netcore.app.runtime.win-x64\$($f.version)"
    Copy-Item -LiteralPath (Join-Path $path 'LICENSE.TXT') -Destination (Join-Path $licenses 'DOTNET-LICENSE.txt') -Force
    Copy-Item -LiteralPath (Join-Path $path 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $licenses 'DOTNET-THIRD-PARTY-NOTICES.txt') -Force
   }
  }
  if(Get-ChildItem -LiteralPath $release -Filter 'Adonis*.dll'){throw 'Obsolete UI dependency in v0.5.12rc package'}
 }
 $exe=if($Publish){Join-Path $release 'ScreenmapMaker.exe'}else{Join-Path $root "out\desktop-v0512rc\$Configuration\net10.0\win-x64\ScreenmapMaker.exe"}
 if($Verify){
  $stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
  $output=Join-Path $root "out\verification-0512rc\$stamp"
  Verify-App $exe @('--verify',('"'+$output+'"')) (Join-Path $output 'verification.json')
  Get-Content -LiteralPath (Join-Path $output 'verification.json')
  Write-Output "VERIFICATION_DIRECTORY=$output"
  if($DesktopRender){
   $desktopOutput=Join-Path $output 'desktop-backend'
   $case=Join-Path $output 'configs\01-perspective-dark.json'
   Verify-App $exe @('--render-config',('"'+$case+'"'),'--output',('"'+$desktopOutput+'"'),'--desktop-render') (Join-Path $desktopOutput 'viewport.png')
   Write-Output "DESKTOP_OWN_RENDER=$desktopOutput"
  }
 }
 if($Publish){
  $files=@(Get-ChildItem -LiteralPath $release -File -Recurse|Where-Object{[IO.Path]::GetRelativePath($release,$_.FullName) -notmatch '^config[\\/]'})
  $records=@(foreach($file in $files){[pscustomobject]@{path=[IO.Path]::GetRelativePath($release,$file.FullName);bytes=$file.Length;sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}})
  [pscustomobject]@{version='0.5.12rc';ui='Avalonia 12.1.4';nativeAbi=2;files=$records}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $root 'releases\0.5.12rc\manifest.json') -Encoding utf8
  $package=Join-Path $root ('out\package\0512rc-'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')+'\win-x64')
  [IO.Directory]::CreateDirectory($package)|Out-Null
  foreach($file in $files){$dest=Join-Path $package ([IO.Path]::GetRelativePath($release,$file.FullName));[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dest))|Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $dest -Force}
  $zip=Join-Path $root 'releases\aqorde-ScreenMapMaker-0.5.12rc-win-x64.zip'
  Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal -Force
  Write-Output "RELEASE=$release"
  Get-FileHash -LiteralPath $zip -Algorithm SHA256|Format-List
 }
}finally{Pop-Location}
