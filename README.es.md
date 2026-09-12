# AudioFlow

> **Enrutado de audio por aplicación para Windows.**

AudioFlow te permite decidir **dónde suena cada aplicación**. Caso de uso principal:

```
Spotify            ->  Parlantes
Discord            ->  Audífonos
Chrome / YouTube   ->  Audífonos
Juegos             ->  Audífonos
Todo lo demás      ->  Audífonos (regla por defecto)
```

[English](README.md)

---

## Funciones

- Detecta todos los dispositivos de salida (parlantes, audífonos, Bluetooth, monitores, virtuales).
- Detecta aplicaciones que están reproduciendo audio en tiempo real (sesiones WASAPI).
- Identificación estable (nombre del ejecutable + hash de ruta + AUMID para apps de la Store).
- Reglas por aplicación y una salida predeterminada global.
- **Bloqueo de audio** (protección parcial) para evitar que apps no autorizadas usen un dispositivo.
- Interfaz WPF oscura y moderna con localización **inglés y español**.
- Verificación objetiva del routing (mide el nivel real de cada endpoint).
- Módulo experimental de **Process Loopback** (API oficial, aislado del MVP).

## Requisitos

- Windows 10 (build 20348+) o Windows 11
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) (para compilar)
- .NET 8 Desktop Runtime (para ejecutar)

## Instalación

