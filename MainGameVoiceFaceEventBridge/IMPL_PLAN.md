# IMPL_PLAN: response_text トリガーの発火タイミングを「行ごと実尺」ベースに

## ゴール（ユーザー言葉）
- 今は全文の文字位置比例でトリガー時刻を出している。行ごとの文字数＋行ごとの実尺で計算すれば実発話にかなり近づく。

## 現状（確認済み）
- C# `ComputeActionDelaySecondsByTextPosition`（Plugin.Handlers.cs:928-940）= `総尺 × (matchIndex / (text.Length-1))`。**全文比例**。
- 各トリガー（coord/pose/camera/subcamera/clothes）はこの式で `executeAt = baseScheduleTime + delay` を算出し `_delayedActions` に登録（同:62,86,112,149,183）。
- パイプラインは `response_text` に `text` と `delaySeconds`(=総尺) しか積んでいない（pipeline_worker.py:1490, _schedule_response_text:1477）。
- ただし p_json には行データが既にある: `display_line_texts`(737)・`line_durations`(895)・`total_wav_duration`(896)。

## 方式
```
delay(matchIndex) = Σ(先行行の実尺) + (行内文字位置 / max(1,行文字数-1)) × その行の実尺
```
- C#は `searchText = string.Join("\n", lineTexts)` を作り、既存の検出器(matchIndex)をそのまま使用 → 行構造が lineTexts と完全一致するので matchIndex→行マッピングが厳密。
- `lineTexts`/`lineDurations` が無い・件数不一致なら現行の全文方式にフォールバック（後方互換）。

## 必要材料（確認済み）
- パース: `JsonUtility.FromJson<ExternalVoiceFaceCommand>`（CommandParser.cs:39）。`float[]`/`string[]` はそのまま読める（`faces int[]`:102 が実例）。配列オブジェクトのみ手動復元（items）だが今回は不要。
- 型: `ExternalVoiceFaceCommand`（ExternalVoiceFaceCommand.cs:47-）。`delaySeconds`(60) あり。ここに `lineTexts string[]`・`lineDurations float[]` を追加。
- ハンドラ: `HandleResponseTextCommand`（Plugin.Handlers.cs:44-）。`baseScheduleTime=Time.unscaledTime`(47)、`totalDelaySeconds`(48)。各トリガーの `ComputeActionDelaySecondsByTextPosition` 呼び出しを行ごと版に切替。
- Python送出: `_schedule_response_text`（pipeline_worker.py:1477, 呼び出し1894）。payload(1490) に行データ追加。p_json の `display_line_texts`/`line_durations` を渡す。

## 実装ステップ
1. **C# 型**: `ExternalVoiceFaceCommand` に `public string[] lineTexts=null; public float[] lineDurations=null;`
2. **C# 計算**: `ComputeActionDelaySecondsPerLine(string[] lineTexts, float[] lineDurations, int matchIndex)` を新設（上式、separator1文字考慮、末行ガード）
3. **C# ハンドラ**: 冒頭で `useLineTiming = lineTexts!=null && lineDurations!=null && len>0 && len一致`。true なら `text = join(lineTexts,"\n")`。各 `ComputeActionDelaySecondsByTextPosition(...)` を `useLineTiming ? PerLine : 従来` に差し替え（coord/pose/camera/subcamera/clothes の5箇所）
4. **Python**: `_schedule_response_text` に `line_texts`/`line_durations` 引数追加、payload に `lineTexts`/`lineDurations` を載せる。呼び出し1894 で p_json の値を渡す
5. **ビルド**: VoiceFaceEventBridge DLL を Release ビルド
6. **デプロイ**: ゲーム終了後に DLL コピー（起動中はロックで不可）。Python は即反映
7. **確認**: ログ `scheduled delay=` が行ごと値になるか。入力(payload)・処理(マッピング)・出力(発火時刻)を確認
8. **コミット**: C#（型/計算/ハンドラ）と Python（送出）

## 落とし穴
- `searchText` を lineTexts 連結で作るので、検出器が見るテキストは display_lines 由来＝実際に流す行と一致（字幕と同じ粒度）。
- separator は "\n" 1文字。offset 加算は `len+1`。
- 行内はまだ文字数比例（1行内の読み速度一定仮定）。行間ドリフトは消えるが行内誤差は残る＝許容。
- 配列欠落/不一致時は従来式にフォールバック。Python・DLL 片方だけ更新でも壊れない。
- `total_wav_duration == Σline_durations` 済み（pipeline 896）。delaySeconds と整合。
