# AGENTS.md - Reflective Caustics (URP/PC)

> このファイルは Codex Cloud 等のクラウド環境で作業するエージェント向けガイドラインです。

## コミュニケーション / Communication
- この環境で動作するエージェントは、**すべての応答・コメント・PR説明を丁寧な日本語**で記述してください。
- 例外が必要な場合（外部仕様書などで英語必須）は Issue や PR に明記してください。

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
curl -sSfL https://raw.githubusercontent.com/rhysd/actionlint/main/scripts/download-actionlint.bash | bash -s -- -b ./.bin
./.bin/actionlint
pipx install yamllint || pip install --user yamllint
~/.local/bin/yamllint -s .
```

If any lints fail, fix them before creating a PR.

## CI (GameCI)
- Unity tests (game-ci/unity-test-runner@v4) and builds (game-ci/unity-builder@v4) run in GitHub Actions
- Secrets required: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`
- See `.github/workflows/unity-ci.yml`

## Branch & PR policy
- Branch: `feature/rc-00x-<slug>` (for example `feature/rc-003-gen-pass`)
- PR title: `[RC-00x] <short summary>`
- Include in PR body:
  - Context: link to the issue (for example RC-003)
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
- "Implement RC-003 GenPass as per the issue. Branch: feature/rc-003-gen-pass. Run lints; open a PR with `closes #<issue-number>` and a short test plan."
- "For RC-005 Composite Pass, add depth-to-world reconstruction and plane masking. Update README with usage. Run lints; include screenshots in the PR."

## Definition of Done (project-wide)
- CI (tests plus Windows build) succeeds
- Issue-specific acceptance criteria are met
- No new warnings, no secrets committed, diffs kept minimal
