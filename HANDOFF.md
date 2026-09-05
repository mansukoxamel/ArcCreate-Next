# ArcCreate 本家互換開発ハンドオフ

最終更新: 2026-09-05

## 開発目的

このフォークを、AFF譜面の編集機能を維持しながら、同じAFF・再生時刻・入力列に対するノーツ位置、アーク形状、表示状態、入力判定、コンボ、スコアを本家Arcaeaへ近づけた派生版として開発する。

本家バイナリのコードを移植することが目的ではない。所有するAPKを読み取り専用で静的解析し、外部から観測できる入力と出力の対応を独立した仕様・テストとして再実装する。

## リポジトリ状態

- ローカル: `D:\chaos\MAGATU_EMULATOR\ArcCreate`
- リモート: `https://github.com/mansukoxamel/ArcCreate-Next.git`
- 基準ブランチ: `main`
- 移転時の起点: `755c2e8818dab3c8404b3ff997e87373607891cd`（元の`origin/trunk`）
- Unity: `6000.3.20f1`（Unity 6.3 LTS）
- Git署名: `jinn / jinn@local`

`main`を復元可能な基準として保ち、試行錯誤する変更は目的別ブランチで行う。`master`は使わない。

## 解析資料

解析資料の本拠地は[Documentation/ArcaeaResearch](Documentation/ArcaeaResearch)へ移した。以後、ArcCreate本家互換開発に関する調査結果はここを更新する。旧配置のMAGATU TEMPO FORGE側には実体を残さず、移転先への案内だけを残す。

最初に次の順で読む。

1. [ARCAEA_COMPATIBILITY_RESEARCH.md](Documentation/ArcaeaResearch/ARCAEA_COMPATIBILITY_RESEARCH.md) — 本家、ArcCreate、Arcade Plusの確定差分と実装優先順
2. [ARCAEA_NATIVE_ANALYSIS.md](Documentation/ArcaeaResearch/ARCAEA_NATIVE_ANALYSIS.md) — 本家3.6.1／5.5.8の入力判定、判定点、再取得、ネイティブ関数根拠
3. [AFF_NOTE_JUDGEMENT_RESEARCH.md](Documentation/ArcaeaResearch/AFF_NOTE_JUDGEMENT_RESEARCH.md) — ノート型、判定点数、実物譜面との照合
4. [AFF_FORMAT_RESEARCH.md](Documentation/ArcaeaResearch/AFF_FORMAT_RESEARCH.md) — 正式AFF表記と各エディター独自拡張の区別

保存サイト、集計JSON、再現スクリプト、回帰試験も同じディレクトリ内に元の相対構造を保って移した。資料内リンクはそのまま利用できる。

## 外部に置く原資料

次は容量、権利、誤push防止のためGitリポジトリへ複製しない。

- 実物AFF 1,478譜面: `D:\work\arcaea\songs`
- Arcaea 3.6.1: `D:\work\arcaea\APK\3.6.1.mod`
- Arcaea 4.0.255: `D:\work\arcaea\APK\4.0.255.mod`
- Arcaea 5.5.8: `D:\work\arcaea\APK\5.5.8.mod`

解析対象`libcocos2dcpp.so`のSHA-256は`ARCAEA_NATIVE_ANALYSIS.md`に固定してある。バイナリを取り違えない。

## 現在までに確定した重要事項

- ArcCreateはArcade Plusより入力判定と編集機能が発展しており、開発母体に適する。
- Arcade Plusはcameraの`s`とアーク追従tiltなど、一部が本家3.6.1に近い。比較資料として使う。
- アーク8種類`b / s / si / so / sisi / siso / sosi / soso`の連続補間式とX/Y割当はArcCreate、Arcade Plusとも本家3.6.1に一致する。
- アーク描画用の折れ線分割と端点整数化は、0.3.2で本家準拠へ修正した。
- 移転起点のArcCreateではcamera命令`s`がsmoothstepだが、本家3.6.1とArcade Plusは線形である。0.1.1で本家準拠の線形補間へ修正した。
- 本家3.6.1のアーク追従tiltは追従中4%、中央復帰中2%を1更新ごとに補間する。移転起点にあった常時`6 * deltaTime`は、0.1.2で本家準拠の二係数へ修正した。
- 移転起点のArcCreateではZ距離を基準BPMで正規化していたが、0.5.0でtiming区間の積分値とハイスピードから本家準拠の距離へ変換するよう修正した。
- 移転起点のArcCreateにあった厳しすぎるアーク接続条件は、0.6.0で本家3.6.1の時刻・X・Y・線種条件へ修正した。
- 初回取得と保持の入力範囲、物理リリース後の再取得ロック、判定点ごとのMiss猶予、異色アーク近接時の全指受理は、0.7.0で本家3.6.1準拠へ修正した。
- tap、arctap、arcの表示開始時の透明度と、hold／arcの判定面クリップは、0.8.0で本家のネイティブ処理に合わせた。
- 3.6.1で確定し実装へ反映した判定点、接続、入力状態、表示更新の主要経路は、5.5.8のRTTI、仮想関数表、命令列、定数へ対応付けた。
- Arcade Plusには本家相当の手入力判定状態機械がないため、Arcade Plus自体を開発母体にはしない。

数値、式、関数アドレス、反例は必ず解析資料本文を参照する。この要約だけから実装しない。

## 実装方針

見た目を目視だけで合わせると、Z距離とカメラの誤差が互いを隠す。次の順で、純粋関数または小さい状態遷移として試験を先に固定する。

1. camera命令`s`の線形補間（0.1.1で対応済み）
2. アーク追従tiltの追従・復帰係数（0.1.2で対応済み）
3. アークの正規化時間による折れ線分割と端点整数化（0.3.2で対応済み）
4. timing区間をまたぐZ距離積分とハイスピード（0.5.0で対応済み）
5. アーク接続条件（0.6.0で対応済み）
6. 初回取得、保持、離脱、再取得ロック、異色近接猶予（0.7.0で対応済み）
7. tap、hold、arc、arctapの表示開始、消滅、手前クリップ（0.8.0で対応済み）
8. 3.6.1で確定した処理を5.5.8へ命令列・定数・呼出関係で対応付ける（0.8.1で入力平面範囲と異色交差猶予まで対応済み）

本家互換挙動を既定動作とし、切替可能な互換モードは設けない（2026-09-05決定）。本家との差が外部観測と解析で確定した箇所は、根拠と回帰試験を残しながら順次置き換える。AFF編集機能と操作性の改善は維持・発展させる。

## 検証

研究用回帰試験は次で実行する。

```powershell
cd D:\chaos\MAGATU_EMULATOR\ArcCreate\Documentation\ArcaeaResearch
python -m unittest -v test_aff_judgement_analysis.py
```

ネイティブ解析の再実行には`tools\analyze_arcaea_native.py`を使う。LIEF 0.12.3とCapstone 5.0.7で確認済みである。APKと実物譜面は読み取り専用で扱い、生成物やキャッシュを原資料のディレクトリへ書き込まない。

実装変更では、対象機能だけを変更し、Unityでのビルドまたは該当テスト、同一AFFによる変更前後比較まで確認する。変更価値がある単位ごとにバージョンと変更履歴を更新し、コミットする。
