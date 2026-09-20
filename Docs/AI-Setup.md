# AI setup for RPG

## Installed in this repository

- `AGENTS.md`: project conventions, architecture boundaries, validation and single-Unity-MCP policy.
- `.agents/skills`: five adapted upstream Unity skills and seven original project skills.
- `.codex/config.toml` and `.codex/agents`: architect, gameplay implementer, reviewer and QA role configurations. They inherit the chosen model and existing permission settings.
- `Packages/manifest.json`: Unity Pipeline for the existing `unity` MCP connection.

The five upstream skills cover architecture, state machines, data-driven gameplay, save/load and async. Each includes its original guide, license and source reference. The four original skills are `rpg-feature-workflow`, `rpg-systematic-debugging`, `rpg-code-review`, and `rpg-validation`. They provide local workflow capabilities. The additional unity-di-modules, unity-litmotion and rpg-reuse-research skills implement the requested DI/module, tween and reuse policies. Superpowers, Unity Workflows, MVP and DOD plugins have not been installed.

Unity Agent Plugin and the `unity` MCP definition are installed at user scope on this machine. This repository does not duplicate them or carry credentials. On another machine, install the official Unity plugin/CLI and configure a single `unity` MCP for that checkout.

## Use

Start a new Codex session in this trusted project to load the role configuration. Local skills are discoverable from `.agents/skills` and can also be requested explicitly:

- `Dùng $rpg-feature-workflow xây dựng một combat vertical slice nhỏ.`
- `Dùng $rpg-systematic-debugging điều tra lỗi damage bị tính hai lần.`
- `Dùng $rpg-code-review review thay đổi hiện tại, chưa sửa code.`
- `Dùng $rpg-validation kiểm tra thay đổi và báo rõ phần chưa xác minh.`
- `Dùng các agent unity_architect và unity_reviewer chạy song song để phân tích thiết kế combat; chưa sửa code.`

Custom role availability depends on the Codex client loading trusted project config. The current session may need restarting. Role files are configuration, not always-running processes. Editor mutation stays serialized even when code analysis runs in parallel.

## Connection and verification

Open this project in Unity and let package import/compilation finish. Run `unity status --format json` and confirm this project's instance is ready. If not, use `unity pipeline list` and inspect compile logs. Do not add another bridge as a workaround.

Skill structure and TOML parsing are setup checks; gameplay compilation, tests and visual behavior require separate Unity validation. No gameplay system is installed by this setup.

Role configuration follows the [official Codex configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference).

## Last setup check

- The original nine skill entrypoints passed the skill validator; the updated set contains twelve (see current validation below).
- Four role files parsed successfully; `codex --strict-config doctor` loaded the configuration without warnings or failures.
- Only one Unity MCP (`unity`) is configured; the other existing MCP is unrelated to Unity.
- Pipeline installer reported success for `0.7.0-exp.1`. The running RPG Editor was detected, but its Pipeline server was not reachable yet; live Editor automation has not been verified. Let Unity resolve the package and reload; if it stays disconnected, save your work and reopen the project, then repeat `unity status --format json`.

## Ponytail and architecture hooks

Ponytail 4.10.0 is installed from the author's `DietrichGebert/ponytail` marketplace into Codex's shared cache. It is disabled in user config and enabled by this project's `.codex/config.toml`, so other projects do not inherit activation. Its source is not vendored into this repository. Reproduce on another machine with `codex plugin marketplace add DietrichGebert/ponytail` then `codex plugin add ponytail@ponytail`; disable it at user scope if project-only activation is desired. The project override enables it here.

Ponytail supplies coding instructions and lifecycle hooks inside the Codex harness. The installed version has three hook events: SessionStart, SubagentStart and UserPromptSubmit. Its default mode is full unless user environment/config chooses another. Use `@ponytail lite`, `@ponytail full`, `@ponytail off`, or `@ponytail-review` as appropriate. Mode state is maintained by the upstream plugin in PLUGIN_DATA; concurrent sessions can share that plugin state, so do not assume per-thread isolation. Avoid changing its global default just for this project.

Project hooks in `.codex/hooks.json` add architecture/reuse context at session and subagent start. Their Python script reads only the manifest and emits context; it does not edit code, install packages, call the network, scan transcripts or control Unity. These hooks are reminders, not a compiler or semantic architecture enforcement system. The same policy remains in AGENTS.md when hooks are unavailable.

Codex requires review/trust of new hook definitions. Start a new trusted project session, open `/hooks`, and review/trust Ponytail's three hooks and this project's two hooks. Installing a plugin does not grant hook trust. No trust-bypass or approval-policy changes are part of this setup. [Official hook behavior](https://learn.chatgpt.com/docs/hooks).

VContainer, UniTask and LitMotion are the chosen stack for future implementation. At this update they are not in the manifest: skills and hooks do not pretend they are installed. Add verified package versions when implementing the first feature that needs them. Custom ECS and ZLinq are not implied by this choice.

## Current validation

Twelve skill entrypoints passed the skill validator. All relative skill links resolve and role/config TOML parses. Codex strict-config doctor passed. The project hook passed SessionStart/SubagentStart, nested working-directory, malformed-input and outside-project checks. Installed Ponytail hooks passed isolated startup, subagent injection, lite and off mode tests using temporary plugin data. Runtime trust in the real Codex session still requires `/hooks`; these direct script tests do not grant trust. No Unity gameplay code or runtime packages were added in this policy update.
