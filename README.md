# AudioFlow

> Controla **dónde suena cada aplicación** en Windows.

AudioFlow te permite decidir qué aplicación se reproduce en qué dispositivo de
audio. El caso de uso principal:

```
Spotify            →  🔊 Parlantes
Discord            →  🎧 Audífonos
Chrome / YouTube   →  🎧 Audífonos
Juegos             →  🎧 Audífonos
Todo lo demás      →  🎧 Audífonos (regla por defecto)
```

## Estado del proyecto

Desarrollo por fases. Este repositorio se construye de forma incremental:

| Fase | Módulo | Estado |
| ---- | ------ | ------ |
| 0 | Esqueleto del repo, licencia, build | ✅ |
| 1 | Detección de dispositivos de salida | ✅ |
| 2A | Detección de sesiones de audio | ✅ |
| 3 | Identificación estable de aplicaciones | ✅ |
| 4 | Motor de reglas + persistencia JSON | ✅ |
| 5 | Routing de audio por aplicación (híbrido) | ✅ |
| 6 | Interfaz gráfica (WPF) | ✅ |

## Requisitos

- Windows 10 (build 20348+) o Windows 11
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)

## Compilar y ejecutar

```powershell
# Compilar todo
dotnet build AudioFlow.sln -c Release

# Interfaz gráfica (WPF)
dotnet run --project src/AudioFlow.UI -c Release

# CLI (dispositivos, sesiones, reglas, routing)
dotnet run --project src/AudioFlow.Console -c Release -- devices
```

En Linux/macOS solo se puede **compilar** (con `EnableWindowsTargeting`), no ejecutar:

```bash
dotnet build AudioFlow.sln -c Release
```

## Uso rápido

1. Abre la interfaz: verás **Parlantes** y **Audífonos**.
2. Elige el dispositivo de audífonos (destino por defecto).
3. Pulsa **+ Parlantes** en las apps que deban sonar por los parlantes (p. ej. Spotify).
4. Activa **Audio Lock** para que ninguna otra app pueda usar los parlantes.
5. Pulsa **Iniciar** para aplicar las reglas automáticamente.

> **Nota sobre el routing (MVP):** AudioFlow usa la interfaz interna
> `IAudioPolicyConfigFactory` que emplea Ajustes de Windows. El cambio se aplica
> cuando la aplicación **reinicia su stream de audio**; no mueve audio en vivo.
> La Fase 5B añadirá Process Loopback (API oficial) para re-enrutado en vivo.

## Arquitectura

```
src/
├── AudioFlow.Models         Modelos de dominio (AudioDevice, AudioSessionInfo, AudioRule)
├── AudioFlow.Core           AudioDeviceManager, AudioSessionManager, AudioSessionMonitor,
│                            AudioRoutingManager, WindowsAudio (interop COM/P-Invoke)
├── AudioFlow.Applications   ProcessManager, ApplicationIdentifier
├── AudioFlow.Rules          RuleEngine, RuleStorage (JSON)
├── AudioFlow.Console        CLI para las fases 1–5
└── AudioFlow.UI             Interfaz WPF (Fase 6)
```

Ver [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) y
[docs/AUDIO-ROUTING.md](docs/AUDIO-ROUTING.md) para el análisis técnico.

## APIs de Windows utilizadas

- **WASAPI / Core Audio** (`IMMDeviceEnumerator`, `IAudioSessionManager2`,
  `IAudioSessionControl2`, `ISimpleAudioVolume`) — detección y control de sesiones.
- **`IAudioPolicyConfigFactory`** (interfaz interna, no documentada) — endpoint
  persistido por aplicación. Se usa con las limitaciones documentadas.
- **Process Loopback** (`AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS`, oficial, Win10
  build 20348+) — captura y re-enrutado por proceso.

## Licencia

[MIT](LICENSE). Puedes modificar, traducir y redistribuir libremente.
Las contribuciones de traducción (i18n) son bienvenidas.

## Descargo de responsabilidad

AudioFlow no recopila contraseñas, archivos personales, historial ni datos
privados. Solo accede a dispositivos de audio, sesiones de audio e información
de procesos.
