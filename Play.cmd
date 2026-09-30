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
start "" "%GODOT%" --path "%~dp0game"
