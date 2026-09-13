# AudioFlow — Reddit launch post

Ready-to-publish material to introduce AudioFlow to developer communities and
recruit contributors. Pick a title, copy the body, and post it. There is an
English and a Spanish version.

- Repository: https://github.com/JavierparraDev/audioflow
- Contributing: https://github.com/JavierparraDev/audioflow/blob/main/CONTRIBUTING.md
- Roadmap: https://github.com/JavierparraDev/audioflow/blob/main/docs/ROADMAP.md
- Limitations: https://github.com/JavierparraDev/audioflow/blob/main/docs/LIMITATIONS.md
- Good first issues: https://github.com/JavierparraDev/audioflow/labels/good%20first%20issue

---

## Suggested titles (English)

- **AudioFlow: an open-source per-app audio router for Windows (C#/.NET 8) — beta, contributors wanted**
- **I built an open-source per-application audio router for Windows. Help me make it 100% functional**
- **Show r/csharp: AudioFlow, per-app audio routing for Windows, open source and looking for contributors**

## Suggested titles (Spanish)

- **AudioFlow: enrutador de audio por aplicación para Windows, open source (C#/.NET 8) — beta, busco colaboradores**
- **Hice un enrutador de audio por app para Windows y quiero que la comunidad lo lleve al 100%**

## Recommended subreddits

| Subreddit | Notes |
|---|---|
| r/csharp | Focus on the .NET 8 / WPF / COM interop side. |
| r/dotnet | Same, more general. |
| r/opensource | The open-source / help-wanted angle. |
| r/programming | Broad; lead with the technical challenge. |
| r/Windows11 | End-user angle; be honest about beta limits. |
| r/devsarg, r/programacion | Spanish version. |

Suggested flair: `Show and Tell`, `Project`, or `Open Source`.

---

## English post

**Title:** AudioFlow: an open-source per-app audio router for Windows (C#/.NET 8) — beta, contributors wanted

**Body:**

Hi everyone,

I've been building **AudioFlow**, an open-source, per-application audio router
for Windows. The goal is simple to describe and hard to get right: decide
**where each app plays its audio** — keep Spotify on the speakers while Discord,
YouTube and games stay on the headphones, at the same time, without unplugging
anything.

Repo: **https://github.com/JavierparraDev/audioflow**

It is in **beta** and I'm looking for developers to help turn it into a complete,
100% functional open-source project. I'd rather be upfront about what works and
what doesn't than oversell it.

### What works today

- Native **Windows 10/11** app: **.NET 8 + WPF**, dark UI, **English + Spanish**.
- Detects output devices (speakers, USB, Bluetooth, monitors, virtual) and live
  WASAPI audio sessions.
- Stable app identification (exe name + path hash + AUMID for Store apps).
- Per-app rules and a global default output, plus a partial **Audio Lock**.
- **Session-only and ephemeral by design:** while AudioFlow is open the rules may
  be active; when it closes, Windows goes back to normal and **nothing is left
  behind** — no rules file, no logs, no registry change. It snapshots the
  per-application audio registry before touching anything and restores it exactly,
  even for apps that are no longer running.
- Crash safety: a recovery marker + an independent **Session Guardian** that
  restores audio within seconds if the app dies.
- A CLI for diagnostics (`audioflow devices/sessions/plan/apply/verify/...`).
- Installer + portable ZIP + a self-updater with SHA-256 verification.
- **93 unit tests** + Windows integration tests, CI on Linux and Windows.

### What does *not* work yet (the honest part)

This is why I need help:

1. **Routing is not live.** AudioFlow uses the same undocumented internal API as
   *Settings → Sound → Volume mixer* (`IAudioPolicyConfigFactory`). It sets the
   **persisted** output endpoint, applied when an app (re)initializes its audio
   stream. It does not move a currently playing stream.
2. **Duplication-free live routing is blocked on a virtual audio endpoint.** A
   real Process Loopback capture → WASAPI render pipeline is implemented and
   physically verified, but it duplicates audio, and muting the original session
   also silences the capture. To fix it properly we need a virtual endpoint (null
   sink / virtual cable or our own driver) to capture from and re-render.
3. **Audio Lock is partial**, not a hard block (exclusive-mode and
   self-routing apps can bypass it).
4. Some physical tests (device disconnect, logoff/shutdown) are implemented but
   not yet executed on real hardware.

The full, blunt list is in
[docs/LIMITATIONS.md](https://github.com/JavierparraDev/audioflow/blob/main/docs/LIMITATIONS.md).

### Where I need help (in priority order)

1. **Virtual audio endpoint** — integrate a virtual cable/driver (or build one)
   so we can do live, duplication-free routing. This is *the* blocker.
2. **Physical testing on real hardware** — Realtek, USB DACs, Bluetooth,
   Voicemeeter, HyperX, gaming headsets. Tell us what works and what doesn't.
3. **Routing backends** — the backend layer is swappable
   (`IAudioRoutingBackend`); add new strategies.
4. **COM/registry interop review** — the code that touches
   `PolicyConfig\PropertyStore` deserves careful eyes.
5. **UI/UX and translations** — more languages, accessibility, polish.
6. **Code signing and release automation** for trustworthy downloads.

Good first issues are labelled here:
https://github.com/JavierparraDev/audioflow/labels/good%20first%20issue

### How to contribute

- Read [CONTRIBUTING.md](https://github.com/JavierparraDev/audioflow/blob/main/CONTRIBUTING.md).
- Try it: download a build from
  [Releases](https://github.com/JavierparraDev/audioflow/releases) or build from
  source (Windows + .NET 8 SDK):
  ```powershell
  git clone https://github.com/JavierparraDev/audioflow.git
  cd audioflow
  ./install.ps1 -Launch
  ```
- Fork it, pick an issue, open a PR. Small, focused PRs are easiest to review.
- Found a device that misbehaves? Open an issue with your Windows build and
  hardware — that alone is a huge help.

**MIT licensed.** If you've ever fought with Windows audio routing, I'd love
your feedback, your tests and your PRs. Let's make it work for everyone.

Thanks!

---

## Spanish post

**Título:** AudioFlow: enrutador de audio por aplicación para Windows, open source (C#/.NET 8) — beta, busco colaboradores

**Cuerpo:**

Hola a todos,

Vengo construyendo **AudioFlow**, un enrutador de audio **por aplicación** para
Windows, open source. La idea es fácil de describir y difícil de lograr bien:
decidir **dónde suena cada app** — dejar Spotify en los parlantes mientras
Discord, YouTube y los juegos suenan en los audífonos, al mismo tiempo y sin
desconectar nada.

Repo: **https://github.com/JavierparraDev/audioflow**

Está en **beta** y busco desarrolladores para convertirlo en un proyecto open
source completo y 100% funcional. Prefiero ser honesto sobre qué funciona y qué
no, en lugar de vender humo.

### Lo que ya funciona

- App nativa de **Windows 10/11**: **.NET 8 + WPF**, UI oscura, **inglés y español**.
- Detecta dispositivos de salida (parlantes, USB, Bluetooth, monitores, virtuales)
  y sesiones de audio WASAPI en vivo.
- Identificación estable de apps (nombre del exe + hash de ruta + AUMID).
- Reglas por app y salida por defecto global, más un **Bloqueo de audio** parcial.
- **Solo por sesión y efímero por diseño:** mientras AudioFlow está abierto las
  reglas pueden estar activas; al cerrarse, Windows vuelve a la normalidad y **no
  queda nada** — ni archivo de reglas, ni logs, ni cambios en el registro. Toma
  una foto del registro de audio por app antes de tocar nada y lo restaura
  exactamente, incluso para apps que ya no están corriendo.
- Seguridad ante caídas: marcador de recuperación + **Session Guardian**
  independiente que restaura el audio en segundos si la app se cae.
- CLI de diagnóstico (`audioflow devices/sessions/plan/apply/verify/...`).
- Instalador + ZIP portable + auto-actualizador con verificación SHA-256.
- **93 tests unitarios** + tests de integración en Windows, CI en Linux y Windows.

### Lo que **no** funciona todavía (la parte honesta)

Por esto necesito ayuda:

1. **El routing no es en vivo.** AudioFlow usa la misma API interna no
   documentada que *Configuración → Sonido → Mezclador de volumen*
   (`IAudioPolicyConfigFactory`). Fija el endpoint **persistido**, que se aplica
   cuando la app (re)inicia su stream de audio. No mueve un stream en vivo.
2. **El routing en vivo sin duplicación está bloqueado por un endpoint de audio
   virtual.** El pipeline real de Process Loopback → WASAPI render está
   implementado y verificado físicamente, pero duplica el audio, y silenciar la
   sesión original también silencia la captura. Para resolverlo hace falta un
   endpoint virtual (null sink / cable virtual o nuestro propio driver).
3. **El Bloqueo de audio es parcial**, no un bloqueo duro (apps en modo exclusivo
   o que eligen su propio endpoint pueden saltárselo).
4. Algunas pruebas físicas (desconexión de dispositivo, apagado/cierre de sesión)
   están implementadas pero aún no ejecutadas en hardware real.

La lista completa y sin filtros está en
[docs/LIMITATIONS.md](https://github.com/JavierparraDev/audioflow/blob/main/docs/LIMITATIONS.md).

### Dónde necesito ayuda (por prioridad)

1. **Endpoint de audio virtual** — integrar un cable/driver virtual (o crear uno)
   para lograr routing en vivo sin duplicación. Este es *el* bloqueante.
2. **Pruebas físicas en hardware real** — Realtek, DACs USB, Bluetooth,
   Voicemeeter, HyperX, headsets gamer. Cuéntanos qué funciona y qué no.
3. **Backends de routing** — la capa es intercambiable
   (`IAudioRoutingBackend`); agrega nuevas estrategias.
4. **Revisión de interop COM/registro** — el código que toca
   `PolicyConfig\PropertyStore` merece ojos expertos.
5. **UI/UX y traducciones** — más idiomas, accesibilidad, pulido.
6. **Firma de código y automatización de releases** para descargas confiables.

Los "good first issues" están etiquetados aquí:
https://github.com/JavierparraDev/audioflow/labels/good%20first%20issue

### Cómo contribuir

- Lee [CONTRIBUTING.md](https://github.com/JavierparraDev/audioflow/blob/main/CONTRIBUTING.md).
- Pruébalo: descarga un build desde
  [Releases](https://github.com/JavierparraDev/audioflow/releases) o compila
  desde el código (Windows + .NET 8 SDK):
  ```powershell
  git clone https://github.com/JavierparraDev/audioflow.git
  cd audioflow
  ./install.ps1 -Launch
  ```
- Haz fork, toma un issue y abre un PR. Los PR pequeños y enfocados son más
  fáciles de revisar.
- ¿Un dispositivo se comporta mal? Abre un issue con tu build de Windows y tu
  hardware — eso solo ya es una gran ayuda.

**Licencia MIT.** Si alguna vez peleaste con el audio de Windows, me encantaría
tu feedback, tus pruebas y tus PRs. Hagamos que funcione para todos.

¡Gracias!

---

## Posting checklist

- [ ] Create a `v0.2.0` GitHub Release so the download links work (tag `v0.2.0`).
- [ ] Pin the repository topics on GitHub: `windows`, `audio`, `csharp`, `dotnet`,
      `wpf`, `wasapi`, `audio-routing`, `naudio`, `open-source`.
- [ ] Add labels `good first issue` and `help wanted` to a few issues.
- [ ] Enable **Discussions** on the repository (linked from the issue chooser).
- [ ] Post at a developer-friendly time (weekday morning, US/EU hours).
- [ ] Reply to every comment for the first 24–48h.
- [ ] Cross-post the Spanish version to Spanish-speaking communities.
- [ ] Keep the tone honest: lead with the limitation, then ask for help.
