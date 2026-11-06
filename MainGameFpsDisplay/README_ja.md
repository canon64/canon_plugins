# MainGameFpsDisplay

画面右上に直近0.5秒間の平均FPSを表示し、時間停止や再生速度に影響されない実時間で計測します。

初期状態は表示ONで、プレイバックバーの「万華鏡ON/OFF」のすぐ右にある「FPS OFF」で非表示、「FPS ON」で再表示できます。

表示設定は `BepInEx/config/com.kks.maingame.fpsdisplay.cfg` の `Display / Visible` に保存され、次回起動でも引き継ぎます。

FPSの文字はマウス入力を消費しません。プレイバックバーのボタンは既存の共通入力保護範囲に含まれます。

デスクトップとVR起動に対応し、表示先はゲーム画面の右上です。
