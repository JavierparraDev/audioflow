# AudioFlow — Viabilidad del routing por aplicación

## Pregunta central

¿Windows permite cambiar la salida de audio de una aplicación individual
mediante APIs?

**Respuesta: PARCIALMENTE.**

No existe una API **oficial y documentada** para mover una aplicación (o su
sesión viva) a otro endpoint. Hay cuatro caminos reales, con distintos
compromisos.

## Tabla comparativa

| Método | Oficial | Documentado | Driver | Admin | Latencia | Producción |
| ------ | ------- | ----------- | ------ | ----- | -------- | ---------- |
| `IAudioSessionManager2` + `ISimpleAudioVolume` | Sí | Sí | No | No | N/A | Sí — **solo detección** y volumen/mute |
| `IAudioPolicyConfigFactory` (CLSID `{2a59116d-6c4f-45e0-a74f-707e3fef9258}`) | **No** | **No** | No | No | N/A | Riesgoso — endpoint persistido por proceso |
| `IPolicyConfig` | **No** | **No** | No | No | N/A | Riesgoso — cambia el default **global** |
| **Process Loopback** (`AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS`) | **Sí** | **Sí** | No | No | Media | Sí, con matices |
| Driver de audio virtual | Sí (firmado) | Sí | **Sí** | Instalación | Baja | Caro (cert EV + WHQL) |

## Detalle

### 1. Detección — oficial
`IAudioSessionManager2` → `IAudioSessionEnumerator` → `IAudioSessionControl2`.
Permite PID, session id, estado, volumen y mute. **No enruta.**

### 2. `IAudioPolicyConfigFactory` — interno
Interfaz COM usada por *Configuración > Sistema > Sonido > Mezclador de
volumen > Opciones avanzadas* para asignar dispositivo de salida por app.
Fija el endpoint **persistido** para un `ProcessId` + `EDataFlow` + `ERole`.

- Efecto: se aplica cuando la app **reinicia su stream** de audio.
- **No** mueve audio en vivo.
- Interfaz **no documentada**; puede cambiar entre builds de Windows.
- Existen variantes (`...VariantForDownlevel`) para Windows 10.

### 3. `IPolicyConfig` — interno
Cambia el dispositivo **predeterminado del sistema**, no por app. No sirve para
el caso de uso de AudioFlow (Spotify en parlantes y el resto en audífonos).

### 4. Process Loopback — oficial
`ActivateAudioInterfaceAsync` con `AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS`
(`TargetProcessId` + `PROCESS_LOOPBACK_MODE`). Captura el audio de un proceso y
sus hijos, y se re-renderiza a otro endpoint.

- Requiere **Windows 10 build 20348+** / Windows 11.
- Hay que **mutear la sesión original** (`ISimpleAudioVolume`) para no duplicar.
- **No captura contenido protegido (DRM).**
- Añade latencia (decenas de ms).
- No requiere driver ni administrador.

### 5. Driver virtual
Control total y baja latencia, pero exige desarrollo de driver, firma EV y
WHQL. **Descartado para el MVP.**

## Estrategia de AudioFlow

1. **MVP (Fase 5A) — IMPLEMENTADO:** `IAudioPolicyConfigFactory` para asignar
   el endpoint persistido por app. Es la vía más simple hacia el comportamiento
   "Spotify → Parlantes". Se documenta el caveat de que la app debe reiniciar su
   stream. La escritura se verifica leyendo el valor persistido de vuelta.
2. **Evolución (Fase 5B):** Process Loopback oficial para re-enrutado en vivo.
3. **Futuro:** driver virtual firmado si se necesita routing perfecto.

### Detalles de implementación de la Fase 5A

- Activación: `RoGetActivationFactory("Windows.Media.Internal.AudioPolicyConfig")`
  (`combase.dll`) con los IID conocidos (21H2, Downlevel, 1709).
- Métodos invocados por índice de vtable: Set = 25, Get = 26, Clear = 27
  (IUnknown 3 + IInspectable 3 + 19 métodos internos).
- El `deviceId` **no** es el ID crudo del endpoint: Windows espera la ruta de
  interfaz SWD:
  `\\?\SWD#MMDEVAPI#{0.0.0...}.{guid}#{e6327cad-dcec-4949-ae8a-991e976a79d2}`.
- Se escribe para los roles `eMultimedia` y `eConsole`, igual que Ajustes.
- Resultado `E_INVALIDARG` (0x80070057) = "PROCESS_NO_AUDIO": el proceso no
  tiene audio activo, la llamada no aplica nada. Se reporta como fallo.

## Audio Lock

No es una API distinta: es una política del motor de reglas.

```
PARLANTES: bloqueado
  Permitidas: Spotify
  Resto: BLOQUEADAS → destino automático Audífonos
```

Se implementa con una lista blanca por dispositivo. No se declara
"implementado" hasta que el routing real esté verificado.

## Compatibilidad por tipo de aplicación

| Tipo | Detección | Routing persistido | Loopback |
| ---- | --------- | ------------------ | -------- |
| Win32 clásico (Spotify, Discord) | Sí | Sí (reinicio de stream) | Sí |
| Electron (Discord, VS Code) | Sí | Sí | Sí |
| Navegadores (Chrome) | Sí | Sí | Sí |
| Microsoft Store / UWP | Sí | Sí (AUMID) | Sí |
| Juegos (modo compartido) | Sí | Sí | Sí |
| Juegos (modo exclusivo) | Limitada | No | No |
| Contenido DRM | Sí | Sí | No (protegido) |
