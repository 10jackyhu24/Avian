# Enemy animation assets

Each enemy has its own folder under `Assets/Sprites/Characters`:

- `Raider/`: original artwork, animation sheet, `AC_Raider.controller`, and `Animations/`.
- `Spitter/`: the same structure for the poison bird.
- `ChargingWarden/`: the same structure for the charging mini-boss.

Each sheet contains twelve poses: four idle frames, four movement frames and four attack frames. Idle and movement loop; attack plays once. The controllers use `IsMoving`, `IsAttacking` and `AttackSpeed`. `EnemyController` derives movement from the displayed position and synchronizes the attack flag from the server. Attack speed matches windup time, including the charge duration for the Warden.

The original PNG files and their Unity GUIDs were preserved when moved from `NewEnemies`. Generated animation sheets are separate files. Point filtering, no compression, tight sprite outlines and consistent foot pivots keep the frames readable and isolate nearby poses.

To rebuild the Unity metadata, clips and controllers (PNG files are only read, never modified):

```powershell
python -m pip install pillow numpy opencv-python pyyaml shapely
python Tools/EnemyAnimationAssets/build_enemy_animation_assets.py
python Tools/EnemyAnimationAssets/validate_enemy_animation_assets.py
```

## Image generation

All three animation sheets were generated with the built-in imagegen tool, using each enemy's original PNG as the edit reference and requesting a transparent background. Final prompts:

### Raider

Use case: identity-preserve. Edit target: attached Raider bird sprite. Deliver a production game animation SPRITE SHEET of this exact orange/cream hooked-beak Raider. Preserve character identity, colors, dark outline, pixel-art style and LEFT-facing three-quarter view. Genuinely transparent background. EXACTLY 12 full-body frames on a perfectly regular 4-COLUMN by 3-ROW grid; each equal cell has generous clear padding; same character scale, same camera, aligned body center and feet baseline within every cell. Canvas landscape 4:3. NO labels, no grid lines, no captions, no backgrounds, no ground shadows, no projectiles. TOP ROW four idle frames: wings relax, breathing/feathers rise subtly, blink, return. MIDDLE ROW four movement frames: wing upstroke, downstroke, upstroke, downstroke with slightly alternating talons, stable body position. BOTTOM ROW four attack frames: draw head back in anticipation, lean forward opening beak, fierce extended beak bite with wings swept back, recover. Attacks face LEFT. Keep every entire character inside its own cell, no overlap or cropped wings. One consistent character, clear genuine pose differences across animation frames, crisp pixel clusters.

### Spitter

Use case: identity-preserve. Edit target: attached poison Spitter bird. Produce a usable animation SPRITE SHEET of this same teal marsh bird, round bright poison throat sac, short hooked beak, spotted stubby wings and webbed talons. Retain identity and crisp pixel art, LEFT-facing three-quarter view. EXACTLY 4 columns x 3 rows of equal cells on landscape 4:3 canvas, 12 full-body frames. CRITICAL: each character must fit entirely within the CENTER 65% of its own cell; leave WIDE transparent gutters and 17% blank padding around all four sides of EVERY cell so Unity can slice cells without cut-off parts or neighboring pixels. Keep same character size and aligned feet/body baseline across frames. TOP row idle 4 frames breathing and blink, sac gently expands/contracts. MIDDLE row movement 4 frames alternating waddling steps and small wing motions. BOTTOM row attack 4 frames: lean backward, inflate throat sac, open beak wide and thrust head forward to spit, recover. Draw character only, NO actual projectile, no effects. Genuine pose differences but no character redesign. Transparent alpha background, no text, no grid lines, no shadows, no environment. Exactly 12 sprites.

### Charging Warden

Use case: identity-preserve. Edit target: attached Charging Warden bird mini-boss. Create animation SPRITE SHEET of EXACT same heavy slate-feather guardian with bronze/stone armor, broad horned charging crest and thick talons. Preserve identity, armor design, colors, crisp pixel art, LEFT-facing three-quarter view. EXACTLY 12 full-body sprites, arranged perfectly regular 4 COLUMNS x 3 ROWS on 4:3 landscape canvas. Each equal cell holds one complete character, same scale, same camera, aligned body center/feet baseline. IMPORTANT wide transparent empty gutters, each character fits in central 65% of its cell, never touch neighboring frames or canvas edges. TOP ROW idle four frames: steady breathing, slight armor/shoulder motion, blink, return. MIDDLE ROW movement four frames: alternating heavy marching steps and slight wing sway. BOTTOM ROW attack four frames: lift crest and brace, crouch and lower armored forehead, lean forward into a forceful LEFT-facing charge, recover. No handheld weapons, no effects/projectiles. Transparent background, no labels, no text, no grid lines, no floor/shadows. Show genuinely different animation poses while retaining one consistent character.
