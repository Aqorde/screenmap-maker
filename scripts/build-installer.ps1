param([switch]$VerificationBuild)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$compiler=Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
if(-not(Test-Path -LiteralPath $compiler)){throw 'Inno Setup 6 is required to build the installer.'}
$publish=Join-Path $root 'releases\0.5.9b\win-x64'
foreach($name in @('ScreenmapMaker.exe','ScreenmapCore.dll','screenmap.ico','LICENSE.txt')){
 if(-not(Test-Path -LiteralPath (Join-Path $publish $name))){throw "Missing publish artifact: $name. Run scripts/build.ps1 -Publish first."}
}
$output=if($VerificationBuild){Join-Path $root 'out\installer-test-059b'}else{Join-Path $root 'releases'}
$arguments=@("/DPublishDir=$publish","/DOutputDir=$output")
if($VerificationBuild){$arguments+='/DVerificationBuild'}
$log=Join-Path $root ('out\logs\059b-installer'+$(if($VerificationBuild){'-test'}else{''})+'.log')
& $compiler @arguments (Join-Path $root 'installer\ScreenmapMaker.iss') 2>&1|Tee-Object -FilePath $log
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
$name=if($VerificationBuild){'aqorde-ScreenMapMaker-0.5.9b-installer-test.exe'}else{'aqorde-ScreenMapMaker-0.5.9b-setup-win-x64.exe'}
Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256
