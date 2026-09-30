@echo off
rem Builds and starts One More Year. Set GODOT to your Godot 4.6 .NET executable if it lives elsewhere.
if "%GODOT%"=="" set "GODOT=E:\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe"
echo Building...
dotnet build "%~dp0game\OneMoreYear.csproj" -v q -nologo
if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)
rem New fonts and other assets must be imported before the game can use them (quick when nothing changed).
echo Importing assets...
"%GODOT%" --headless --path "%~dp0game" --import >nul 2>&1
start "" "%GODOT%" --path "%~dp0game"
