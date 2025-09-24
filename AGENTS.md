# AGENTS.md - Reflective Caustics (URP/PC)

> このファイルは Codex Cloud などのクラウド環境で動作するエージェント向けガイドラインです。

## コミュニケーション / Communication

- この環境で作業するエージェントは、**すべての応答・コメント・PR 説明を丁寧な日本語**で記述してください。
- 例外が必要な場合（外部仕様書などで英語必須）は、Issue や PR に明記してください。

## Overview

- Goal: Implement reflective caustics on planar walls for a Unity URP project (PC).
- Key folders:
  - `Assets/CausticsReflective/` - Scripts, shaders, materials, demo assets
  - `.github/workflows/` - CI for tests and builds (GameCI)
  - `.github/ISSUE_TEMPLATE/` - Issue forms RC-001..RC-012

## What you (the agent) SHOULD do

- Work in **small branches**: `feature/rc-00x-<slug>`
- Always include **`closes #<issue-number>`** in PR descriptions
- Run **local checks** listed below before opening a PR
- Keep changes scoped to the referenced issue

## What you SHOULD NOT do

- Do not attempt to install or run Unity locally in this environment
  - All Unity tests and builds are handled by **GitHub Actions / GameCI**
- Do not push directly to `main`

## Check commands (run these in the container)

```bash
# Lint GitHub Actions, Markdown, YAML
npx --yes markdownlint-cli2 "**/*.md" "#node_modules"
curl -sSfL \
  https://raw.githubusercontent.com/rhysd/actionlint/main/scripts/download-actionlint.bash \
  | bash -s -- -b ./.bin
./.bin/actionlint
pipx install yamllint || pip install --user yamllint
~/.local/bin/yamllint -s .
```

If any lints fail, fix them before creating a PR.

## CI (GameCI)

- Unity tests (`game-ci/unity-test-runner@v4`) and builds (`game-ci/unity-builder@v4`) run in GitHub Actions
- Secrets required: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`
- See `.github/workflows/unity-ci.yml`

## Branch & PR policy

- Branch: `feature/rc-00x-<slug>`（例: `feature/rc-003-gen-pass`）
- PR title: `[RC-00x] <short summary>`
- Include in PR body:
  - Context: link to the issue（例: `RC-003`）
  - What changed: bullet list
  - How to test: scene name, steps
  - Perf note: frame time snapshot if relevant
  - `closes #<issue-number>`
- Request review from code owners

## Security / Internet access

- Cloud tasks run in a sandbox. Leave internet access disabled unless explicitly required
- Never add external binary blobs; use Git LFS for large assets

## How to delegate to Codex

- Start a cloud task from ChatGPT, an IDE integration, or mention `@codex` on GitHub
- For code review on a PR, comment `@codex review`

### Concrete task prompts (examples)

- 「RC-003 GenPass を Issue の指示通りに実装してください。ブランチは
  `feature/rc-003-gen-pass`。Lint を実行し、`closes #<issue-number>` と簡潔な
  テスト計画を含む PR を作成してください。」
- 「RC-005 Composite Pass で深度からワールド座標の復元と平面マスクを追加し、
  README を更新してください。Lint を通し、スクリーンショットを添付した PR
  をお願いします。」

## CIエラー時のCodexCloud連携テンプレート

CodexCloudを使ってCI失敗をリカバリーするときは、以下の定型フローをそのままコピー＆調整して利用してください。

### ステップ1：エラーの確認

- GitHub Actionsのログで失敗したジョブ（GameCIのテスト／ビルド）を確認する
- CodexCloudにはCIログが転送されないため、原因がわかる最小限のログを抜粋しておく
- 抜粋するログには、失敗したブランチ名がわかる一文を添える

例:
```
The build failed with the following Unity error:
Shader error in 'ReflectiveCausticsGen': undeclared identifier '_CameraDepthTexture'
Please fix RC-003 branch (feature/rc-003-gen-pass).
```

### ステップ2：CodexCloudへの修正依頼

CodexCloudでは同じブランチを指定して依頼します。以下のひな形をそのまま貼り付け、必要な箇所を置き換えてください。

```
Fix CI error on branch <branch-name>.

Error log:
<貼り付けたログ抜粋>

Goal:
- <修正してほしいポイントを書き出す>
- Push fix to same branch
```

### ステップ3：PRレビューとマージの進め方

1. PRをオープンしたら `closes #<issue-number>` を必ず本文に含める。CI（lint + Unity test/build）が自動で走る。
2. 内容確認：不要ファイルが含まれないか、AGENTS.mdやIssueのAcceptance Criteriaを満たしているかを目視でチェックする。
3. 追加レビュー：必要に応じてPRコメントで `@codex review` を呼び出し、Codexから改善提案をもらう。
4. 修正ラウンド：自分またはCodexが修正をpushするとCIが再実行される。グリーンになるまで繰り返す。
5. マージ条件：CI OK、目視レビュー OK、（必要なら）`@codex review` OK の状態でマージし、`main` の実行可能性を常に維持する。

### PRごとのチェックリスト

- `closes #<issue-number>` が本文に含まれていること
- 不要な生成物（`Library/`, `Temp/` など）が入っていないこと
- CI（lint / test / build）がグリーンであること
- PR本文に動作確認手順が記載されていること
- Acceptance Criteriaを満たしていること
- `@codex review` を必要に応じて実施済みであること


## Definition of Done (project-wide)

- CI（テスト + Windows ビルド）が成功している
- Issue 固有の受け入れ基準を満たしている
- 新たな警告や秘密情報の混入がなく、差分が最小限である
