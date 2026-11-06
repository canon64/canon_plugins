# MainGameSubtitleCore

## Overview

Core subtitle system for KKS main game.
Provides subtitle queue/display and exposes `SubtitleApi` used by bridge plugins.

## Role in the 3-plugin set

- `MainGameVoiceFaceEventBridge`: voice/event bridge
- `MainGameSubtitleEventBridge`: HTTP subtitle receiver
- `MainGameSubtitleCore` (this plugin): subtitle display core

## Files

- `MainGameSubtitleCore.dll`
- `SubtitleCoreSettings.json`
- `SubtitleInputPanelState.json` (runtime UI state)

## Main features

- subtitle display backend control (`Auto / InformationUI / Overlay`)
- subtitle queue handling
- optional input panel UI
- optional forwarding from input panel to external text receiver

## Forwarding default (input panel -> external receiver)

- Host: `127.0.0.1`
- Port: `18767`
- Endpoint: `/manual-text`

## Notes

- Main config is `SubtitleCoreSettings.json`.
- Reload settings by Ctrl+R (if enabled).
