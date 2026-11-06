[CmdletBinding()]
param([string]$PythonVersion='3.13.17')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$stage=Join-Path $root 'artifact\CM-IMAP-Sicherung'
$dist=Join-Path $root 'dist'
if(Test-Path $stage){Remove-Item -LiteralPath $stage -Recurse -Force}
New-Item -ItemType Directory -Path $stage,$dist -Force | Out-Null
Copy-Item -LiteralPath 'CMIMAPSicherung\bin\Release\CM IMAP Sicherung.exe' -Destination $stage
Copy-Item -LiteralPath 'CMIMAPSicherung\bin\Release\py.exe' -Destination $stage
$runtime=Join-Path $stage 'runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$tempRoot=if($env:RUNNER_TEMP){$env:RUNNER_TEMP}else{$env:TEMP}
$runtimeZip=Join-Path $tempRoot 'cm-python-embed.zip'
$uri="https://www.python.org/ftp/python/$PythonVersion/python-$PythonVersion-embed-amd64.zip"
Invoke-WebRequest -Uri $uri -OutFile $runtimeZip
$runtimeHash=(Get-FileHash $runtimeZip -Algorithm SHA256).Hash.ToLowerInvariant()
Expand-Archive -LiteralPath $runtimeZip -DestinationPath $runtime -Force
$site=Join-Path $runtime 'Lib\site-packages'
New-Item -ItemType Directory -Path $site -Force | Out-Null
& python -m pip install --disable-pip-version-check --no-compile --only-binary=:all: --target $site 'pyzipper==0.3.6' 'pycryptodomex==3.23.0'
if($LASTEXITCODE -ne 0){throw 'Installing pinned ZIP dependencies failed.'}
$imapCommit='ad926dc1f10300a9f580996a09c8690ea175373a'
& python -m pip install --disable-pip-version-check --no-compile --no-deps --target $site "https://github.com/DavidMStraub/imapbackup3/archive/$imapCommit.zip"
if($LASTEXITCODE -ne 0){throw 'Installing pinned IMAP backup engine failed.'}
@('python313.zip','.','Lib/site-packages','import site') | Set-Content -LiteralPath (Join-Path $runtime 'python313._pth') -Encoding ascii
Copy-Item -LiteralPath 'tools\sitecustomize.py' -Destination $site
Get-ChildItem $site -Directory -Filter '__pycache__' -Recurse | Remove-Item -Recurse -Force
$launcher=Join-Path $stage 'py.exe'
& $launcher -3 -c 'import sys, ssl, imapbackup3.cli, pyzipper; assert sys.version_info[:2] == (3,13); c=ssl.create_default_context(); assert c.check_hostname; print("PACKAGED_PYTHON_IMPORTS_PASS")'
if($LASTEXITCODE -ne 0){throw 'Bundled interpreter or dependency smoke test failed.'}
Copy-Item -LiteralPath 'README.md' -Destination $stage
Copy-Item -LiteralPath 'RELEASE-NOTES-1.4.0.2.md' -Destination (Join-Path $stage 'RELEASE-NOTES.md')
Copy-Item -LiteralPath 'docs\ARCHIVIERUNG.md' -Destination (Join-Path $stage 'ARCHIVIERUNG.md')
Copy-Item -LiteralPath 'docs\THIRD-PARTY.md' -Destination $stage
if(Test-Path 'support-url.txt'){Copy-Item -LiteralPath 'support-url.txt' -Destination $stage}
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $stage 'CM IMAP Sicherung.exe')).FileVersion
if($version -ne '1.4.0.2'){throw "Unexpected executable version: $version"}
$commit=(& git rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0){throw 'Commit identification failed.'}
[ordered]@{version=$version;source_commit=$commit;python=$PythonVersion;python_archive_sha256=$runtimeHash;imapbackup3_commit=$imapCommit;pyzipper='0.3.6';pycryptodomex='3.23.0';code_signed=$false;built_utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'BUILD-INFO.json') -Encoding utf8
$forbidden=Get-ChildItem $stage -Recurse -File | Where-Object {$_.Extension -in @('.cred','.pwd','.eml','.mbox','.pdb') -or $_.Name -eq 'config.xml'}
if($forbidden){throw 'Private data or debug files found in release package.'}
$lines=Get-ChildItem $stage -Recurse -File | Sort-Object FullName | ForEach-Object {"$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($_.FullName.Substring($stage.Length+1).Replace('\','/'))"}
$lines | Set-Content -LiteralPath (Join-Path $stage 'FILES.sha256') -Encoding utf8
$zip=Join-Path $dist "CM-IMAP-Sicherung-v$version-Windows-x64.zip"
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal -Force
"$((Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant())  $(Split-Path $zip -Leaf)" | Set-Content -LiteralPath "$zip.sha256" -Encoding ascii
Write-Host "PACKAGE_READY $zip"
