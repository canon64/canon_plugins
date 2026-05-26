# MainGamePregnancyPlusBridge UI 並び設計案

## 目的
- `Enabled` を最上段に固定し、上から読むだけで運用できるUIにする。
- 関連項目（Phase / Size / 保存系）を分断せず、近接配置する。
- 「調整する場所」と「状態を見る場所」を分けて、誤操作を減らす。

## 現状課題
- `BellyBoko` 内で `Phase` と `Size` と `Profile` が混在している。
- `General.Enabled` が中段/下段に出るため、全体ON/OFFが見つけにくい。
- `Min/Max` ペアが離れて出るため、値の関係が把握しづらい。

## 並び設計（上から下）

### 1. プラグイン全体
1. `General.Enabled`
2. `General.ApplyIntervalSeconds`

### 2. BellyBoko 運用モード
1. `BellyBoko.Enabled`
2. `BellyBoko.EditMode`
3. `BellyBoko.AutoLoadProfileOnContextChange`
4. `BellyBoko.ProfileEnabled`

### 3. BellyBoko タイムライン編集
1. `BellyBoko.TimelineEditor`（シークバー本体）
2. `BellyBoko.ForwardMinPhase`
3. `BellyBoko.MinHoldWidth`
4. `BellyBoko.MaxPhase`
5. `BellyBoko.ReturnMinPhase`
6. `BellyBoko.TimelineDisplayOffset`
7. `BellyBoko.EaseUp`
8. `BellyBoko.EaseDown`

### 4. BellyBoko 腹サイズ（必ず隣接）
1. `BellyBoko.MinInflationSize`
2. `BellyBoko.MaxInflationSize`
3. `BellyBoko.PresetSlotForMax`

### 5. BellyBoko キャプチャ
1. `BellyBoko.CaptureEnabled`
2. `BellyBoko.CaptureMaxKey`
3. `BellyBoko.CaptureMinKey`
4. `BellyBoko.AutoReturnMin`

### 6. BellyBoko プロファイルI/O
1. `BellyBoko.SaveProfileNow`
2. `BellyBoko.LoadProfileNow`

### 7. BellyBoko 状態表示（読み取り専用）
1. `BellyBoko.CurrentContext`
2. `BellyBoko.CurrentStrength`

### 8. Preset（手動運用）
1. `Preset.SelectedSlot`
2. `Preset.PresetName`
3. `Preset.SavePresetNow`
4. `Preset.LoadPresetNow`

### 9. PregnancyPlusData（手動微調整）
1. `PregnancyPlusData.GameplayEnabled`
2. `PregnancyPlusData.InflationSize`
3. `PregnancyPlusData.InflationMoveY`
4. `PregnancyPlusData.InflationMoveZ`
5. `PregnancyPlusData.InflationStretchX`
6. `PregnancyPlusData.InflationStretchY`
7. `PregnancyPlusData.InflationShiftY`
8. `PregnancyPlusData.InflationShiftZ`
9. `PregnancyPlusData.InflationTaperY`
10. `PregnancyPlusData.InflationTaperZ`
11. `PregnancyPlusData.InflationMultiplier`
12. `PregnancyPlusData.InflationClothOffset`
13. `PregnancyPlusData.InflationFatFold`
14. `PregnancyPlusData.InflationFatFoldHeight`
15. `PregnancyPlusData.InflationFatFoldGap`
16. `PregnancyPlusData.InflationRoundness`
17. `PregnancyPlusData.InflationDrop`
18. `PregnancyPlusData.ClothingOffsetVersion`
19. `PregnancyPlusData.PluginVersion`

### 10. Logging
1. `Logging.EnableLog`
2. `Logging.VerboseLog`

## 実装ルール（順序を崩さないため）
- 全項目に `ConfigurationManagerAttributes.Order` を付与して固定順にする。
- `Enabled` 系は各セクションの先頭に置く。
- `Min/Max` ペアは必ず連続配置する。
- `Save/Load` はペアで隣接配置する。

## セクション順の固定案（Generalを最上段にする）
- 方式A（互換優先）: セクション名は現状維持。項目順のみ固定。
- 方式B（見た目優先）: セクション名を `00.General` / `10.BellyBoko` / `20.Preset` / `30.PregnancyPlusData` / `90.Logging` にする。
- 推奨: 方式B。理由は「Generalが確実に最上段になる」ため。
