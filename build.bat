@echo off
title Building NVIDIA Color Switcher...
echo ===================================================
echo   Building NVIDIA Color Switcher (Single File EXE)
echo ===================================================
echo.

:: 1. Close any running instances to release Windows file locks
echo [1/3] Closing any running background instances...
taskkill /F /IM NvidiaColorSwitcher.exe >nul 2>&1

:: 2. Publish single-file executable to dist\ folder (overwrites same file)
echo [2/3] Publishing single file executable to dist\ ...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./dist

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed! Please check the output above.
    echo.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ===================================================
echo   [SUCCESS] Build Complete!
echo   Executable: dist\NvidiaColorSwitcher.exe
echo ===================================================
echo.
pause
