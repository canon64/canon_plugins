# AI少女・KKSの比較知識DB

正本はai_kks_knowledge.sqlite3。過去成果を再採取せず、ゲーム/ドメイン/型別のentity、生値property、関係、確度付き対応、操作の前提/引数/効果/後始末、原典hash/locator、coverageを保存する。非公開ローカル成果で、Git公開/commit/pushは禁止。

現行のlocal behavior task集計は `kb.py coverage --id coverage:dab254d2e16f50ae360a9115` で確認する。現在の根拠はbehavior-task-meaning-union-final.json（unique1672/missing0/duplicate0）。BEHAVIOR_MEANING_PROGRESS.mdの1611/1672・missing61は保存した歴史checkpointであり、現行の未読件数ではない。local row契約と外部87nodeのinstance条件/委譲helper、全pathの実行、KKS移植/実機は別scopeである。

既存analysis/ai-material-library/materials.sqliteはread-only ATTACHで保持し、material_query.pyを再利用する。native object/pointerの巨大層を複製しない。中央DBが対応・出典・調査状態を管理し、元DB/gzip/object-indexは根拠として参照する。

PowerShellでrepo rootから共通Pythonを使う。JSONが既定、`--format table`はサブコマンドの前に置く。

```powershell
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py --format table search cf_J_Foot01 --kind bone --limit 4
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py --format table compare cf_J_Foot01_L --game ai --kind bone --limit 2
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py lookup SetParamator --kind operation --limit 3
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py mappings cf_J_Foot01_L --kind bone --limit 8
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py coverage --domain rig
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py sources --limit 5
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py stale
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py material item 0:0
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py material action 0:0
```

compareは完全一致と意味対応を優先し、通常は左右名/確度/対応種別/理由/根拠の短い結果を返す。JSONには実path/rest/scale/基底も含み、`compare --detail`またはlookupで全property/関係/操作を表示する。意味骨対応が確定していても構造/動作の等価を意味しない。候補や未対応を確定として表示しない。

mapping行がないentityは`comparison_not_reviewed`（対応判定の記録なし）、明示した未割当は`unmapped`、検索entity自体が見つからない場合は`not_indexed_or_query_mismatch`と区別する。いずれも「KKSに存在しない」の自動判定ではない。元仕様や数値比較が保存済みでも、意味対応の判定がない場合がある。`mapping-coverage --game ai --domain persistence`等でdomain/kind別の対応記録数を確認できる。確度別数は複数scopeの重なりを含むため足し合わせて網羅率としない。

調査前はsearch/compare/coverage/staleで既存結果を確認し、無ヒットならsources/researchと旧DB/報告も調べる。不足・原典hash変化だけを調査し、既知/追加根拠/重複再採取/以前の不足閉鎖を分類して戻す。今回のDB化は既知成果の整理であり、新規原本調査と称しない。

更新はSTAGING.mdに従い担当別.ndjsonまたは.ndjson.gzを作り、単一writerで`kb.py import <path...>`。各sourceの実hashを確認してからtransactionで取り込み、同一staging hashはno-op。変わったsourceの旧factはstaleとして残し、変更factはfact_historyへ保存する。

sources.sha256は登録した実ファイルのhash、各factのsource_hashはその根拠版。原本assembly/bundleと抽出JSON/C#は別source/関係として辿る。factのextraction_hashはstaging側から与えた抽出checksumを保持し、省略時は正規化したfactのchecksumを生成する（既存NULLも同方式で補完）。これは実行時の値や原bundleのhashを推測するものではない。原本版が不明な旧資料はcoverage/evidenceで不明のまま保持する。

`stale`はsize/mtimeを用いたhash cacheを使用し、`stale --refresh`は全原典のhashを再計算する。変化が検出されても内容を自動で新根拠へ読み替えない。`check`はSQLite整合性/FK/出典hash/stale/証拠参照を検査する。外部キー不正・原典hash不一致はimport全体をrollbackする。

schema.sqlのmigrationは現行v3（旧ファイル発見用metadataを追加）で、破壊的dropを行わない。`test_kb.py`は冪等、変更検出、履歴、候補保持、FK不整合rollback、source不一致拒否、FTS filter、gzip、write中reader、未照合表示、同名snapshotでのsemantic対応優先、limit1でも他scope判断を残す12検査を独立DBで行う。`check`は全ページのquick_checkとFK/出典/証拠参照を検査する。

新規原本採取前は、repo-local `.agents/skills/kks-ai-knowledge-db/SKILL.md` の必須順序に従う。`references FaceBlendShape --limit 3` は旧knowledge_indexの型/API名から過去文書path/行へ接続する。`discover bo_head_00 --limit 5` は広域既存ファイルのpathと本文索引を検索する。索引更新は `index_prior_files.py`（既定root F:/kks/work、J:/workspaces/ai_shoujo_map_extract、既知ポインタで見つかったJ:/workspaces/KksExpressionLab）。build/cache/staging、無関係なTTS生成outputs、一時デプロイ複製を明示除外してmetadataを列挙し、2MiB以下のテキストだけ本文FTSへ格納する。`discovery`でroot/直近run/索引状態を確認でき、詳細receiptはdiscovery-receipts/へ保存する。列挙/本文索引は内容レビュー・原典証明・中央factへの取込と別。大きなraw/旧DBは専用catalogを照合し、無ヒットだけで新規採取しない。

操作を少数行で引く例（すべて `--format table` をcommand前へ）：

```powershell
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py --format table operation HScene.InitCoroutine --phase prerequisite --limit 2
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py --format table operation HScene.ChangeAnimation.dispatch0 --phase effect --limit 2
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py --format table operation HScene.EndProc --phase cleanup --limit 2
& J:/system_tools/venvs/py310-common/Scripts/python.exe -B analysis/ai-shoujo-knowledge/kb.py --format table operation ConfigScene.ConfigWindow.Awake --phase effect --limit 2
```

現在のoperationは選定済み原本契約の前提/条件/効果/復旧を構造化した範囲と、全文契約段落をsequenceとして索引化した範囲がある。API宣言数、元資料行数、全操作の意味解釈済み件数を同一視しない。

旧顔辞書は `F:/kks/work/FaceExpression/hface_extract/hface_dict.db` の792表/64,108顔行をread-onlyで再利用する。`face --file-id 1 --face-id 0 --limit 1` または `face <HFace名> --limit 3` で数値と元辞書hash/主キーlocatorを返す。別path `F:/kks/work/analysis/hface_dict.db` の0byteファイルとは区別する。保存辞書の存在はAI/KKSの目・口pattern番号やhead blendshape indexの同一性を意味しない。

現時点では旧生活素材・骨/animation/IK/体型・原本操作・KKS API/consumerのimportを進行中。coverage partialは未解決のまま残す。全API/全生活操作の意味対応や移植成功を完了と宣言するDBではない。未取込/未照合はcoverage/researchへ明示する。
