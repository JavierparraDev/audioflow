<div align="center">

<img src="docs/assets/banner.svg" alt="AudioFlow — enrutado de audio por aplicación para Windows" width="100%" />

<br/>

**Decide dónde suena cada aplicación en Windows.**

[![build](https://img.shields.io/github/actions/workflow/status/JavierparraDev/audioflow/build.yml?branch=main&label=build&logo=github)](https://github.com/JavierparraDev/audioflow/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/JavierparraDev/audioflow?include_prereleases&label=release&logo=github)](https://github.com/JavierparraDev/audioflow/releases)
[![license](https://img.shields.io/github/license/JavierparraDev/audioflow?label=license)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![stars](https://img.shields.io/github/stars/JavierparraDev/audioflow?style=flat&logo=github)](https://github.com/JavierparraDev/audioflow/stargazers)
[![forks](https://img.shields.io/github/forks/JavierparraDev/audioflow?style=flat&logo=github)](https://github.com/JavierparraDev/audioflow/network/members)
[![issues](https://img.shields.io/github/issues/JavierparraDev/audioflow?style=flat&logo=github)](https://github.com/JavierparraDev/audioflow/issues)
[![last commit](https://img.shields.io/github/last-commit/JavierparraDev/audioflow?style=flat&logo=github)](https://github.com/JavierparraDev/audioflow/commits/main)
[![PRs welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)

[English](README.md) · [Español](README.es.md) · [Releases](https://github.com/JavierparraDev/audioflow/releases) · [Roadmap](docs/ROADMAP.md) · [Contribuir](CONTRIBUTING.md)

**Idiomas de la interfaz:** English · Español · Türkçe · Français · Deutsch · Português (Brasil) · Italiano · 日本語 · Русский

</div>

---

## ¿Qué es AudioFlow?

AudioFlow te permite decidir **dónde suena cada aplicación**. El caso clásico:
mantén la música en los **parlantes** mientras juegos, chat de voz y video
suenan en los **audífonos** — al mismo tiempo y sin desconectar nada.

```
Spotify            ->  Parlantes
Discord            ->  Audífonos
Chrome / YouTube   ->  Audífonos
Juegos             ->  Audífonos
Todo lo demás      ->  Audífonos (regla por defecto)
```

AudioFlow es una app nativa de **Windows 10/11** (WPF, interfaz oscura) con una
CLI para diagnóstico. El routing es **por sesión**: al cerrar AudioFlow, Windows
vuelve a su comportamiento normal y no queda nada atrás.

## Funciones

| | |
|---|---|
| 🎧 **Detección de dispositivos** | Parlantes, audífonos, Bluetooth, monitores y dispositivos virtuales. |
| 🔎 **Sesiones en vivo** | Detecta aplicaciones que reproducen audio en tiempo real (WASAPI). |
| 🎯 **Identidad estable** | Nombre del ejecutable + hash de ruta + AUMID para apps de la Store. |
| 🧩 **Reglas por app** | Reglas por aplicación y una salida predeterminada global. |
| 🔒 **Bloqueo de audio** | Protección parcial para evitar que apps no autorizadas usen un dispositivo. |
| 🌍 **UI localizada** | Interfaz WPF oscura y moderna en **inglés y español**. |
| ✅ **Verificación** | Comprobación objetiva del routing que mide el nivel real por endpoint. |
| 🧪 **Process Loopback** | Módulo experimental sobre la API oficial de Windows, aislado del MVP. |

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

## Inicio rápido

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
audioflow routing             # backends de routing + estado del endpoint virtual
```

## Índice

- [¿Qué es AudioFlow?](#qué-es-audioflow)
- [Funciones](#funciones)
- [Requisitos](#requisitos)
- [Instalación](#instalación)
- [Inicio rápido](#inicio-rápido)
- [Actualizaciones](#actualizaciones)
- [Sesión y seguridad](#sesión-y-seguridad)
- [Backends de routing](#backends-de-routing)
- [Arquitectura](#arquitectura)
- [Limitaciones del routing (importante)](#limitaciones-del-routing-importante)
- [Limitaciones del Bloqueo de audio](#limitaciones-del-bloqueo-de-audio)
- [Process Loopback (experimental)](#process-loopback-experimental)
- [Tests](#tests)
- [Desarrollo](#desarrollo)
- [Contribuir y hacer fork](#contribuir-y-hacer-fork)
- [Licencia](#licencia)
- [Privacidad](#privacidad)

## Actualizaciones

AudioFlow comprueba GitHub Releases al iniciar (configurable) y muestra el
resultado en **Configuración > Actualizaciones**. Si hay una versión nueva,
**Actualizar ahora** la descarga, verifica su SHA-256 y se la pasa al proceso
independiente `AudioFlow.Updater.exe`, que hace copia de tu configuración, aplica
la actualización y reinicia AudioFlow. Nunca se reemplazan archivos mientras la
app está en ejecución, y AudioFlow sigue funcionando sin conexión.

Ver [docs/UPDATES.md](docs/UPDATES.md).

## Sesión y seguridad

El routing es **por sesión y totalmente efímero**. Mientras AudioFlow está
abierto, las reglas viven en memoria y pueden estar activas; al cerrarlo, Windows
vuelve a su comportamiento normal y **no queda nada**: ni archivo de reglas, ni
logs, ni cambios en el registro de audio. AudioFlow captura el estado original
(incluido el registro de audio por aplicación) antes de cambiar nada y lo
restaura exactamente al salir, incluso para aplicaciones que ya no están en
ejecución. Si AudioFlow se cae, el siguiente arranque restaura el audio de
Windows **antes** de nada y nunca reactiva el routing solo.

```powershell
audioflow session   # ACTIVE | INACTIVE | STALE
audioflow restore   # RESTORE SUCCESS | RESTORE FAILED
audioflow cleanup   # borra toda regla, log y cambio de registro que quede
.\tools\emergency-restore.ps1   # restaurar sin la UI
.\tools\cleanup.ps1             # limpiar sin la UI
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

## Backends de routing

AudioFlow enruta mediante una capa de backends intercambiables
(`AudioFlow.Routing`):

- **Policy Endpoint** — fija el endpoint persistido de la app. Sin duplicación;
  aplica al reiniciar el stream. Disponible hoy.
- **Virtual Endpoint** — captura el loopback de un endpoint virtual y lo
  renderiza al destino. Live y sin duplicación, pero requiere un endpoint de
  audio virtual instalado; si no, reporta `BLOCKED`.

Ver [docs/ROUTING-BACKENDS.md](docs/ROUTING-BACKENDS.md) y
[docs/VIRTUAL-ENDPOINT-INTEGRATION.md](docs/VIRTUAL-ENDPOINT-INTEGRATION.md).

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

## Contribuir y hacer fork

**Tu feedback es lo que hace mejor a AudioFlow.** 🚀

1. **Pruébalo** — descarga el último [Release](https://github.com/JavierparraDev/audioflow/releases)
   o compílalo desde el código, y úsalo con tus dispositivos reales.
2. **Haz fork** — pulsa **Fork**, crea una rama y hazlo tuyo. Todo el hardware,
   los stacks de audio y los casos de uso son distintos a los nuestros.
3. **Comparte mejoras** — abre un PR con tu fix, nuevo backend, perfil de
   dispositivo o traducción. Los PR pequeños y enfocados son más fáciles de revisar.
4. **Reporta** — ¿un dispositivo o app no enruta como esperabas? Abre un
   [issue](https://github.com/JavierparraDev/audioflow/issues) con tu build de
   Windows, dispositivos y pasos para reproducirlo.

Mantén los textos de UI localizados (agrega claves en `Strings.resx` y
`Strings.es.resx`) y añade tests para los cambios. Lee
[CONTRIBUTING.md](CONTRIBUTING.md) para la guía completa. Al participar aceptas
nuestro [Código de Conducta](CODE_OF_CONDUCT.md); reporta vulnerabilidades de
forma privada siguiendo [SECURITY.md](SECURITY.md).

> **Haz fork, experimenta y devuelve lo que funcione.** Ya sea un nuevo backend
> de routing, un fix para un dispositivo o una mejor UI, las contribuciones son
> bienvenidas.

## Licencia

[MIT](LICENSE) - Copyright (c) 2026 Javier Parra.

Componentes de terceros: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Privacidad

AudioFlow no recopila contraseñas, archivos personales, historial ni datos
privados. Solo accede a dispositivos de audio, sesiones de audio e información
de procesos.
