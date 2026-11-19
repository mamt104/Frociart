@echo off
setlocal
chcp 65001 >nul
title Frociart - Diagnostica di avvio
pushd "%~dp0"
if not exist "Frociart.exe" (
  echo Non trovo Frociart.exe. Estrai tutti i file del pacchetto prima di avviare questo comando.
  pause
  popd
  exit /b 2
)
set "FROCIART_DIAG_DIR=%LOCALAPPDATA%\Frociart\Diagnostica"
if not defined LOCALAPPDATA set "FROCIART_DIAG_DIR=%TEMP%\Frociart\Diagnostica"
if not exist "%FROCIART_DIAG_DIR%" mkdir "%FROCIART_DIAG_DIR%" >nul 2>&1
if not exist "%FROCIART_DIAG_DIR%" (
  echo Non riesco a creare la cartella dei dettagli. Avvio comunque il programma.
  start "" /wait "%~dp0Frociart.exe" --avvio-sicuro
  pause
  popd
  exit /b 3
)
set "DOTNET_HOST_TRACE=1"
set "DOTNET_HOST_TRACE_VERBOSITY=3"
set "DOTNET_HOST_TRACEFILE=%FROCIART_DIAG_DIR%\host-ultimo-avvio.txt"
echo Frociart 1.3.2 - Diagnostica di avvio > "%FROCIART_DIAG_DIR%\riepilogo.txt"
ver >> "%FROCIART_DIAG_DIR%\riepilogo.txt"
echo Architettura: %PROCESSOR_ARCHITECTURE% %PROCESSOR_ARCHITEW6432% >> "%FROCIART_DIAG_DIR%\riepilogo.txt"
echo Data: %DATE% %TIME% >> "%FROCIART_DIAG_DIR%\riepilogo.txt"
echo.
echo Avvio Frociart senza caricare il recupero della sessione precedente.
echo I diagrammi salvati non vengono modificati da questo controllo.
echo Quando chiudi Frociart si apre la cartella con i dettagli dell'avvio.
echo.
start "" /wait "%~dp0Frociart.exe" --avvio-sicuro
set "FROCIART_EXIT=%ERRORLEVEL%"
echo Codice di uscita: %FROCIART_EXIT% >> "%FROCIART_DIAG_DIR%\riepilogo.txt"
echo.
echo Codice di uscita: %FROCIART_EXIT%
echo Dettagli: "%FROCIART_DIAG_DIR%"
echo Per assistenza, condividi riepilogo.txt, host-ultimo-avvio.txt e il file avvio piu' recente.
echo Nessun file viene inviato automaticamente.
start "" "%FROCIART_DIAG_DIR%"
echo.
pause
popd
exit /b %FROCIART_EXIT%
