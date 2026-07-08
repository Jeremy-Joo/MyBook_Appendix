@echo off
rem Remove Visual Studio build/debug artifacts before git upload
cd /d "%~dp0"

echo ============================================
echo  Clean Visual Studio build artifacts
echo  Target: %~dp0
echo ============================================
echo.
echo The following will be deleted recursively:
echo   - bin, obj, .vs folders
echo   - packages folder (NuGet restore will recreate it)
echo   - *.user, *.suo files
echo.
set /p CONFIRM=Continue? (Y/N):
if /i not "%CONFIRM%"=="Y" goto :end

echo.
rem Delete bin / obj / .vs folders in all subdirectories (including root)
for /d /r %%d in (bin obj .vs) do (
    if exist "%%d" (
        echo Deleting: %%d
        rd /s /q "%%d"
    )
)

rem Delete NuGet packages folder at root
if exist "packages" (
    echo Deleting: %~dp0packages
    rd /s /q "packages"
)

rem Delete user-specific files
del /s /q /a *.user 2>nul
del /s /q /a *.suo 2>nul

echo.
echo Done.

:end
pause
