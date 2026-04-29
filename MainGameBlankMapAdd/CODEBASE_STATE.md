# CODEBASE_STATE: MainGameBlankMapAdd

- Updated: 2026-03-01
- Project: `F:/kks/work/plugins/MainGameBlankMapAdd/`
- Build: `dotnet build MainGameBlankMapAdd.csproj -c Release "-p:KKSDir=F:/kks"`
- Deploy: `F:/kks/BepInEx/plugins/MainGameBlankMapAdd/`

## 現在の役割
- Harmonyで本編マップ一覧へカスタムマップ1件を注入する。
- 既存マップ (`SourceMapNo`) を複製して `AddedMapNo` として公開する。
- 追加マップ読込時に、任意で描画を無効化してブランク化する（Renderer/Terrain/Light/Particle）。
- すべての挙動を `MapAddSettings.json` で調整可能。

## パッチポイント（現在）
- `BaseMap.LoadMapInfo` (Postfix): `MapInfo.Param` を追加。
- `BaseMap.Reserve` (Postfix): `no == AddedMapNo` のとき `mapRoot` をブランク化。
- `MapSelectMenuScene.Create` (Postfix): 診断ログのみ（修正未実装）。

## 主要ファイル
- `Plugin.cs`
- `PluginSettings.cs`
- `SettingsStore.cs`

---

## デコンパイル調査結果 (2026-03-01)

デコンパイル済みソース:
- `F:/kks/work/_decomp/MapThumbnailInfo.cs`
- `F:/kks/work/_decomp/MapSelectMenuScene.cs`

### MapThumbnailInfo の構造

```csharp
public class MapThumbnailInfo : ScriptableObject
{
    public class Param {
        public string Name;    // ボタン表示名
        public int ID;
        public string Bundle;  // サムネ画像アセットバンドル
        public string Asset;   // サムネ画像アセット名
        public Texture2D GetThumbnail() { ... }
    }
    public List<Param> param = new List<Param>();
}
```

### MapSelectMenuScene.Create → Start() のボタン生成フロー

```
Create(visibleType, resultType, infos, mapThumbnailInfoTable)
  → instance.infos = infos.OrderBy(x => x.Sort).ToArray()  ← Sort順でソート
  → instance.mapThumbnailInfoTable = mapThumbnailInfoTable

Start() [非同期]
  → infos を順に foreach
  → FreeH の場合: isFreeH=false はスキップ
  → No==9 / No==36 は特殊条件あり、それ以外は default → CreateThumbnail(info)

CreateThumbnail(info):
  → GetThumbnailInfo(info) で MapThumbnailInfo.Param を取得
  → param == null なら return null (ボタン作成スキップ)
  → Instantiate(_thumbnailButton, root)
  → thumbnailButton.Bind(param.GetThumbnail(), param.Name)  ← Name が表示名
  → thumbnailButton.gameObject.SetActive(true)

GetThumbnailInfo(info):
  → info.FindThumbnailID() で時刻帯に対応するサムネID取得
  → mapThumbnailInfoTable.TryGetValue(id, out value)
  → value を返す（null なら CreateThumbnail がスキップ）
```

### 原因の確定

ボタンは**作られている**。ただし Source マップと**完全に同じ見た目**。

`CloneMapParam` で `ThumbnailMorningID/DayTimeID/EveningID/NightID` を Source からそのままコピーしているため、`GetThumbnailInfo` が返すのは **Source の `MapThumbnailInfo.Param`**。結果：

- ボタン表示名 → Source の `param.Name`（同じ）
- サムネ画像 → Source の Bundle/Asset（同じ）

`AddedSort=1` で先頭に来ているが、Source マップと見た目が全く同一なので新しいマップだと識別できない。

診断ログの `thumbExists=True` は「Source のサムネ ID が テーブルに存在する」を確認しているだけで、専用エントリの有無を確認できていなかった。

---

## 実装済み修正 (2026-03-01)