**Instalador (recomendado):** descarga `AudioFlow-Setup-v<version>.exe` desde
[Releases](https://github.com/JavierparraDev/audioflow/releases) y ejecútalo.
Instala en Program Files, crea un acceso en el menú Inicio y registra el
desinstalador. Tu configuración en `%APPDATA%\AudioFlow` nunca se toca.

**Portable:** descarga `AudioFlow-v<version>-win-x64.zip`, extráelo y ejecuta
`AudioFlow.exe`. Los datos se guardan en una carpeta `data\` junto a la app.

**Desde el código:**

```powershell
git clone https://github.com/JavierparraDev/audioflow.git
cd audioflow
./install.ps1 -Launch      # instalación local, sin admin
./install.ps1 -Release     # genera instalador + ZIP portable (requiere Inno Setup)
```

Ver [docs/INSTALLATION.md](docs/INSTALLATION.md).

## Actualizaciones

AudioFlow comprueba GitHub Releases al iniciar (configurable) y muestra el
resultado en **Configuración > Actualizaciones**. Si hay una versión nueva,
**Actualizar ahora** la descarga, verifica su SHA-256 y se la pasa al proceso
independiente `AudioFlow.Updater.exe`, que hace copia de tu configuración, aplica
la actualización y reinicia AudioFlow. Nunca se reemplazan archivos mientras la
app está en ejecución, y AudioFlow sigue funcionando sin conexión.

Ver [docs/UPDATES.md](docs/UPDATES.md).

## Sesión y seguridad

El routing es **por sesión**. Mientras AudioFlow está abierto, las reglas pueden
estar activas; al cerrarlo, Windows vuelve a su comportamiento normal. AudioFlow
captura el estado original, escribe un marcador de recuperación atómico antes de
cambiar nada y restaura al salir. Si AudioFlow se cae, el siguiente arranque
restaura el audio de Windows **antes** de nada y nunca reactiva el routing solo.

```powershell
audioflow session   # ACTIVE | INACTIVE | STALE
audioflow restore   # RESTORE SUCCESS | RESTORE FAILED
.\tools\emergency-restore.ps1   # restaurar sin la UI
```

Ver [docs/SESSION-RULES.md](docs/SESSION-RULES.md) y
[docs/CRASH-RECOVERY.md](docs/CRASH-RECOVERY.md).

Un **Session Guardian** independiente (`AudioFlow.SessionGuardian.exe`) vigila
AudioFlow mientras se ejecuta y restaura el audio de Windows en segundos si
AudioFlow se cae, sin esperar a reiniciar. Las desconexiones de dispositivo se
manejan restaurando solo las aplicaciones afectadas. Ver
[docs/SESSION-GUARDIAN.md](docs/SESSION-GUARDIAN.md) y
[docs/DEVICE-RECOVERY.md](docs/DEVICE-RECOVERY.md).

```powershell
audioflow diagnostics   # sesión / guardian / dispositivos / rutas
audioflow guardian status
```

## Uso

1. Ejecuta `publish/ui/AudioFlow.exe`.
2. Elige los dispositivos **Parlantes** y **Audífonos**.
3. En **Aplicaciones**, pulsa **+ Parlantes** en las apps que deban usar los parlantes (p. ej. Spotify).
4. Opcionalmente activa **Bloqueo de audio** para que otras apps no usen los parlantes.
5. Pulsa **Iniciar** para aplicar las reglas continuamente.

CLI (diagnóstico):

```powershell
audioflow devices        # lista endpoints de salida
audioflow sessions       # lista sesiones de audio activas
audioflow plan           # muestra el routing resuelto
audioflow apply          # aplica las reglas a las sesiones activas
audioflow verify "Parlantes"   # mide el nivel real de audio por endpoint
audioflow loopback-probe <pid> # sonda experimental de process loopback
audioflow version             # muestra la versión (p. ej. AudioFlow 0.2.0)
audioflow update --check      # comprueba actualizaciones en GitHub Releases
```

## Arquitectura

```
src/
├── AudioFlow.Models         Modelos de dominio
├── AudioFlow.Core           Dispositivos, sesiones, routing, verificación
├── AudioFlow.Applications   Gestor de procesos + identificación estable
├── AudioFlow.Rules          Motor de reglas + persistencia JSON
├── AudioFlow.Configuration  Rutas, settings, migraciones (instalado/portable)
├── AudioFlow.Updates        Comparación de versiones, GitHub, checksums
├── AudioFlow.ProcessLoopback Experimental (API oficial Process Loopback)
├── AudioFlow.Updater        Actualizador independiente y verificado
├── AudioFlow.Console        CLI
└── AudioFlow.UI             App WPF (localizada)
```

Ver [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Limitaciones del routing (importante)

AudioFlow usa el mismo mecanismo interno que
*Configuración > Sistema > Sonido > Mezclador de volumen > Opciones avanzadas*
(`IAudioPolicyConfigFactory`).

- Fija el **endpoint de salida persistido** para un proceso.
- El cambio se aplica cuando la aplicación **(re)inicia su stream de audio**.
- **No** mueve audio en vivo.

En nuestras propias pruebas en Windows 11, un experimento controlado de reinicio
de stream **no** movió el audio del proceso de prueba. Por lo tanto el routing
debe considerarse **PARCIAL**, no en vivo. Ver
[docs/AUDIO-ROUTING.md](docs/AUDIO-ROUTING.md) y [docs/LIMITATIONS.md](docs/LIMITATIONS.md).

## Limitaciones del Bloqueo de audio

El bloqueo de audio **redirige el audio gestionado por AudioFlow**. Las
aplicaciones que usan su propio endpoint, modo exclusivo o que ignoran la
política pueden saltárselo. Es **protección parcial**, no un bloqueo duro.

## Process Loopback (experimental)

`src/AudioFlow.ProcessLoopback` usa la API oficial de Process Loopback de Windows
(`AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS`, Windows 10 build 20348+). Puede activar
una interfaz de captura por proceso (verificado en Windows 11), pero aún no está
integrado en el MVP ni re-renderiza audio.

## Tests

```powershell
dotnet test tests/AudioFlow.Tests          # tests unitarios (cualquier SO)
dotnet test tests/AudioFlow.Windows.Tests  # integración Windows (skip fuera de Windows)
```

Ver [docs/TESTING.md](docs/TESTING.md) y [docs/TEST-REPORT.md](docs/TEST-REPORT.md).

## Desarrollo

- .NET 8, C#, WPF, NAudio.Wasapi.
- `dotnet build AudioFlow.sln -c Release`
- SDK fijado en [global.json](global.json).
- CI: [.github/workflows/build.yml](.github/workflows/build.yml).

## Contribuir

Se aceptan issues y pull requests. Mantén los textos de UI localizados (agrega
claves en `Strings.resx` y `Strings.es.resx`) y añade tests para los cambios.

## Licencia

[MIT](LICENSE) - Copyright (c) 2026 Javier Parra.

Componentes de terceros: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Privacidad

AudioFlow no recopila contraseñas, archivos personales, historial ni datos
privados. Solo accede a dispositivos de audio, sesiones de audio e información
de procesos.
