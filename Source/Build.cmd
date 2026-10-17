@echo off
setlocal
cd /d "%~dp0.."
set "COREPILOT_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
"%COREPILOT_CSC%" /nologo /target:winexe /platform:x64 /main:CorePilot /win32icon:CorePilot.ico /resource:CorePilot.ico,CorePilot.ico /win32manifest:CorePilot.manifest /out:CorePilot.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll /reference:System.Security.dll Source\CorePilot.cs Source\StableGrid.cs Source\ApplicationGroups.cs Source\CcdDiscovery.cs Source\TopologyModels.cs Source\Native.cs Source\AppStore.cs Source\SchedulerEngine.cs Source\Recovery.cs Source\StartupTask.cs Source\LocatorModels.cs
if errorlevel 1 exit /b 1
"%COREPILOT_CSC%" /nologo /target:winexe /platform:x64 /main:CoreLocator /out:CorePilot.Locator.exe /reference:CorePilot.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Source\CoreLocator.cs
exit /b %errorlevel%


