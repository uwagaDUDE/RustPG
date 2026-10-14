# Changelog

## [0.1.0] - 2026-10-15
### Added
- Character stats system (Strength, Agility, Intelligence)
- XP and leveling system
- Mana pool with INT scaling
- CUI interface (`!rpg`)
- Admin commands: `rpg.xp give/take`, `rpg.add`, `rpg.reset`
- HP bonus from Strength (via StartMaxHealth postfix)
- Melee damage bonus from Strength
- Ranged damage bonus from Agility
- Armor reduction from Agility
- Mana regeneration based on Intelligence
- XP from gathering (DoGather postfix)
- XP from killing players (OnKilledByPlayer postfix)
- XP from killing NPCs (BaseNpc.OnKilled postfix)
- Persistent player data (JSON per steamId)
- Level-down stat rollback (spentOrder stack)
