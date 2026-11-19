@echo off
setlocal
pushd "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
  echo Per ricompilare serve l'SDK .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0
  echo Il programma pronto e' Frociart.exe nella cartella principale del pacchetto.
  pause
  popd
  exit /b 1
)
dotnet publish Frociart.csproj -c Release -r win-x64 --self-contained true -o "bin\win-x64"
if errorlevel 1 (
  echo Compilazione non riuscita. Controlla di avere l'SDK .NET 10.
  pause
  popd
  exit /b 1
)
echo.
echo Compilazione riuscita: bin\win-x64\Frociart.exe
echo L'eseguibile contiene il runtime e puo' essere copiato su un PC Windows 10/11 a 64 bit.
pause
popd
endlocal
