# StudioCharaStateLogger

Studio専用 キャラ状態フルダンプ診断プラグイン（汗/ツヤ調査用）。

## 目的
スタジオのUI（特に「その他」タブの skin スライダー = 汗レベルや、液体・頬・涙など）で操作した瞬間に、関連する全状態をログに出力する。
- `fileStatus.skinTuyaRate`（汗・肌ツヤ率）
- `custom.body.skinGlossPower`（ベース肌ツヤ）
- `siruLv[]`（全SiruParts、SiruKao含む）
- `hohoAkaRate` / `tearsLv`
- 実際のマテリアルに設定されている `_SpecularPower` / `_Sweat` / `_Wet` / `_liquid*` などの値

これで「スタジオの汗UIが実際に何を触っているか」を特定できる。

## 使い方
1. ビルド or デプロイ済みDLLを `BepInEx/plugins/canon_plugins/StudioCharaStateLogger/` に置く。
2. CharaStudioを起動。
3. ログは以下の2箇所に出る：
   - BepInExコンソール / ログ
   - `BepInEx/plugins/canon_plugins/StudioCharaStateLogger/StudioCharaStateLogger.log`（このファイルを読む）

## トリガー
- **自動検知（推奨）**: ポーリングで `skinTuyaRate` / `siruLv` / `hohoAkaRate` / `tearsLv` / ベースgloss の変化を検知したら即フルダンプ（Configでオンオフ・間隔調整可）。
- **UI直接パッチ**: MPCharCtrlのスライダー/ボタン（skin, cheek, liquid/siru, tears など）をHarmonyで捕捉。操作した瞬間にダンプ。
- **手動ダンプ**: デフォルト **F10** 押下でシーン中の全ChaControlの状態を強制ダンプ。
- Configの `Dump Hotkey` で変更可。

## Config（ConfigurationManager対応）
- General/Enabled
- General/Dump Hotkey (KeyboardShortcut)
- Log/Verbose Materials : true にするとマテリアルプロパティを全種類全部吐く（デフォルトfalseでも汗関連の主要なものは出す）
- Log/Log On Value Delta
- Log/Poll Interval Sec

## ログ例（抜粋）
```
[12:34:56.789] === CHARA STATE DUMP reason=studio-ui-skin cha=...
  fileStatus.skinTuyaRate=0.423
  ...
  custom.body.skinGlossPower(base)=0.300
  MAT renderer=cf_J_Skin_mat mat=... _SpecularPower=0.652 _Sweat=0.000 ...
```

## 既知の注意
- スタジオの "skin" スライダー実装は decomp ではハンドラが薄かったため、delta poll + 手動F10 が最も確実。
- 液体（siru）は見た目で汗っぽく見えるので両方ログに出る。
- 必要なら AstralMode のプローブ系と併用して往復検証に使える。

## ビルド
dotnet build -c Release （このディレクトリで）

生成物: bin/Release/net472/StudioCharaStateLogger.dll

## ライセンス/スコープ
secret_work 実験用。AstralMode の汗調査の前段階ツール。

## 2026-06 更新
- Additional Skin Effects mod (KKS_SkinEffects.dll) の "Additional skin effects > Sweat" (レベル0-3) をダンプ/デルタ検知するよう追加。
- KK_SkinEffects.SkinEffectsController.SweatLevel を反射で取得・ログ。
- anim>状態 パネルで Sweat 変更後 F10 や自動で "KK_SkinEffects.SweatLevel=xx" が出る。
- これによりスタジオ汗UIの実態 (overlay wet tex) を検証可能。
