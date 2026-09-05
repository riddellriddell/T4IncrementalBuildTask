@echo off
setlocal

rem ============================================================
rem  Automated test runner for the standalone T4CodeGen.exe.
rem  Builds the library, the CLI exe, and the harness offline,
rem  then runs the black-box case battery. Exits 0 only if all
rem  cases pass. No NuGet, no network.
rem ============================================================

set "ROOT=%~dp0"

rem ---- resolve MSBuild (VS2022 Developer-environment fallback) ----
set "MSBUILD="
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD if exist "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if not defined MSBUILD set "MSBUILD=msbuild"

echo [test] building task library...
"%MSBUILD%" "%ROOT%CustomBuildTasks\CustomBuildTasks.csproj" -v:m -nologo
if errorlevel 1 exit /b 1

echo [test] building CLI exe...
"%MSBUILD%" "%ROOT%T4CodeGen\T4CodeGen.csproj" -v:m -nologo
if errorlevel 1 exit /b 1

echo [test] building harness...
"%MSBUILD%" "%ROOT%T4CodeGenTests\T4CodeGenTests.csproj" -v:m -nologo
if errorlevel 1 exit /b 1

echo [test] running case battery...
"%ROOT%T4CodeGenTests\bin\Debug\T4CodeGenTests.exe"
exit /b %errorlevel%