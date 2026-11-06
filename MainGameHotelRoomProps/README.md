# Hotel Room VR Props

ホテルの家具・小物を左右のグリップで掴み、離すとその位置と向きを保存するプラグイン。

`HotelRoomProps__201` のような客室ルートの下にある `PROP__` オブジェクトをロード時に一度だけ登録し、掴んだ間は既存VRGINコントローラーへ親子付けして追従させる。

クッションの縁やぬいぐるみの目・耳は本体と一緒に動き、ベッド本体（`bed_furniture`）・壁・床・天井・固定扉は掴む対象に含めない。ベッド本体の配置読込・保存は維持する。

配置は `F:\kks\UserData\HotelMode\RoomLayouts\201.json` のように部屋別で保存し、次の読込時に安定IDで復元する。

保存はグリップ解除・フォーカス喪失・追跡喪失・客室非表示・終了時に行い、一時ファイルからの置換と直前データのバックアップを使う。

canon_gate の `[Exceptions] ActivityAllowedPluginGuids` に `canon.maingamehotelroomprops` を指定した次回の客室読込で実行を許可し、門番が停止すると掴み処理も停止する。

追加診断は `[Diagnostics] Enabled=false` が既定で、毎フレームの探索・距離監視・保存監視は追加していない。

配置JSONの試験とビルドは検証済みで、実際のVR機器による左右の掴み・置き直し・再入室時の復元確認はユーザーが行う。
