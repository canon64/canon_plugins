# SimpleAfterimage

## Overview
- Character afterimage effect plugin for `KoikatsuSunshine` and `KoikatsuSunshine_VR`.
- Captures character-only frames and renders trailing ghost images.

## What It Does
- Adds residual image trails to characters
- Lets you tune fade frames, capture interval, tint, alpha scale, and fade curve
- Supports preset storage
- Supports beat-linked fade range options

## Target Processes
- `KoikatsuSunshine`
- `KoikatsuSunshine_VR`

## Main Files
- `SimpleAfterimage.dll`
- `config.json`
- `SimpleAfterimage.log`

## Main Settings
- fade frames
- max slots
- capture interval
- tint RGBA
- alpha scale
- fade curve
- optional preset actions

## Notes
- Strong settings can easily wash out the image.
- Start from generated config values and tune gradually.