### MapThumbnailInfo 注入
- `BaseMap.LoadMapThumbnailInfo` Postfix を追加
- Source のサムネ Bundle/Asset を流用し、`Name=AddedDisplayName` のエントリを `AddedThumbnailID`(=9000) で注入
- `InjectCustomMapInfo` で Added map の `ThumbnailMorningID=AddedThumbnailID` にセット
- `PluginSettings` に `AddedThumbnailID`（デフォルト: 9000）を追加

### DataContractJsonSerializer デフォルト値問題の修正
- 既存JSONに無いフィールドは `default(int)=0` になる（フィールド初期化子は無視される）
- `PluginSettings` に `[OnDeserialized]` を追加し `AddedThumbnailID <= 0` のとき 9000 に補正
- `MapAddSettings.json` にも `"AddedThumbnailID": 9000` を直接追記

### 動作確認済み
- ログ: `thumbnail injected id=9000 name=Blank Test bundle=map/thumbnail/00.unity3d asset=sp_sun_map_01_00`
- ログ: `map ui diagnose: thumbId=9000 thumbExists=True ownThumbInjected=True ThumbnailMorningID=9000`
- FreeH マップ選択画面に「Blank Test」ボタンが独立エントリとして表示されることを確認
- SourceMapNo=1（民宿）のサムネを流用しているため画像は同一。Sort=1 で先頭付近に表示

### Blankマップ動画ルーム実装（2026-03-01）
- `UnityEngine.VideoModule` 参照を追加し、`VideoPlayer` を利用可能にした。
- `BaseMap.Reserve` Postfix 内で `AddedMapNo` 判定時のみ動画ルーム生成を実行。
- `mapRoot` 配下に `__BlankMapVideoRoom` を作成し、床・天井・壁（4面/1面切替）を `Quad` で自動生成。
- 各面へ `VideoPlayer + RenderTexture` を接続し、動画をマテリアルへ描画する方式に変更。
- JSON設定に動画関連パラメータを追加：
  `EnableVideoRoom`, `VideoPath`, `UseSeparateSurfaceVideos`,
  `FloorVideoPath`, `WallVideoPath`, `CeilingVideoPath`,
  `VideoLoop`, `MuteVideoAudio`, `RoomWidth`, `RoomDepth`, `RoomHeight`, `CreateFourWalls`
- マップ切替時/プラグイン破棄時に動画ルームと `RenderTexture` を破棄するクリーンアップ処理を追加。
- `SettingsStore.LoadOrCreate()` でロード後に再保存するようにし、新規JSONキーを自動反映するようにした。
- `dotnet build`（Release, net472）成功、`F:/kks/BepInEx/plugins/MainGameBlankMapAdd/` へ再配置済み。

## 外部プラグイン向けAPI（2026-03-07追加）

`Plugin.Api.cs` を新規追加。外部プラグインが購読・呼び出し可能な入口。

### イベント
```csharp
Plugin.OnVideoLoaded  += (path) => { ... }; // 動画準備完了時
Plugin.OnVideoStarted += (path) => { ... }; // 再生開始時
Plugin.OnVideoEnded   += (path) => { ... }; // 終端到達時（loopPointReached）
Plugin.OnSettingsApplied += () => { ... };  // 設定保存時
```

### メソッド
```csharp
string path = Plugin.GetCurrentVideoPath(); // 現在の動画パス（null の場合あり）
```

### フック点（内部）
- `OnVideoPrepared()` → `FireOnVideoLoaded`（mainVideoPlayer のみ）
- `TryStartPendingVideosIfRoomReady()` → `FireOnVideoStarted`（mainVideoPlayer のみ）
- `TryAttachVideo()` → `player.loopPointReached += _ => FireOnVideoEnded`
- `SaveCurrentStateSnapshot()` → `FireOnSettingsApplied`

### 想定利用プラグイン
`MainGameVideoSuite`（構想中）が `OnVideoEnded` を購読して次トラックへ切り替えるなど。
詳細は `PLAN.md` 参照。

## 現状・残課題

- `VideoPath` の実ファイル配置が必要（相対パスはプラグインフォルダ基準）。
- `MainGameVideoSuite` プラグイン（プリセット・マイリスト管理）は未着手。設計は `PLAN.md` 参照。
