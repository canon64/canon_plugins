# VR描画の負荷・接地・画像確認

2026-10-31 13:25 JST 配置完了。

- MainGameMagicPaint: 87040 bytes, SHA256 8001BB3CFAB939E33DC5C9C3D8D9AF9FBFE1BFFF2241077133D425C3F6A7CF64
- MainGameBlankMapAdd: 30987264 bytes, SHA256 EDDA9689C3879E308A8988955E596AEAE95615A3B100F9DCCD7856BA249D190E
- 両DLLは各製品のbin/Release/net472から正規フォルダへ配置し、サイズ・日時・ハッシュ一致と重複なしを確認。
- 接地設定は既存cfgもAttachMm=20、DetachMm=30へ変更。バックアップはmagicpaint-deploy-20261031-132537-621。
- 製品コア124項目、ポップアップ34項目が成功。VR実機は未確認。
- 全三角形反復探索をBVHへ置換、ガイドの探索数削減、画像生成を実描画時へ遅延、変更タイル更新、微小粒の面積反映、肌合成の色・パス明示、ポップアップ遅延呼出しのnull防止。
- 本体右側へ全設定・選択矢印・ゲージ・CPUインク画素数を表示。
- 原因は未確定。CPUインクが0なのか、GPU転送・合成・割当のどこで消えるか、診断画像で判断する。
- 診断既定OFF。タブの診断を有効にして保存ボタンを押した時だけ、UserData/MagicPaint/Diagnostics/日時へ画像とstate.txtを出す。OFFで専用監視や収集は行わない。
