# Changelog

## [0.4.2] - 2026-10-15
### Fixed
- Staff spells cast on air swings (hook BaseMelee.ServerUse), not only on hits. A dedupe window (0.25 s) keeps one swing = one cast
- Staff RMB no longer throws the spear: blocked at ThrownWeapon.DoThrow, which is the call that spawns it
- Respawn/spawn health is 50% of the real max (BaseCombatEntity.StartHealth postfix, espawnHealthFraction in config.json)
- Vitals HUD: HP bar against the real max (with STR and Vitality bonuses), MP bar below it

### Added
- Skill tree: click a node to read its name, requirements, description and status in the bottom panel. ОТКРЫТЬ unlocks the selected node
- Multi-line CUI text (\n now escaped as JSON newline in Cui.Esc)

### Notes
- Respawn health also applies on the first spawn after login (same StartHealth call)
- Staff hooks log staff: ServerUse hook active / PlayerAttack hook active / DoThrow hook active on first use, for checking which path fires in-game
## [0.4.1] - 2026-10-15
### Fixed
- Staff item: Rust shortname is `spear.wooden`; creation tries it first, then `wooden.spear`, and logs which one worked
- Max HP bonus (STR, Vitality) did not apply: `BasePlayer` overrides `MaxHealth()`, so the bonus is now added in a `BasePlayer.MaxHealth` postfix. Current HP gets only the gained amount
- `/stats` shows the real game max HP

## [0.4.0] - 2026-10-15
### Added
- Skill tree `/skills`: 12 nodes in 4 branches (General, Warrior, Hunter, Mage), one point every 5 levels (10 total)
- Requirements per node: level, STR/AGI/INT, previous node. Unlock refuses with a reason; clicking a locked node shows it in chat
- `/dash`: 12 m forward dash (requires Dash node), 10 s cooldown, stops at walls
- Staff RMB opens the spell picker (`RustPG_Spell` window) instead of throwing
- Skill effects: vitality (+25 HP), gather XP x1.5, melee +15%, lifesteal (+3 HP per melee hit), ranged +15%, crit (15% x1.5), fire bolt x1.5, fire blast splash, mana regen x1.5, heal x1.5, greater ward
- Ward factor is now per-cast (duration and multiplier depend on the Greater Ward node)
- Skill points are re-validated after level/XP/stat drops (`Skills.Validate`)
- Shared CUI helper `Cui` for new windows (destroy-before-add, no trailing commas)

### Notes
- LMB melee hook is `BaseMelee.PlayerAttack`. The exact class of `wooden.spear` was not confirmed from the DLL; first in-game test decides it
- RMB uses `ThrownWeapon.ServerThrow`

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
