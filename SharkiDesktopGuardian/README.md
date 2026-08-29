# Desarrollo de Sharki Desktop Guardian

La guía de descarga, uso, seguridad y compilación se mantiene en el [README principal](../README.md).

Desde esta carpeta puedes validar el proyecto con:

```powershell
dotnet restore .\src\SharkiDesktopGuardian\SharkiDesktopGuardian.csproj
dotnet build .\src\SharkiDesktopGuardian\SharkiDesktopGuardian.csproj -c Release --no-restore
dotnet build .\tests\SharkiDesktopGuardian.Diagnostics\SharkiDesktopGuardian.Diagnostics.csproj -c Release
dotnet .\tests\SharkiDesktopGuardian.Diagnostics\bin\Release\net8.0-windows\SharkiDesktopGuardian.Diagnostics.dll
dotnet publish .\src\SharkiDesktopGuardian\SharkiDesktopGuardian.csproj -c Release -r win-x64 --self-contained true -o .\publish
```

La publicación es autocontenida: distribuye la carpeta `publish` completa, no solo el ejecutable.
