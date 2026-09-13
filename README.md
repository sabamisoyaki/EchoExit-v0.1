# 666号扉 (EchoExit v0.1)

Unity 製の一人称 3D 異変探索ホラーゲームです。プロジェクト名は `EchoExit`、正式なゲームタイトルは **「666号扉」** です。

このドキュメントはゲーム全体の概要を、企画資料（[Docs/](Docs/)）と現在の実装（[Assets/Scripts/](Assets/Scripts/)）の両方から要約したものです。

---

## 1. どんなゲームか

Backrooms のような正体不明の空間に迷い込んだ一般青年が、部屋に置かれたオブジェクトを観察・検証して **「この部屋に異変があるか / ないか」** を見抜き、**6 回連続で正しく判断する**ことで脱出を目指します。

- ジャンル: 一人称 3D 異変探索ホラー / 1 人プレイ / Windows PC
- 標準プレイ時間: 約 20 分（途中セーブ・チェックポイントなし）
- 言語: 日本語のみ
- 入力: キーボード＋マウス / ゲームパッド

「前回の部屋との差分を暗記する」タイプの八番出口系ではなく、**対象ごとに決まった「儀式」を試して異変であることを能動的に確かめる**点が中核です。

## 2. ゲームループ

1. ラウンドの舞台となる部屋（Scene ID）がランダムに抽選される
2. 通常オブジェクトと、異変オブジェクト（最大 3 個）が JSON 定義に従って配置される
3. プレイヤーは一人称で探索し、怪しい配置を疑う
4. 対象ごとの儀式（注視 / 視線を外す / 接近 / 離脱 / 背後通過 / 静止 / 叩く）を試す
5. 儀式が成立すると、対象が異変としての現象を起こす＝**認識**成立
6. 穏やかな異変は観察するだけ。攻撃的な異変は認識後に追跡してくるので出口へ逃げる
7. 判断して扉を操作する — **異変なし → 前進 / 異変あり → 引き返す**
8. 正解で連続正解数 +1、不正解で 0 にリセット
9. 6 連続正解で脱出（クリア）

ラウンドには制限時間があり、MVP では **180 秒**です。捕獲された場合・時間切れの場合はゲームオーバーとして区別して表示されます。

### 正誤ルール

| 空間の状態 | 正しい行動 |
|---|---|
| 異変がない | 前へ進む |
| 異変がある | 引き返す |

異変ラウンドの抽選確率は **66.6%**（`GameManager.anomalySpawnChance`）、クリア条件は **6 連続正解**（`GameManager.goalThreshold`）です。

### 抽選の設計意図

[RoundSelection.cs](Assets/Scripts/GameSystem/RoundSelection.cs) では、異変 OFF のラウンドを「異変なし部屋」だけから選ばず、異変あり部屋も候補に含めます（構築時に `isAnomaly` アイテムを飛ばす）。これは **部屋の見た目と異変の有無が 1 対 1 に固定され、間取りの暗記だけでクリアできてしまうことを防ぐ**ためです。

## 3. 異変と儀式

儀式の目的は敵の撃破やアイテム取得ではなく、対象が異変であると **認識すること**です。通常オブジェクトに儀式を試してもペナルティはなく、何も起きません。ヒント機能・異変図鑑はありません（経験で学ぶ設計）。

実装済みの異変は [AnomalyRuntimeFactory.cs](Assets/Scripts/GameSystem/AnomalyRuntimeFactory.cs) が `prefabId` から儀式プロファイルへ結び付けています。

| prefabId | 表示名 | 儀式 | 攻撃性 |
|---|---|---|---|
| `changeColorBox` | 変色する箱 | 注視してから視線を外す | — |
| `anomaryShirinkBox` | 縮む箱 | 接近する | — |
| `DollPrefab` | 戻ってくる人形 | 接近してから離れる | — |
| `bears` | 追ってくる人形 | 叩く | 追跡型 |
| `ChairPrefab` | 席を数える椅子 | 背後を通ってから注視する | — |
| `wall` | 呼吸する壁 | 半径内で視線を外して静止する | — |
| `footstepEcho` | 一歩多い足音 | 足音を止め、振り返る | — |
| （未定義） | prefabId をそのまま表示 | 注視（既定） | — |

判定は [AnomalyRitualController.cs](Assets/Scripts/GameSystem/AnomalyRitualController.cs) の共通ランタイムが行い、`Primed`（前提条件成立）→ `Recognized`（認識成立）の 2 段階で進みます。現象の見た目は [Assets/Scripts/Anomary/](Assets/Scripts/Anomary/) 以下の各コンポーネントが担当します。

内部設計では挙動を現象型 (P) / 反応型 (R) / 追跡型 (H) / 特殊終幕型 (E)、危険度を D0〜D3 に分類しています（プレイヤーには非表示）。詳細は [Docs/異変設計書.md](Docs/異変設計書.md) を参照。

追跡型は 1 ステージにつき 1 体まで、認識後に約 1.2 秒の逃走猶予があります。

## 4. 操作

