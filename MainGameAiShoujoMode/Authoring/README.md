# 会話・イベントエディター

AI少女・ホテル共通の編集機能。通常は編集メニューも編集ボタンも非表示で、右Altで表示・非表示を切り替える。

1. キャラプリセットで人物名・カード・音声を決める。ゲーム内音声は検索と試聴、外部音声はWAV/OGG/MP3のパスで指定する。
2. イベントを作り、歩行／フリーH、マップ、人物の状態・配置、家具・アイテムを決める。
3. 会話と選択肢を並べ、会話ごとの音声・アニメ・体位・カメラを指定する。
4. 「試す」で確認し、「保存」で確定する。「非表示」は下書きを保持し、「閉じる」は未保存分を破棄する。

JSONの正本は `F:\kks\UserData\canon_plugins\EventAuthoring`。AIと編集するときは「AIとJSONで編集」で書き出し、Data内のJSON SchemaとAI_AUTHORING.mdを渡す。初回の許可済み起動時にReferenceへ同梱資料を配置する。

非Hの許可はcanon_gate.cfgのActivityAllowedPluginGuidsに `com.kks.maingame.aishoujomode` を指定する。作成したフリーHの許可は所有シーンの開始時に別途取得する。

検査はTests/Authoring、Tests/AuthoringStage、Tests/Authoring/reference_contract.py。本物のゲーム画面の描画・操作・音声・H開始成功は別途ユーザーが確認する。ビルド成果物はMainGameAiShoujoMode/bin/Release/net472で、ゲーム起動・自動操作・DLL配置は検査に含めない。
