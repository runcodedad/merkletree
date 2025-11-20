@echo off
REM MerkleTree Benchmarks Runner Script (Windows)
REM This script provides convenient ways to run various benchmark scenarios

setlocal
cd /d "%~dp0MerkleTree.Benchmarks"

echo MerkleTree Performance Benchmarks
echo ==================================
echo.

set SCENARIO=%1
if "%SCENARIO%"=="" set SCENARIO=all

if /I "%SCENARIO%"=="all" (
    echo Running ALL benchmarks...
    dotnet run -c Release
    goto :end
)

if /I "%SCENARIO%"=="tree" (
    echo Running tree building benchmarks...
    dotnet run -c Release -- --filter *TreeBuilding*
    goto :end
)

if /I "%SCENARIO%"=="proof" (
    echo Running proof generation and verification benchmarks...
    dotnet run -c Release -- --filter *Proof*
    goto :end
)

if /I "%SCENARIO%"=="cache" (
    echo Running cache performance benchmarks...
    dotnet run -c Release -- --filter *Cache*
    goto :end
)

if /I "%SCENARIO%"=="serialize" (
    echo Running serialization benchmarks...
    dotnet run -c Release -- --filter *Serialization*
    goto :end
)

if /I "%SCENARIO%"=="fast" (
    echo Running fast benchmarks only (small datasets)...
    dotnet run -c Release -- --filter *Small*
    goto :end
)

if /I "%SCENARIO%"=="streaming" (
    echo Running streaming tree benchmarks...
    dotnet run -c Release -- --filter *Streaming*
    goto :end
)

if /I "%SCENARIO%"=="memory" (
    echo Running benchmarks with memory profiling...
    dotnet run -c Release -- --memory
    goto :end
)

echo Usage: %~nx0 [all^|tree^|proof^|cache^|serialize^|fast^|streaming^|memory]
echo.
echo Options:
echo   all        - Run all benchmarks (default)
echo   tree       - Run tree building benchmarks
echo   proof      - Run proof generation and verification benchmarks
echo   cache      - Run cache performance benchmarks
echo   serialize  - Run serialization/deserialization benchmarks
echo   fast       - Run only fast benchmarks (small datasets)
echo   streaming  - Run streaming tree benchmarks
echo   memory     - Run with memory profiling enabled
exit /b 1

:end
echo.
echo Benchmarks complete! Results are in BenchmarkDotNet.Artifacts/
endlocal
