Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  正在编译与运行自动化测试套件 (包含高倍速平衡性测试)" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

dotnet build --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] C# 编译失败！" -ForegroundColor Red
    exit 1
}

$godotExe = Join-Path $projectRoot "Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
& $godotExe --headless res://test/TestRunner.tscn

if ($LASTEXITCODE -eq 0) {
    Write-Host "[SUCCESS] 所有测试全部通过！" -ForegroundColor Green
} else {
    Write-Host "[FAILURE] 测试未通过，请检查上方日志！" -ForegroundColor Red
}

exit $LASTEXITCODE
