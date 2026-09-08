# LWF Tax Ledger

**Lazy Witch's Factory** の給与明細に課税欄を足す（ver 0.27.0 で動作確認）

- 結果画面に、その回の課税で失ったものを期ごとに出す
- Inferno で表示
- 画面の文字はゲームの言語に従う
- 内容はダミー

![課税欄](https://raw.githubusercontent.com/KiyonakaNata/lwf-tax-ledger/main/img/result-full.jpg)

## 内訳を開く

- 行をクリックすると、アイテムごとの個数・資源・換算額が出る
- もう一度押すと閉じる
- cfg の `Expand breakdown = true` で開いた状態から始まる

![内訳](https://raw.githubusercontent.com/KiyonakaNata/lwf-tax-ledger/main/img/ledger-breakdown.png)

## 課税ごとに出る値

| 課税 | 出る値 |
|---|---|
| 人頭税・固定資産税・消費税 | 固定費用の点数と、支払った合計額 |
| 投資課税・損害賠償・特産品税 | 失った資源 |
| 中央集権 | 無効になった回数とアイテムの内訳 |
| サンプル提供命令・関税 | 減った製作数と換算額 |
| 鉱産税・農産税 | 増えた必要打数 |
| トリプルチェック・逮捕 | 減った製作数と換算額（速度低下からの推定値） |
| 節水令 | 下がった搬送速度の率 |

![固定費用](https://raw.githubusercontent.com/KiyonakaNata/lwf-tax-ledger/main/img/ledger-fixed-cost.png)

## 入れかた

- MOD管理ソフトで入れる。手で入れるなら `LwfTaxLedger.dll` を `BepInEx/plugins/` へ
- Inferno を 1 回遊ぶ

## 設定

`BepInEx/config/kiyonakanata.lwftaxledger.cfg`

| 項目 | 既定 | 値 |
|---|---|---|
| `1. General/Enabled` | `true` | `false` で出さない |
| `1. General/Expand breakdown` | `false` | `true` で内訳を開いた状態 |

`[2. Layout]` は文字と余白。調整不要

`[9. Dev]` は開発用。`Enabled` は `false` のまま

## 消しかた

- `BepInEx/plugins/LwfTaxLedger.dll` を消す
- 設定も消すなら `BepInEx/config/kiyonakanata.lwftaxledger.cfg`

## リンク

- [GitHub](https://github.com/KiyonakaNata/lwf-tax-ledger)
- [公式のMODに関する方針](https://store.steampowered.com/news/app/3971650/view/699897618302503133)

非公式のMOD、公式のサポート対象外
