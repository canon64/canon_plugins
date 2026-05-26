# MainGameHScenePoseInjector — IMPL_PLAN

## 目的
本編HScene中に、キャラメイクのポーズ（立ち/腰に手/内股 等）を女キャラに適用できるようにする。
HScene本体には手を入れず、Animator上書き方式で被せる。

## ユーザー要件（合意済み）
1. HSceneメニューに「ポーズ」というアイコン的UI要素を追加（オナニーの上あたり）
2. クリックでポーズ一覧パネルが開く
3. 選択するとそのポーズで女キャラが固まる
4. HSceneシステムは放置（ボイス・表情・物理・着脱は生きる、リアクションモーション・イキ・挿入は死ぬ）
5. 体位変更ボタンを押せば HScene 側が ChangeAnimator 呼ぶので自動復帰
6. 「解除」ボタンも用意

## 重要な技術的判明事項
- HScene の mode enum (`HFlag.EMode`) には `aibu / houshi / sonyu / masturbation / peeping / lesbian / houshi3P / sonyu3P / houshi3PDark / sonyu3PDark` の10個が固定。新規modeは追加不可
- HSpriteUI への新ボタン inject は menuMain (`HSceneSpriteCategory`) の親 Transform に追加する必要があるが、初期化タイミングと依存が多い
- **V1 方針**: HSpriteUIへのボタン inject はせず、**IMGUI OnGUI ウィンドウ＋ホットキー** で実装する。アイコン化はV2以降
- 「ポーズ」状態は **プラグイン内の bool フラグ** で管理する（HSceneのstate machine には触らない）

## 必要材料（DB / analysis で調査）

### 材料1: HScene 中の女ChaControl 取得経路
- 取得先: `HSceneProc.flags.lstFemale[0]` または `lstFemale[N]`
- 参考: `F:/kks/work/manual/plugin_modding/03_hscene_modding/04_input_and_position_change.md:62-71`
- メモリ: `F:/kks/test_plugin/StudioVoicePlugin/VOICE_SYSTEM_ANALYSIS.md` 参照

### 材料2: Animator 上書きAPI
- `chaCtrl.LoadAnimation(bundleName, assetName)` → `animBody.runtimeAnimatorController` に代入
- `chaCtrl.AnimPlay(stateName)` → `animBody.Play(stateName)`
- 参考: `F:/kks/work/analysis/ilspy_full_20260306_1/ChaControl.cs:669-691`

### 材料3: 元コントローラ保存・復帰
- 保存: `var saved = chaCtrl.animBody.runtimeAnimatorController; var savedState = chaCtrl.animBody.GetCurrentAnimatorStateInfo(0);`
- 復帰: `chaCtrl.animBody.runtimeAnimatorController = saved; chaCtrl.animBody.Play(savedState.shortNameHash, 0, savedState.normalizedTime);`
- 注: HScene側がChangeAnimator呼んだら自動的に上書きされる（解除と同義）

### 材料4: ポーズ一覧データ取得
- 出所: `custom/customscenelist/*.unity3d` 内の Excel "cus_pose"
- 列構成: ID, Bundle, Asset, Name, State, useMale(default true), useFemale(default true)
- ロード方法: `CommonLib.GetAssetBundleNameListFromPath("custom/customscenelist/", true)` → 全 unity3d を `AssetBundleManager.LoadAllAsset(file, typeof(ExcelData))` で開き、`excelData.name == "cus_pose"` のもののパラメータ行を走査
- 参考: `F:/kks/work/analysis/ilspy_full_20260306_1/ChaCustom/CustomControl.cs:195-274`
- 名前ローカライズは別シート（cus_pose_name 等）にあるが、V1 では Bundle+Asset+State から擬似名生成で済ませる

### 材料5: HScene検知
- `HSceneProc` シングルトン or instance を探す
- `[BepInProcess("KoikatsuSunshine")]`（本編H、VRは別途）
- HSceneProc.Awake / OnDestroy にHarmonyパッチして自プラグインの enabled/disabled を切替

### 材料6: トグルキー
- F4（既存プラグインと競合確認後決定）
- 現状確認: `F:/kks/work/docs/keybindings.md` に既存キー一覧
- 設定可能（JSON）

### 材料7: 出力ログ・設定
- ログ: `Path.GetDirectoryName(Info.Location)\MainGameHScenePoseInjector.log`
- 設定: `MainGameHScenePoseInjectorSettings.json`

## ファイル構成（予定）
```
MainGameHScenePoseInjector/
├── MainGameHScenePoseInjector.csproj
├── Plugin.cs           — エントリ、HScene検知、Update、グローバル状態
├── Plugin.PoseData.cs  — cus_pose Excelロード、PosePattern一覧キャッシュ
├── Plugin.Apply.cs     — Animator上書き/復帰
├── Plugin.UI.cs        — OnGUI（ホットキー、ポーズ一覧パネル、解除ボタン）
├── Plugin.Settings.cs  — JSON設定（ToggleUiKey, WindowX/Y/W/H, 最後に選択したポーズID等）
└── IMPL_PLAN.md
```

## 既知のリスク・既受容項目
- リアクションモーション死ぬ（ユーザー承知）
- イキ/挿入は触らない方が安全（ユーザー承知）
- 首/視線追従はアニメ名キーで dic 引きなので止まる可能性（ユーザー承知）
- セッション内詰みの可能性低だが残る → クイックセーブ運用ルール

## ビルド・配置
- net472, KKSDir=F:\kks
- 出力: `bin/Release/net472/MainGameHScenePoseInjector.dll`
- 配置: `F:/kks/BepInEx/plugins/canon_plugins/MainGameHScenePoseInjector/`

## V2 以降検討
- HSpriteUIへのアイコン inject（オナニーの上あたり）
- ボーンIK固定オフセット
- ポーズ毎の表情/視線プリセット連動
- VR対応
