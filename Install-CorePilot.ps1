#requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
$source = Split-Path -Parent $MyInvocation.MyCommand.Path
$destination = Join-Path $env:ProgramFiles 'CorePilot'
$running = Get-Process -Name CorePilot -ErrorAction SilentlyContinue
if ($running) { throw '请先关闭 CorePilot，再运行安装脚本。' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force
& icacls.exe $destination /inheritance:r
if ($LASTEXITCODE -ne 0) { throw '无法关闭安装目录的 ACL 继承。' }
& icacls.exe $destination /setowner '*S-1-5-32-544' /T /C
if ($LASTEXITCODE -ne 0) { throw '无法设置安装目录所有者。' }
& icacls.exe $destination /grant:r '*S-1-5-18:(OI)(CI)(F)' '*S-1-5-32-544:(OI)(CI)(F)' '*S-1-5-32-545:(OI)(CI)(RX)' /T /C
if ($LASTEXITCODE -ne 0) { throw '无法设置受保护的安装目录权限。' }
$exe = Join-Path $destination 'CorePilot.exe'
$process = Start-Process -FilePath $exe -ArgumentList '--install-autostart' -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "已复制程序，但登录自启注册失败（退出码 $($process.ExitCode)）。" }
Write-Host "CorePilot 已安装到 $destination，登录自启已注册。"
