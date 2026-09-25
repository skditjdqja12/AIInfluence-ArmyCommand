# Changelog

All notable changes to this project are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versioning: [SemVer](https://semver.org/).

## [Unreleased]

## [1.0.0] - 2026-09-25
### Added
- `create_army` NPC action: a lord raises an army from its kingdom and leads it to a settlement (`siege` / `defend` / `patrol`), summoning named lords or the nearest lords automatically.
- Objective lock: no retargeting, minimum cohesion, food top-up, and blocked "soft" disbands until the objective is achieved. After that the army acts autonomously, and a new goal from dialogue re-locks it.
- `release_army` NPC action (optionally `disband:true`).
- Automatic cancellation when the leader dies or is captured, the army collapses, or the war with the target ends.
- A lord with no party gathers a new party before raising the army.
- Hooks into AI Influence v6.0.2 at runtime (no bridge/proxy required), with a version check that disables the addon on unsupported versions.
- Settings file and log under `Documents\Mount and Blade II Bannerlord\Configs\AIInfluenceArmyCommand\`.
- English and Korean messages.
