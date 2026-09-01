# Sharki Desktop Guardian

[![Integración continua](https://github.com/SharkIT-sys/sharkIT-Desktop-Guardian/actions/workflows/ci.yml/badge.svg)](https://github.com/SharkIT-sys/sharkIT-Desktop-Guardian/actions/workflows/ci.yml)
[![Última versión](https://img.shields.io/github/v/release/SharkIT-sys/sharkIT-Desktop-Guardian?label=versi%C3%B3n)](https://github.com/SharkIT-sys/sharkIT-Desktop-Guardian/releases/latest)

Mascota de escritorio para Windows 11 que muestra el estado local de CPU, GPU NVIDIA, RAM y almacenamiento. Funciona sin conexión, no instala servicios y no envía telemetría.

## Descargar y empezar

**[Descargar la última versión para Windows x64](https://github.com/SharkIT-sys/sharkIT-Desktop-Guardian/releases/latest/download/Sharki-Desktop-Guardian-win-x64.zip)**

[Comprobar el SHA-256 de la última versión](https://github.com/SharkIT-sys/sharkIT-Desktop-Guardian/releases/latest/download/Sharki-Desktop-Guardian-win-x64.sha256)

1. Descarga el archivo ZIP y elige **Extraer todo**.
2. Abre la carpeta extraída.
3. Haz doble clic en `SharkiDesktopGuardian.exe`.

No requiere instalación, cuenta, conexión a Internet ni tener .NET instalado. Conserva toda la carpeta extraída: el modelo de voz y las dependencias forman parte de ella.

> La aplicación todavía no está firmada digitalmente. Windows SmartScreen puede mostrar un aviso al ejecutarla por primera vez; comprueba que la descarga procede de este repositorio. Cada versión incluye un archivo SHA-256 para verificar su integridad.

### Requisitos

- Windows 11 de 64 bits.
- GPU NVIDIA opcional. Sin ella, Sharki sigue funcionando y muestra como no disponibles las métricas exclusivas de NVIDIA.
- Micrófono opcional para las órdenes de voz.

## Funciones principales

- Monitorización local de CPU, GPU NVIDIA, RAM, discos y velocidad de red.
- Sharki y Mummy seleccionables, con reposo estático y un saludo único al pasar el cursor.
- Avisos visuales compactos con animaciones específicas para cada tipo de alerta.
- Panel de rendimiento redimensionable y movible, con posición persistente.
- Icono en la bandeja del sistema para abrir, ocultar o cerrar Sharki.
- Reconocimiento de voz local mediante una gramática cerrada y modelo Vosk incluido.
- Síntesis de voz SAPI5 local, estilo normal o robótico y botón rápido **Silenciar / Activar voz**.
- Modo demostración y tutorial integrado.
- Ajustes portátiles guardados en `Data/settings.json`, junto al ejecutable.

## Uso

- **Doble clic en Sharki**: abre o cierra el panel de rendimiento.
- **Clic derecho**: abre el menú contextual.
- **Arrastrar Sharki**: cambia su posición.
- **Ctrl+Alt+S** o botón de micrófono: escucha una orden durante 8 segundos.
- **Icono de la bandeja**: abre, oculta o cierra la aplicación.

Por defecto, Sharki solicita elevación mediante el cuadro UAC para intentar leer temperaturas de CPU y discos. Si cancelas el aviso, continúa funcionando con las métricas disponibles. Este comportamiento se puede desactivar en **Ajustes → Sensores avanzados**.

### Cuando aparece «No disponible» o «—»

Sharki solo muestra valores que Windows, el controlador o el propio hardware exponen de forma fiable. Si un sensor no está accesible, aparece como **No disponible** o `—` y no se inventa un valor ni se genera una alerta falsa.

Los porcentajes y temperaturas configurables en **Ajustes** son únicamente umbrales para decidir cuándo mostrar un aviso. Cambiarlos no activa, calibra ni hace compatible un sensor.

#### Activar los sensores avanzados

1. Abre **Ajustes → Sensores avanzados**.
2. Activa **Iniciar siempre con permisos de administrador**.
3. Cierra Sharki por completo desde el icono de la bandeja.
4. Vuelve a abrirla y acepta el aviso de Control de cuentas de usuario (UAC).

La elevación mejora el acceso a determinados sensores, pero no garantiza que todos estén disponibles: también depende del equipo, el firmware, el controlador y de cómo el fabricante publique los datos.

| Dato | Qué necesita | Qué hacer si no aparece |
|---|---|---|
| Uso de CPU y RAM | Funciones estándar de Windows; no deberían requerir permisos de administrador | Espera una actualización del panel y reinicia Sharki. Si continúa ausente, es un fallo que conviene comunicar con el diagnóstico de la aplicación. |
| Temperatura de CPU | Permisos elevados y un sensor compatible que LibreHardwareMonitor pueda leer | Activa los sensores avanzados y reinicia aceptando UAC. Si sigue como **No disponible**, no existe un ajuste que pueda forzarlo: el sensor no está siendo expuesto de forma compatible. |
| GPU NVIDIA: carga, temperatura y VRAM | Una GPU NVIDIA, el controlador oficial instalado y `nvidia-smi.exe` operativo | Actualiza o reinstala el controlador oficial de NVIDIA y comprueba `nvidia-smi` en una terminal. Las métricas de GPU Intel o AMD no están soportadas actualmente en esta sección. |
| Espacio y modelo de los discos | Una unidad fija, montada, preparada y visible para Windows | Las unidades extraíbles, desconectadas o que Windows no marque como listas pueden no aparecer. El espacio usado y libre no depende del sensor de temperatura. |
| Temperatura de discos / SMART | Permisos elevados y que la unidad y su controlador expongan telemetría compatible | Activa los sensores avanzados. Algunas unidades NVMe y, especialmente, discos conectados mediante USB, RAID o ciertos controladores pueden seguir sin ofrecer la temperatura. |
| Velocidad de red | Una interfaz de red activa que no sea de bucle local ni túnel | La primera lectura aparece como `—` porque hacen falta dos muestras para calcular la velocidad. Espera al siguiente intervalo; si no hay tráfico o interfaz activa, puede permanecer sin dato. |

Para comprobar la GPU NVIDIA desde PowerShell o Símbolo del sistema:

```powershell
nvidia-smi
```

Si el comando no existe o devuelve un error, Sharki tampoco podrá obtener esas métricas hasta que el controlador de NVIDIA funcione correctamente.

### Órdenes de voz

“Sharki”, “Sharky” o “Charqui” son opcionales antes de cada orden. El reconocedor solo acepta frases de una lista blanca; no interpreta texto libre ni lo envía a PowerShell, `cmd.exe` o un servicio remoto.

| Orden | Acción |
|---|---|
| `estado del sistema` | Abre el panel y resume CPU, GPU y RAM |
| `abre el panel` / `cierra el panel` | Muestra u oculta el panel |
| `pausa la monitorización` / `reanuda la monitorización` | Controla la lectura de sensores |
| `qué temperatura tienes` | Indica las temperaturas disponibles |
| `cómo está la memoria` / `cómo están los discos` | Resume RAM o almacenamiento |
| `habla` / `silencio` | Activa o desactiva las respuestas habladas |
| `qué puedes hacer` | Muestra la ayuda de voz |
| `qué hora es` / `qué día es hoy` | Dice la hora o la fecha local |
| `abre el explorador` / `abre la calculadora` / `abre el bloc de notas` | Abre una utilidad fija de Windows |
| `abre el administrador de tareas` / `abre la configuración` / `abre la papelera` | Abre una utilidad fija de Windows |
| `muestra el escritorio` | Minimiza las ventanas |
| `bloquea el equipo` | Bloquea Windows tras confirmación explícita |
| `activa sensores avanzados` | Solicita elevación tras confirmación explícita |
| `salir de Sharki` | Cierra únicamente Sharki tras confirmación explícita |

## Privacidad y seguridad

- No hay telemetría, cuentas, API remota ni clientes HTTP.
- El audio se procesa localmente y no se conserva.
- Las órdenes de voz se convierten únicamente en acciones internas enumeradas.
- Las acciones sensibles requieren confirmación y la elevación siempre pasa por UAC.
- Los sensores no accesibles aparecen como **No disponible**, sin generar alertas falsas.

Consulta [Arquitectura y límites de seguridad](SharkiDesktopGuardian/docs/ARCHITECTURE.md) para conocer el diseño técnico.

## Compilar desde el código fuente

Necesitas el SDK de .NET 8 y Windows x64. Desde la raíz del repositorio:

```powershell
dotnet restore .\SharkiDesktopGuardian\src\SharkiDesktopGuardian\SharkiDesktopGuardian.csproj
dotnet build .\SharkiDesktopGuardian\src\SharkiDesktopGuardian\SharkiDesktopGuardian.csproj -c Release --no-restore
dotnet publish .\SharkiDesktopGuardian\src\SharkiDesktopGuardian\SharkiDesktopGuardian.csproj -c Release -r win-x64 --self-contained true -o .\SharkiDesktopGuardian\publish
```

El ejecutable portátil queda en `SharkiDesktopGuardian\publish\SharkiDesktopGuardian.exe`.

### Diagnóstico

```powershell
dotnet build .\SharkiDesktopGuardian\tests\SharkiDesktopGuardian.Diagnostics\SharkiDesktopGuardian.Diagnostics.csproj -c Release
dotnet .\SharkiDesktopGuardian\tests\SharkiDesktopGuardian.Diagnostics\bin\Release\net8.0-windows\SharkiDesktopGuardian.Diagnostics.dll
```

El resultado correcto termina con `"ok": true` y `"failures": []`. La integración continua repite la compilación y este diagnóstico en Windows con cada cambio.

## Versiones y soporte

- Las versiones listas para usar están en [Releases](https://github.com/SharkIT-sys/sharkIT-Desktop-Guardian/releases).
- Los cambios se documentan en [CHANGELOG.md](CHANGELOG.md).
- Para informar de un fallo, abre un [issue](https://github.com/SharkIT-sys/sharkIT-Desktop-Guardian/issues) sin incluir información sensible.

## Licencia

Copyright 2026 SharkIT-sys.

El proyecto permite usar, copiar, modificar y redistribuir el software únicamente para fines no comerciales bajo la [PolyForm Noncommercial License 1.0.0](LICENSE.md). Cualquier uso comercial requiere un [acuerdo escrito independiente](COMMERCIAL_USE.md) con SharkIT-sys.

Los componentes de terceros conservan sus propias licencias, detalladas en [THIRD_PARTY_NOTICES.md](SharkiDesktopGuardian/THIRD_PARTY_NOTICES.md).
