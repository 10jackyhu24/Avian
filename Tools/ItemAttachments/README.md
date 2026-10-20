# Weapon attachments

- Wind Feather: attack_speed, attack speed +20%.
- Power Fang: damage_bonus, damage +25%.
- Keen Eye: critical_rate, critical chance +15 percentage points; critical hits deal 2x damage.

Edit bonusPercent, displayName, description and icon on the ItemObject assets in Assets/Resources/Items. Only an attachment installed on the currently selected weapon applies. Moving, detaching or dropping it updates the next attack. Attack data assets are never modified by these bonuses.

Attack speed uses cooldown / (1 + bonusPercent / 100); +20% speed changes a 1-second cooldown to 0.833 seconds. Melee sweep and weapon/bow visual durations use the same value. The server snapshots damage and one critical roll per accepted attack, so an in-flight arrow or ongoing sweep keeps its original damage.

Random ground loot is configured in Assets/Resources/WorldItemSpawnSettings.asset. A server-only spawner processes seeded cells around loaded player positions, checks terrain clearance, then registers ordinary NetworkDroppedItems. A cell is processed once; picking up an item prevents it from being regenerated when returning. Existing dropped-item streaming preserves uncollected items across terrain unloads. Default: 12-unit cells, 40% chance per cell, 36-unit scan radius, at most 6 spawns per second, one item per spawn.

Both clients and servers must be rebuilt together for the new item resources and combat RPC arguments.

## Checks

`dotnet run --project Tools/ItemAttachments/Tests/AttachmentChecks.csproj` runs the production item and modifier sources with minimal managed Unity substitutes. It covers speed/damage, critical thresholds, independent weapons, invalid types, detach behavior and UI/server agreement; it is not a Play Mode/network test.

`python Tools/ItemAttachments/validate_assets.py` verifies resource IDs, configured percentages, icon GUIDs/transparency, stacking, loot settings and network-prefab registration (requires Pillow and PyYAML).

## Icon generation

The three transparent PNG icons were generated with the built-in imagegen tool. PNGs are stored in Assets/Sprites/Items/Attachments. Point filtering and 2000 pixels per unit keep the high-resolution art at a suitable size on the floor.

### attack_speed

Use case: stylized-concept. Asset type: transparent inventory icon for Avian, a small pixel-art game. Draw exactly ONE Wind Feather weapon attachment: a sleek cyan-and-ivory feather held by a small gold clasp, with a clear lightning-shaped notch that communicates attack speed. Handcrafted chunky pixel art, limited palette, dark navy outline, crisp silhouette readable at 48 pixels, three-quarter view. One centered icon fills 75% of square canvas with clear padding. Actual transparent alpha background; no text, no numbers, no UI border, no scene, no ground shadow, no watermark, no other items.

### damage_bonus

Use case: stylized-concept. Asset type: transparent inventory icon for Avian, a small pixel-art game. Draw exactly ONE Power Fang weapon attachment: a sturdy red-and-ivory curved monster fang set in a small worn gold mount, a bold sharp silhouette suggesting increased attack damage. Handcrafted chunky pixel art, limited palette, dark navy outline, crisp silhouette readable at 48 pixels, three-quarter view. One centered icon fills 75% of square canvas with clear padding. Actual transparent alpha background; no text, no numbers, no UI border, no scene, no ground shadow, no watermark, no other items.

### critical_rate

Use case: stylized-concept. Asset type: transparent inventory icon for Avian, a small pixel-art game. Draw exactly ONE Keen Eye weapon attachment: a faceted violet crystal eye charm set in a small gold rim, a bright golden slit pupil and a small star-shaped glint integrated into the eye, clearly suggesting critical hit chance. Handcrafted chunky pixel art, limited palette, dark navy outline, crisp silhouette readable at 48 pixels, three-quarter view. One centered icon fills 75% of square canvas with clear padding. Actual transparent alpha background; no text, no numbers, no UI border, no scene, no ground shadow, no watermark, no other items.

