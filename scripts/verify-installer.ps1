# Tests the identical installer script with an isolated AppId, extension and shortcut group.
# Does not install the production package or touch the real .screenmap association.
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$base=Join-Path $root 'out\installer-test-0511rc'
$setup=Join-Path $base 'aqorde-ScreenMapMaker-0.5.11rc-installer-test.exe'
$target=[IO.Path]::GetFullPath((Join-Path $base ('installed-'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))))
if(-not $target.StartsWith($base+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe installer test target'}
$extension='Software\Classes\.screenmap-installtest'
$progId='Software\Classes\aqorde.ScreenmapMaker.InstallTestDocument'
$desktop=[Environment]::GetFolderPath('DesktopDirectory')
$versionedLink=Join-Path $desktop 'aqorde ScreenMap Maker Installer Test v0.5.11rc.lnk'
$legacyLink=Join-Path $desktop 'aqorde ScreenMap Maker Installer Test.lnk'
$previousLink=Join-Path $desktop 'aqorde ScreenMap Maker Installer Test v0.5.10b.lnk'
foreach($link in @($versionedLink,$legacyLink,$previousLink)){
 if(Test-Path -LiteralPath $link){throw "Test shortcut already exists: $link"}
}
foreach($key in @($extension,$progId)){
 $existing=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($key)
 if($null -ne $existing){$existing.Dispose();throw "Test registry key already exists: $key"}
}
$checks=[Collections.Generic.List[string]]::new()
function Check([bool]$pass,[string]$message){if(-not$pass){throw $message};$checks.Add($message)}
function Run-Setup([string]$exe,[string[]]$arguments){
 $process=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
 if(-not$process.WaitForExit(60000)){$process.Kill();throw 'Installer test timeout'}
 if($process.ExitCode -ne 0){throw "Installer test exit $($process.ExitCode)"}
}
$installed=$false
$createdPriorLinks=@()
try{
 $installArgs=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/TASKS="associate,desktopicon"',('/DIR="'+$target+'"'))
 Run-Setup $setup ($installArgs + ('/LOG="'+(Join-Path $base 'install.log')+'"'))
 $installed=$true
 $marker=Join-Path $target 'installed-mode'
 Check ((Get-Content -LiteralPath $marker -Raw).Trim() -eq 'en') 'default setup language is English and seeds the app'
 $shell=New-Object -ComObject WScript.Shell
 $exe=Join-Path $target 'ScreenmapMaker.exe'
 Check ((Test-Path -LiteralPath $versionedLink) -and $shell.CreateShortcut($versionedLink).TargetPath -eq $exe) 'desktop shortcut includes v0.5.11rc and points to the installed EXE'
 $manifest=Get-Content -LiteralPath (Join-Path $root 'releases\0.5.11rc\manifest.json') -Raw|ConvertFrom-Json
 $mismatch=@(foreach($record in $manifest.files){$path=Join-Path $target $record.path;if(-not(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -ne $record.sha256){$record.path}})
 Check ($mismatch.Count -eq 0) 'all installed payload files match release manifest SHA256'
 $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($extension)
 try{Check ($key.GetValue('') -eq 'aqorde.ScreenmapMaker.InstallTestDocument') 'test extension default points to own ProgId'}finally{$key.Dispose()}
 $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($progId+'\DefaultIcon')
 try{Check ($key.GetValue('') -eq ('"'+$target+'\screenmap.ico",0')) 'document icon points to supplied screenmap.ico'}finally{$key.Dispose()}
 $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($progId+'\shell\open\command')
 try{Check ($key.GetValue('') -eq ('"'+$target+'\ScreenmapMaker.exe" "%1"')) 'shell open command quotes application and project paths'}finally{$key.Dispose()}
 $data=Join-Path $target 'config';[IO.Directory]::CreateDirectory($data)|Out-Null
 $sentinel=Join-Path $data 'keep-user-data.txt';[IO.File]::WriteAllText($sentinel,'preserve on upgrade and uninstall')
 foreach($link in @($legacyLink,$previousLink)){
  $shortcut=$shell.CreateShortcut($link)
  $shortcut.TargetPath=$exe
  $shortcut.Save()
  $createdPriorLinks+=$link
 }
 Run-Setup $setup ($installArgs + '/LANG=russian' + ('/LOG="'+(Join-Path $base 'upgrade.log')+'"'))
 Check ((Get-Content -LiteralPath $marker -Raw).Trim() -eq 'ru') 'selecting Russian in setup seeds the app with Russian'
 Check (-not (Test-Path -LiteralPath $legacyLink)) 'upgrade replaces a matching legacy desktop shortcut'
 Check (-not (Test-Path -LiteralPath $previousLink)) 'upgrade replaces the desktop shortcut from v0.5.10b'
 Check ([IO.File]::ReadAllText($sentinel) -eq 'preserve on upgrade and uninstall') 'upgrade preserves existing user files'
 Run-Setup $setup ($installArgs + ('/LOG="'+(Join-Path $base 'english-default-after-russian.log')+'"'))
 Check ((Get-Content -LiteralPath $marker -Raw).Trim() -eq 'en') 'English is the setup default after a previous Russian install'
 # Simulate a user selecting another default application after installing.
 $key=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($extension)
 try{$key.SetValue('','Another.Test.Application')}finally{$key.Dispose()}
 Run-Setup (Join-Path $target 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $base 'uninstall.log')+'"'))
 $installed=$false
 $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($extension)
 try{Check ($null -ne $key -and $key.GetValue('') -eq 'Another.Test.Application') 'uninstall preserves a newly selected default application'}finally{if($key){$key.Dispose()}}
 $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($progId)
 Check ($null -eq $key) 'uninstall removes own ProgId';if($key){$key.Dispose()}
 Check ((Test-Path -LiteralPath $sentinel) -and -not(Test-Path -LiteralPath (Join-Path $target 'ScreenmapMaker.exe'))) 'uninstall removes program and preserves user files'
 Check (-not (Test-Path -LiteralPath $versionedLink)) 'uninstall removes the versioned desktop shortcut'
 # The remaining key is test-owned and was created only by this run.
 [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($extension,$false)
 [pscustomobject]@{success=$true;checks=$checks;productionAssociationTouched=$false;productionInstallerInstalled=$false;target=$target}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $base 'verification.json') -Encoding utf8
 $checks
}finally{
 if($installed -and (Test-Path -LiteralPath (Join-Path $target 'unins000.exe'))){Run-Setup (Join-Path $target 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')}
 foreach($link in $createdPriorLinks){
  if(Test-Path -LiteralPath $link){Remove-Item -LiteralPath $link}
 }
}
