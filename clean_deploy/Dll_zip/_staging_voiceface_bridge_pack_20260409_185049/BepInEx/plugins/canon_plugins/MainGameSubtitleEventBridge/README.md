# MainGameSubtitleEventBridge

## Overview

HTTP subtitle bridge for KKS main game.
This plugin accepts subtitle requests over HTTP and forwards them to `MainGameSubtitleCore` through `SubtitleApi`.

## Dependency

Hard dependency:
- `MainGameSubtitleCore` must be loaded first.

## Files

- `MainGameSubtitleEventBridge.dll`
- `SubtitleEventBridgeSettings.json`
- `chime/` (notification audio files)

## Default endpoint

- Host: `0.0.0.0`
- Port: `18766`
- Endpoint: `/subtitle-event`

## Request format (POST JSON)

Main fields:
- `text` (required)
- `hold_seconds` (optional)
- `backend` (optional)
- `display_mode` (optional)
- `speaker_gender` / `speaker` (optional)

## Notes

- Token auth can be enabled with `AuthToken` setting.
- Reload settings by Ctrl+R (if enabled).
