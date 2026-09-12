# AudioFlow — Arquitectura

## 1. Modelo de audio de Windows

```
Aplicación
  │  crea un stream WASAPI (IAudioClient::Initialize)
  ▼
Audio Session            (IAudioSessionControl2, atada a UN endpoint)
  ▼
Audio Engine (audiodg)   (mezcla por sesión)
  ▼
Audio Endpoint (IMMDevice, eRender)
  ▼
Dispositivo físico
```

Hechos verificados en la documentación oficial de Microsoft:

- Una **Audio Session** se crea cuando una app abre un stream en un endpoint.
  Su `ProcessId`, `SessionIdentifier` e `SessionInstanceIdentifier` se obtienen
  con `IAudioSessionControl2` (Windows 7+).
- El **volumen/mute por app** se controla con `ISimpleAudioVolume`.
- El **estado** (`Active`, `Inactive`, `Expired`) viene de
  `IAudioSessionControl::GetState`.
- **No existe** un método para mover una sesión existente a otro endpoint.
  La sesión queda ligada al endpoint donde se creó.
- El dispositivo de una sesión se deduce de **qué endpoint** se usó para
  enumerar su `IAudioSessionManager2`.
- Las sesiones nuevas se detectan con
  `IAudioSessionManager2::RegisterSessionNotification`. **No hay callback de
  destrucción**: se detecta con `GetState() == Expired` o sondeo ligero.

## 2. Capas

```
AudioFlow.Core
  ├── AudioDeviceManager      Enumeración de endpoints (IMMDeviceEnumerator)
  ├── AudioSessionManager     Enumeración de sesiones por endpoint
  ├── AudioSessionMonitor     Notificaciones de sesión + refresco de estado
  ├── AudioRoutingManager     Aplicación de reglas (Fase 5)
  └── WindowsAudio            Interop COM/P-Invoke y PolicyConfigFactory

AudioFlow.Applications
  ├── ProcessManager          PID → nombre, ruta, AUMID
  └── ApplicationIdentifier   Identidad estable de una app

AudioFlow.Rules
  ├── RuleEngine              IF app tiene regla THEN endpoint ELSE default
  └── RuleStorage             Persistencia JSON

AudioFlow.Models
  AudioDevice / AudioSessionInfo / AudioRule / AudioRuleSet
```

## 3. Modelos

```csharp
AudioDevice       { Id, FriendlyName, State, IsDefault, DataFlow, ... }
AudioSessionInfo  { SessionIdentifier, ProcessId, ProcessName, ProcessPath,
                    DisplayName, DeviceId, State, Volume, IsMuted }
AudioRule         { RuleId, ApplicationIdentifier, ApplicationName,
                    OutputDeviceId, Enabled }
AudioRuleSet      { Rules[], DefaultOutputDeviceId, AudioLockEnabled, ... }
```

## 4. Flujo

```
Sesión detectada
   → ApplicationIdentifier (ruta + nombre + hash / AUMID)
   → RuleEngine.Lookup
   → endpoint destino (regla explícita o default)
   → AudioRoutingManager.Apply
```

## 5. Persistencia

`%APPDATA%\AudioFlow\rules.json`

```json
{
  "rules": [
    { "ruleId": "...", "applicationIdentifier": "spotify.exe",
      "applicationName": "Spotify", "outputDeviceId": "{0.0.0...}",
      "enabled": true }
  ],
  "defaultOutputDeviceId": "{0.0.1...}",
  "audioLockEnabled": true
}
```

## 6. Decisiones

- **C#/.NET 8 + NAudio** por velocidad de desarrollo y envoltura de WASAPI.
- **Sin driver de audio** para el MVP.
- **Routing híbrido**: `IAudioPolicyConfigFactory` (interno) y, como evolución,
  Process Loopback (oficial).
- **Sin privilegios de administrador**.
- **Sin recolección de datos personales**.

## 7. Solución completa

```
src/
├── AudioFlow.Models          Modelos de dominio (net8.0)
├── AudioFlow.Applications    ProcessManager, ApplicationIdentifier
├── AudioFlow.Core            Dispositivos, sesiones, routing, verificación
├── AudioFlow.Rules           RuleEngine, RuleStorage (JSON atómico)
├── AudioFlow.ProcessLoopback EXPERIMENTAL (API oficial, aislado)
├── AudioFlow.Console         CLI de diagnóstico
└── AudioFlow.UI              WPF + localización (EN/ES)
tests/
├── AudioFlow.Tests           Unitarios (xUnit)
└── AudioFlow.Windows.Tests   Integración Windows ([WindowsFact] -> skip)
```

### Core

- `AudioDeviceManager` - `IMMDeviceEnumerator` + notificaciones de endpoint.
- `AudioSessionManager` - `IAudioSessionManager2` / `IAudioSessionControl2`.
- `AudioSessionMonitor` - hilo MTA, eventos de sesión, refresco ligero.
- `AudioRoutingManager` - aplica y verifica el endpoint persistido.
- `AudioOutputVerifier` - mide el nivel real de audio por endpoint.
- `WindowsAudio/AudioPolicyConfig` - interop de `IAudioPolicyConfigFactory`.

### UI

- `MainViewModel` orquesta dispositivos, sesiones, reglas, Audio Lock y routing.
- Vistas por pestaña con navegación lateral (Dashboard, Applications, Rules,
  Devices, Diagnostics, Settings).
- `Localization/` - `Loc`, `LocalizationSource`, `{loc:Tr}`; recursos
  `Strings.resx` (EN, por defecto) y `Strings.es.resx` (ES).

### Verificación

`AudioOutputVerifier` es la fuente de verdad objetiva: un retorno de API correcto
**no** demuestra que el audio llegue al dispositivo. Ver
[TEST-REPORT.md](TEST-REPORT.md).
