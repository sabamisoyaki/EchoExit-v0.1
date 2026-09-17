# エージェント向けメモ（Codex / Claude Code 共通）

このリポジトリで作業する LLM エージェントへの申し送りです（2026-09-17 時点）。
いまの実装の説明は `Rebuild/666Door/Documentation/現状実装まとめ.md` を読んでください。

## 1. 作業対象

- 開発しているのは **`Rebuild/666Door`** です（独立した Unity 6000.3.10f1 / URP プロジェクト）。リポジトリ直下の `Assets/` などは旧 EchoExit なので、再構築の作業では変更しないでください。
- ブランチは `feat/rebuild-666door` → `feat/scene-split` → `feat/first-person-editor` の順に積み重なっています。どれもまだ push していません。
- 旧 EchoExit からは、どこからも使われていないもの（URP テンプレートの残り、扉アセット `GVOZDY`、未使用の音声・旧形式 JSON など）を削除済みです。必要になったら Git 履歴から戻せます。
- `Rebuild/666Door/Artifacts/`（テスト結果・スクショ）はコミットしません。
- コミットはユーザーに頼まれたときだけ行います。メッセージは日本語の Conventional Commits（`feat:` / `fix:` / `docs:` など）で書きます。

## 2. 仕様の読み方

- `Docs/再構築仕様書.md` は機能の一覧が中心で、**遊びとしての意図が書かれていないことがあります**。実際に EditMode は §6 の機能一覧だけをもとに見下ろし型の配置ツールとして作られ、企画の「一人称で歩き回り、置いた異変の儀式をその場で試す空間」とズレました（作り直し済みです）。
- UI や操作感を作る前に、`Docs/ゲーム概要_現在の認識.md`、`Docs/異変設計書.md`、旧実装（`Assets/Scripts/`）でも意図を確認してください。
- 設計の判断が分かれるときは、推奨案を付けた選択肢でユーザーに聞いてください。ユーザーは選択肢ではなく自由記述で細かい条件を返すことがあるので、回答をそのまま読み解いてください。
- 仕様書 §2 の「破ってはいけない」制約（追跡型は1体まで、など）を緩めるときは、仕様変更であることを明示してから行ってください。

## 3. いまの構成の要点

- シーンは Title / Game / EditMode（画面ごと。`SceneController` 派生のコントローラーを置く）と、部屋の Field（各画面シーンが追加読込する）の4つです。
- `Field.unity` と3つの画面シーンは、手で編集してかまいません。`ProjectBootstrap.Setup` は**足りないものだけ**を生成します。メニューの「部屋シーンを作り直す（上書き）」は手作業の変更を消します。
- プレイヤーとカメラは `Assets/Prefabs/Player.prefab` です。各コントローラーの `player` 欄に割り当てが必要です。UI（`GameUI`）はまだコードで生成しています。
- マテリアルはアセットです（`Assets/Field/Materials`、`Assets/Resources/Materials/Catalog`）。実行時に `Shader.Find` で Lit マテリアルを作らないでください。
- 異変を動かす処理（`AnomalyActorSet`）は、Game と EditMode で共通です。
- 異変の上限や配置の重なりのルールは `Assets/Resources/GameSettings.asset` で調整します。
- `UnityEngine.Object` に `??` / `?.` を使わないでください（Unity の擬似 null を素通りするため。過去に一括で直しています）。

## 4. Unity の実行と検証

- ユーザーは `Rebuild/666Door` を **Unity エディタで開いたまま**にしていることが多いです。開いている間は、同じプロジェクトでバッチモード（`-batchmode` のテスト・ビルド・`-executeMethod`）を実行できません。
- 開いているエディタには **Unity MCP**（`unity-mcp`：`%USERPROFILE%\.unity\relay\relay_win.exe --mcp`。プロジェクトの `com.unity.ai.assistant` 経由）で接続し、コンソールやシーン階層を直接確認できます。まずこれを使ってください。繋がらないときは `%LOCALAPPDATA%\Unity\Editor\Editor.log` を読みます。
- テストやビルドをバッチモードで実行するなら、次のどちらかにします。
  - ユーザーにエディタを閉じてもらう。
  - プロジェクトを一時フォルダへ複製して実行し、生成物（シーン・プレハブ・マテリアルと、その .meta）だけを元へ戻す。この場合は次に注意してください。
    - **新しい .cs を作ったら、先に元のプロジェクト側で .meta を作って GUID を固定します。** 開いているエディタと複製で別の GUID が振られると、戻したシーンのスクリプト参照が壊れます。
    - 複製には `com.unity.ai.assistant` を入れません（バッチ実行中のパッケージ取得を避けるため）。
    - 一時フォルダは日をまたぐと一部が消されることがあります（`manifest.json` が既定のテンプレートに戻る、`Newtonsoft` が消える、など）。結果がおかしいときは `Library`・`Packages` から同期し直し、テスト結果 XML の `end-time` で古い結果を読んでいないかも確かめてください。
    - 複製ではテスト中に無関係なエラー（`Host type is not matching any asset type ... TraceVirtualOffset.urtshader`）が出ることがあります。エディタ設定の **Error Pause** がオンだと、それでプレイモードが止まります。複製側のテストにだけ `LogAssert.ignoreFailingMessages = true` と一時停止の解除を足してください。元のテストには入れません。
- コマンドの例は `Rebuild/666Door/README.md` と `現状実装まとめ.md` の §8 にあります。

## 5. テストを書くときの罠

- EditMode の `[UnityTest]` で `yield return new EnterPlayMode()` した後は、**入れ子の `IEnumerator` を `yield return` しても実行されません**。シーンの読込待ちは、テスト本体のループで `SceneTestUtility.IsReady(out x)` を回してください。
- バッチモードの `-executeMethod` の中では、`AssetDatabase.ImportPackage` が後回しにされます。そのため TMP の基本リソースは、`ProjectBootstrap` で .unitypackage を直接展開しています。
- 統合テストは実時間で待つ箇所（`WaitForSecondsRealtime`）があります。フレームが進まずに失敗するときは、まず Error Pause などでプレイモードが止まっていないかを疑ってください。

## 6. Windows とツールの罠

- パスに空白と日本語が含まれます（`H:\unity\EchoExit v0.1`）。必ず引用してください。
- PowerShell 5.1 では、here-string を `git commit -F -` に渡せません。コミットメッセージは一時ファイルに書いて `-F <ファイル>` で渡してください。
- 同じコマンドの中で `robocopy`（`/MIR` などのスイッチ）と `Remove-Item` を混ぜると、ツールの安全チェックで止められます。コマンドを分けるか、`[IO.File]::Delete` を使ってください。
- 「同期してから実行する」のように順序に依存するコマンドは、並列に発行しないでください。

## 7. トークンの使い方

- ユーザーは消費量を気にしています。以前 Codex を `gpt-6-astra` / effort `ultra`、サブエージェント付きの計4本で動かしたところ、約1,540万トークン（大半はキャッシュされた入力）を使って利用上限に達しました。
- 地道なデバッグや検証に、最大の推論量や並列エージェントは要りません。
- Unity のログは丸ごと読まず、`error CS|Exception` などで絞ってください（1回の実行で数百KBになります）。
- スクショなどの画像は、見た目の確認が必要なときだけ読んでください。
