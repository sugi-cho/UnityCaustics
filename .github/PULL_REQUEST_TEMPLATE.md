# Pull Request テンプレート

## 概要
- Issue: #<番号>（例: #123 / [RC-003]）
- 変更点の要約:

## スクリーンショット（任意）
<画像/動画があれば>

## 動作確認手順
1. シーン Demo/Scenes/ReflectiveCaustics_Demo.unity を開く
2. 再生 → 壁に反射カスティクスが合成されることを確認
3. 影に入った領域での減衰、ブラー調整の挙動を確認

## パフォーマンス（任意）
- 解像度/ブラー半径/グリッド密度とフレームタイムのメモ

## 影響範囲
- レンダリングパイプライン / マテリアル / シェーダ

## チェックリスト
- [ ] Lint（actions/markdown/yaml）OK
- [ ] CI（Unity tests/build）OK
- [ ] 警告・例外なし
- [ ] closes #<issue-number> を本文に含めた
