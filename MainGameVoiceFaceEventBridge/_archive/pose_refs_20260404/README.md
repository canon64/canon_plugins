# MainGameVoiceFaceEventBridge

## Overview

This plugin is the main voice/event bridge for KKS main game flow.
It receives external commands and applies:

- voice playback
- face control
- text-triggered actions (`response_text` -> coord/clothes parsing)

Primary transport is local named pipe: `kks_voice_face_events`.

## Files

- `MainGameVoiceFaceEventBridge.dll`
- `VoiceFaceEventBridgeSettings.json`
- `pose_list.json` (pose name references)

## human_2_KKS_pipeline integration

`human_2_KKS_pipeline` sends commands through `send_voice_face_event.ps1`.

Default route:
- NamedPipe: `kks_voice_face_events`

Optional route:
- HTTP bridge mode (host/port/endpoint from sender settings)

## Basic command types

- `{"type":"speak", ...}` : play voice/audio and optional face control
- `{"type":"stop"}` : stop current external playback
- `{"type":"response_text","text":"...","delaySeconds":...}` : parse text and schedule coord/clothes actions

## Notes

- Reload settings by Ctrl+R (if enabled in settings).
- Main config is `VoiceFaceEventBridgeSettings.json`.
