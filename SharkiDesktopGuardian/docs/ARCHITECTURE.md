# Arquitectura y límites de seguridad

## Flujo local

1. `HardwareMonitorService` consulta sensores cada dos segundos.
2. `AlertEvaluator` convierte las métricas en un estado visual con prioridad determinista.
3. `MainWindow` selecciona animación, expresión y mensaje; `DashboardWindow` presenta los valores.
4. `LocalVoiceService` reconoce una gramática cerrada mediante SAPI o el modelo español Vosk empaquetado.
5. `SafeCommandRouter` traduce el resultado a un `SafeCommand` enumerado.

No existe ninguna ruta desde una frase reconocida hasta PowerShell, `cmd.exe`, scripts o una línea de comandos arbitraria. `SystemShortcutLauncher` expone un puñado fijo de utilidades estándar de Windows (Explorador, Calculadora, Bloc de notas, Administrador de tareas, Configuración, Papelera, mostrar escritorio, bloquear equipo): cada orden de voz se traduce a exactamente un ejecutable o API fija, sin argumentos ni rutas derivadas del texto reconocido, y usando siempre `UseShellExecute` en vez de un intérprete de comandos.

## Prioridad de reacciones

1. Temperatura crítica: ojos rojos, animación de alarma y aviso.
2. Poco espacio: reacción de bloqueo y aviso ámbar.
3. RAM elevada: reacción de preocupación.
4. Carga elevada de CPU/GPU: animación de trabajo acelerada.
5. Estado normal: reposo y movimiento configurado.

Los umbrales se aplican con histéresis para evitar parpadeos cerca del límite.

## Proveedores de hardware

- CPU: `GetSystemTimes` para carga; sensor de paquete mediante LibreHardwareMonitor cuando el equipo lo expone.
- GPU NVIDIA: `nvidia-smi.exe` local, sin red; LibreHardwareMonitor como alternativa.
- RAM: `GlobalMemoryStatusEx`.
- NVMe: LibreHardwareMonitor para modelo/temperatura y `DriveInfo` para capacidad libre de volúmenes fijos.

Algunos sensores de bajo nivel pueden requerir elevación en ciertos controladores. Por defecto, Sharki solicita elevarse al arrancar mediante el cuadro UAC; si se cancela, continúa sin esos sensores. La opción se puede desactivar y nunca existe una elevación silenciosa.

## Persistencia

`Data/settings.json` contiene únicamente preferencias y la posición de Sharki y del panel. No se guarda audio, transcripciones históricas ni telemetría.

## Distribución

La versión pública se genera en Windows mediante `dotnet publish` para `win-x64` y de forma autocontenida. GitHub Actions ejecuta primero el diagnóstico, crea un ZIP portátil y publica tanto el paquete como su suma SHA-256 en GitHub Releases. El paquete no requiere instalar .NET ni descargar componentes en tiempo de ejecución.
