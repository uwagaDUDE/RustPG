# Changelog

## [0.3.0] - 2026-10-15
### Added
- Wooden Staff (`wooden.spear` marked `RPG Staff`): LMB casts, RMB throw blocked
- Basic spells: Fire bolt (1), Heal (2), Ward (3), selected via `/spell 1|2|3`
- Admin: `/rpgstaff give <name>`
- Hooks: `BaseMelee.PlayerAttack`, `ThrownWeapon.ServerThrow` (staff only)
- Ward reduces incoming damage in `RPG_OnAttacked`

### Notes
- Not yet tested in-game (no server restart in this step)

## [0.2.0] - 2026-10-15
### Added
- RU/EN language switch in `/rpg` (`rpg.lang`, per player, stored in data)
- Mana HUD bar above vitals (created once per connection, updated in place)
- Admin: `/rpglvl give|take <name> <n>` (levels), `/tk.reset [name]` (return spent points)
- `/stats` localized

### Fixed
- `/rpg` buttons did nothing: console parser split by space instead of `rpg.<sub>`
- Malformed CUI JSON (trailing comma) in all builders
- Window stacked on language change and on every metabolism tick (FPS drop)
- Chat routing: `/rpgxp` and `/rpglvl` were caught by the `/rpg` branch; now routed by exact first token

### Changed
- `Id()` reads `UserIDString` first; reflection only as fallback
- XP gains and mana ticks mark data dirty; saved every 30s (level-ups and admin commands save immediately)
- `LastAttacker` is pruned every 30s

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
