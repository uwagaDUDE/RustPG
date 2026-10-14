# RustPG

Rust server RPG plugin for RustBuddy framework.

## Features
- Character stats: Strength, Agility, Intelligence
- XP system with levels
- Mana pool
- CUI interface (`!rpg`)

## Stats (v0.1)
- **Strength**: +5 HP/pt, +0.1% melee damage/pt
- **Agility**: +0.5% armor/pt (cap 75%), +0.1% ranged damage/pt
- **Intelligence**: +5 mana/pt

## Commands
| Command | Description |
|---------|-------------|
| `!rpg` | Open RPG interface |
| `!stats` | Show current stats in chat |
| `rpg.xp give <name> <amount>` | Give XP (admin) |
| `rpg.xp take <name> <amount>` | Take XP (admin) |

## Install
1. Build: `powershell -ExecutionPolicy Bypass -File build.ps1`
2. Deploy: `powershell -ExecutionPolicy Bypass -File build.ps1 -Deploy`
3. Restart server or reload plugins

## Build
Requires .NET Framework 4.x csc. References: RustBuddy, Assembly-CSharp, Rust.Data, Facepunch.*, UnityEngine.*, Newtonsoft.Json.
