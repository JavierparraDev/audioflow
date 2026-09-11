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
