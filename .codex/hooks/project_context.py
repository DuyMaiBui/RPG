#!/usr/bin/env python3
"""Read-only project context for Codex SessionStart/SubagentStart hooks."""
import json
from pathlib import Path
import sys


def main():
    try:
        payload = json.load(sys.stdin)
    except (ValueError, OSError):
        return
    if not isinstance(payload, dict):
        return
    event = payload.get("hook_event_name")
    if event not in ("SessionStart", "SubagentStart"):
        return
    root = Path(__file__).resolve().parents[2]
    try:
        cwd = Path(payload.get("cwd") or Path.cwd()).resolve()
        cwd.relative_to(root)
    except (TypeError, ValueError, OSError):
        return
    try:
        dependencies = json.loads((root / "Packages/manifest.json").read_text())["dependencies"]
        packages = "; ".join(
            f"{name}: {dependencies.get(name, 'NOT INSTALLED')}"
            for name in ("jp.hadashikick.vcontainer", "com.cysharp.unitask", "com.annulusgames.lit-motion")
        )
    except (OSError, ValueError, KeyError, AttributeError):
        packages = "Manifest unavailable; inspect dependencies before using package APIs."
    context = (
        "RPG project requirements: read AGENTS.md and Docs/Architecture.md. "
        "Use VContainer DI, UniTask for async integration, LitMotion for presentation. "
        "Gameplay core is reusable engine-independent C# with constructor injection; "
        "MonoBehaviour only bridges Unity callbacks, scene references and rendering. "
        "Keep container registration and tween APIs outside domain modules. "
        "Own cancellation, motion handles, event cleanup and pool-return lifetimes. "
        "Before new systems, search existing code, installed APIs and suitable maintained upstream solutions. "
        "Ponytail minimizes unnecessary implementation, never explicit requirements, module boundaries "
        "or meaningful validation. Use only the existing unity MCP. "
        "Manifest status (not a compile result): " + packages
    )
    print(json.dumps({"hookSpecificOutput": {"hookEventName": event, "additionalContext": context}}))


if __name__ == "__main__":
    main()
