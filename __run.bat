@echo off
setlocal
pushd "%~dp0" >nul

docker compose up --build -d
if errorlevel 1 (
    popd >nul
    exit /b 1
)

start "" "http://localhost:5000"

popd >nul
endlocal