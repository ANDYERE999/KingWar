@echo off
setlocal
cd /d "%~dp0.."

call dotnet build --nologo
if errorlevel 1 goto :fail

".\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless res://test/TestRunner.tscn
if errorlevel 1 goto :fail

echo.
echo ========================================================
echo [SUCCESS] ALL TESTS COMPLETED SUCCESSFULLY!
echo ========================================================
exit /b 0

:fail
echo.
echo ========================================================
echo [FAILURE] TESTS FAILED - SEE LOGS ABOVE
echo ========================================================
exit /b 1
