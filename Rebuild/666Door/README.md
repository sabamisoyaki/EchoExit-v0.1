# 666号扉 — 再構築版

Unity 6000.3.10f1 / Windows / 日本語。

`Docs/再構築仕様書.md` と `Docs/異変設計書.md` を基に、旧ゲームのC#コードとシーンを持ち込まずに作った独立プロジェクトです。旧プロジェクトを開く代わりに、Unity Hubで **この `666Door` フォルダ**を追加してください。

## 起動

ビルド済みの場合は `Builds/Windows/666Door.exe` を実行します。

Unityで開く場合は `Assets/Scenes/Title.unity` を開いて再生します。シーンや生成アセットがない初回チェックアウトでは、メニューの **666号扉 → プロジェクトを初期化** を実行してください。足りないシーン、URP、日本語フォント、熊の見た目プレハブ、マテリアルをEditor APIで生成します（既にあるシーンは上書きしません）。

### シーン構成

| シーン | 役割 |
| --- | --- |
| `Title.unity` | タイトル画面。起動シーン |
| `Game.unity` | 探索とラウンド進行。やり直しはシーン内で行う |
| `EditMode.unity` | 部屋の編集 |
| `Field.unity` | 部屋の建物・照明・扉・固定の家具。上の3シーンが追加読込する |

画面の移動はシーンの切り替え、ラウンドの交代は `Field` の `Stage placements` の下だけを作り直します。エディタで Title / Game / EditMode を開くと `Field` も自動で追加表示されるので、部屋はシーン上で直接編集できます。コードから部屋を作り直したいときは **666号扉 → 部屋シーンを作り直す（上書き）** を使います（手作業の変更は失われます）。

## 遊び方

異変がなければ前の扉へ、異変があれば後ろの扉へ。6回連続で正しく判断すると脱出できます。不正解は連続数が0に戻り、捕獲または180秒の時間切れで探索が終わります。

| 操作 | キーボード／マウス | ゲームパッド |
| --- | --- | --- |
| 移動 | WASD | 左スティック |
| 視点 | マウス | 右スティック |
| 扉を操作 | E | X / □ |
| 叩く | 左クリック | 右トリガー |
| 一時停止 | Esc | Start |

感度、字幕、キーボードの割り当ては設定画面で変更できます。儀式の手順や図鑑はゲーム内に表示しません。

## 部屋の編集

タイトルの「部屋を編集する」から既存ステージの再編集と新規作成ができます。通常／異変のカテゴリを選び、名前を選択して床に配置します。配置物をクリックして選択し、Rで回転、Deleteで削除、F5または保存ボタンで上書き保存します。右クリックで配置プレビューを解除します。

テキスト入力中やUI操作中は配置ショートカットを抑止します。保存時に異変数、追跡型の重複、初期位置、扉への重なりなどを検証します。未対応IDは警告を表示して元データを保持します。

保存先は `Application.persistentDataPath/Saves/anomalies.json` です。Windowsでは通常 `%USERPROFILE%/AppData/LocalLow/Door666/666号扉/Saves/anomalies.json` になります。旧JSONを利用する場合はゲームを終了してからこのファイルに配置してください。旧プロジェクトの保存ファイルを自動で上書きすることはありません。

## 構成と調整

- `Assets/Game/Core`：Unityに依存しないステージ入出力、抽選、ラン状態、儀式、捕獲猶予。
- `Assets/Game/Runtime`：シーンごとのコントローラー、一人称操作、知覚、配置物の生成、異変演出、UI、配置編集。
- `Assets/Game/Editor`：初期化、部屋シーンの生成、エディタ上での Field 自動表示。
- `Assets/Field`：部屋シーン用のマテリアルとテクスチャ。
- `Assets/Resources/AnomalyDefinitions.json`：7種類の異変定義。配置JSONから儀式・危険度を分離。
- `Assets/Resources/GameSettings.asset`：ラウンド時間、異変数上限などの調整。
- `Assets/Resources/PlayerControls.inputactions`：Input Systemのみを使用する操作定義。
- `Assets/Tests/Editor`：データ互換、抽選、状態遷移、儀式、配置検証、実行時統合のテスト。

既存の9ステージJSONは無変更で同梱しています。世界の座標を保ち、間取りの暗記だけで異変の有無を判定できない抽選規則を維持します。儀式の値は再構築仕様書の値を採用しています。未決の難易度変化や異変個数の重み付き抽選は導入せず、ステージ作者の配置を上限内で使用します。

## 再現用コマンド

このフォルダを作業ディレクトリにして実行します。

```powershell
unity run . --editor-version 6000.3.10f1 --timeout 600 -- -executeMethod Door666.Editor.ProjectBootstrap.Setup
unity test . --editor-version 6000.3.10f1 --mode EditMode --output ./Artifacts/test-results.xml --timeout 600
unity run . --editor-version 6000.3.10f1 --timeout 900 -- -executeMethod Door666.Editor.ProjectBootstrap.BuildWindows
```

統合テストはタイトル、探索、脱出、配置編集の画面を `Artifacts/Screenshots` に保存します。テスト結果と実施状況は `Documentation/実装・検証記録.md` に記載します。

## 素材

旧プロジェクトから持ち込んだものは、互換性基準の `DefaultAnomalies.json`、熊のモデル `bears.fbx`、日本語フォントだけです。通常家具・部屋・テクスチャ・音は新しく生成しています。熊は見た目のみのプレハブへ変換し、旧コンポーネントは使用しません。日本語フォントのライセンスは `Documentation/OFL.txt` を参照してください。