| 操作 | キーボード / マウス | ゲームパッド |
|---|---|---|
| 移動 | WASD | 左スティック |
| 視点 | マウス移動 | 右スティック |
| インタラクト（扉など） | E | □ / X（West） |
| 叩く | 左クリック | 右トリガー |
| カーソル解放 | Esc | — |

実装は [MvpFirstPersonController.cs](Assets/Scripts/MvpFirstPersonController.cs) と [PlayerInteractionController.cs](Assets/Scripts/PlayerInteractionController.cs) です。前進 / 後退 / 最終出口は接触判定ではなく、扉へのインタラクト操作で決定します。

## 5. EditMode（プレイヤー公開機能）

EditMode は開発専用ではなく、プレイヤーに公開するステージ作成機能です。

- 編集できるのは **既存フィールドへの通常 / 異変オブジェクトの配置**
- フィールド自体の作成や、新しい儀式・追跡挙動の実装は対象外
- 保存前に [StageValidator.cs](Assets/Scripts/GameSystem/StageValidator.cs) が検証（オブジェクト 1 個以上、異変は上限まで、追跡型は 1 個まで、prefabId 必須）
- 共有は MVP では **JSON ファイルの手渡し**。将来的に公開サーバー構想あり
- JSON スキーマは基本的に完全互換を維持し、古いユーザーステージも遊べる方針

## 6. データ形式

配置データは JSON で、[SceneData.cs](Assets/Scripts/GameSystem/SceneData.cs) のモデルに対応します。

```json
{
  "scenes": [
    {
      "sceneId": 2,
      "anomalyHouse": true,
      "items": [
        { "prefabId": "ChairPrefab", "position": { "x": -1.0, "y": 0.5, "z": 2.0 },
          "rotation": { "x": 0, "y": 20, "z": 0 }, "isAnomaly": false },
        { "prefabId": "bears", "position": { "x": 1.2, "y": 0.5, "z": 3.0 },
          "rotation": { "x": 0, "y": 180, "z": 0 }, "isAnomaly": true }
      ]
    }
  ]
}
```

- `sceneId`: 部屋 ID（1 以上）。同一 ID の複数ブロックは編集時にマージされます
- `anomalyHouse`: その部屋が異変ありとして使えるか
- `isAnomaly`: そのオブジェクトが異変かどうか。異変 OFF ラウンドでは配置がスキップされます
- 初期データは [Assets/Resources/DefaultAnomalies.json](Assets/Resources/DefaultAnomalies.json)（異変あり / なし計 6 ステージ）。初回起動時に `Application.persistentDataPath/Saves/anomalies.json` へ展開されます（[SavePathProvider.cs](Assets/Scripts/System/SavePathProvider.cs)）

## 7. エンディング

1. 6 連続正解による通常脱出
2. 異変に捕まって発生する特殊エンディング（異変ごとに結末が変わる構想）
3. 特定の特殊シーンによる特殊エンディング

## 8. プロジェクト構成

| パス | 内容 |
|---|---|
| [Assets/Scripts/GameSystem/](Assets/Scripts/GameSystem/) | ラウンド進行、抽選、儀式、EditMode、検証 |
| [Assets/Scripts/Anomary/](Assets/Scripts/Anomary/) | 異変ごとの現象コンポーネント |
| [Assets/Scripts/UI/](Assets/Scripts/UI/) | 正誤フィードバック、ラウンド遷移、終了表示 |
| [Assets/Scenes/](Assets/Scenes/) | `tittle` / `MainScene` / `EditMode` / `endTitle` |
| [Assets/Resources/Prefabs/](Assets/Resources/Prefabs/) | 配置用プレハブ（`Abnormalities` / `Structures` / `Triggers` ほか） |
| [Assets/Tests/Editor/](Assets/Tests/Editor/) | ゲームループ・データのユニットテスト |
| [Docs/](Docs/) | 企画資料 |

主要なエントリポイントは [GameManager.cs](Assets/Scripts/GameSystem/GameManager.cs) で、ワールド生成・異変抽選・制限時間・正誤判定・シーン遷移を統括します。

## 9. 未実装 / 未確定

実装が追いついていない主な企画要素:

- 多種多様なフィールドをラウンドごとに切り替える仕組み
- フィールド形状に対応した本格的な追跡 AI と逃走経路
- 異変ごとの専用捕獲演出・特殊エンディング
- 扉を操作するオープニングと開閉アニメーション
- 感度・キー割り当ての設定画面、アクセシビリティ対応
- ユーザー制作 JSON の公開サーバー

未確定の仕様（異変数の最終上限、制限時間、難易度カーブ、ネタバレ防止、年齢区分など）は [Docs/ゲーム概要_現在の認識.md](Docs/ゲーム概要_現在の認識.md) にまとまっています。

## 10. 関連ドキュメント

- [Docs/ゲーム概要_現在の認識.md](Docs/ゲーム概要_現在の認識.md) — 企画回答と実装差分の詳細
- [Docs/異変設計書.md](Docs/異変設計書.md) — 異変ごとの個別仕様（基準文書）
- [Docs/実装計画書_問題点と改善.md](Docs/実装計画書_問題点と改善.md) — 実装上の課題と改善案
- [Docs/企画確認_追加質問事項.md](Docs/企画確認_追加質問事項.md) — 未回答の企画確認事項
