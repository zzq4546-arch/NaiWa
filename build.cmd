@echo off
setlocal
cd /d "%~dp0"

rem ---- locate the .NET Framework 4.x C# compiler (ships with Windows 10/11) ----
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" goto :nocsc

set "GAC=%WINDIR%\Microsoft.NET\assembly"
set "PF=%GAC%\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll"
set "PC=%GAC%\GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll"
set "WB=%GAC%\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll"
set "SX=%GAC%\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll"
if not exist "%PF%" goto :noref
if not exist "%PC%" goto :noref
if not exist "%WB%" goto :noref
if not exist "%SX%" goto :noref

if not exist "assets\naiwa-idle.png" goto :noasset
if not exist "assets\naiwa.ico" goto :noasset

"%CSC%" -nologo -codepage:65001 -target:winexe -platform:x64 -optimize+ ^
  -out:"NaiWa.exe" ^
  -win32icon:assets\naiwa.ico ^
  -resource:assets\naiwa-idle.png,naiwa.idle.png ^
  -resource:assets\naiwa-laugh-strip.png,naiwa.laugh.png ^
  -resource:assets\naiwa-laugh.wav,naiwa.laugh.wav ^
  -resource:assets\skin1.png,naiwa.skin1.png ^
  -resource:assets\skin2.png,naiwa.skin2.png ^
  -resource:assets\skin3.png,naiwa.skin3.png ^
  -resource:assets\skin4.png,naiwa.skin4.png ^
  -r:"%PF%" -r:"%PC%" -r:"%WB%" -r:"%SX%" ^
  -r:System.Drawing.dll -r:System.Windows.Forms.dll ^
  src\Program.cs src\Settings.cs src\PetWindow.cs src\TrayIcon.cs src\Log.cs src\MiniPetWindow.cs src\SettingsWindow.cs src\TodoStore.cs src\BubbleWindow.cs src\WelcomeWindow.cs src\TodoWindow.cs
if errorlevel 1 goto :failed
echo.
echo Build OK: NaiWa.exe
exit /b 0

:nocsc
echo [ERROR] .NET Framework C# compiler not found.
exit /b 1
:noref
echo [ERROR] WPF reference assemblies not found in the GAC.
exit /b 1
:noasset
echo [ERROR] missing assets\naiwa-idle.png or assets\naiwa.ico
exit /b 1
:failed
echo [ERROR] compile failed
exit /b 1
