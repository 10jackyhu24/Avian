# Enemy sprite simplification

Generated with the built-in image_gen tool; no CLI/API fallback.

Target: simpler hand-drawn pixel art matching the existing eagle and Medusa; each animation frame uses a logical grid no larger than 80 x 80 pixels. Preserve character identity, colors, poses, animation order, sprite IDs, pivots and original slice rectangles.

Original PNGs and import settings: `../EnemySpriteOriginals/`.

## Deliverables and integration

- `Raider_80px.png`, `Spitter_80px.png`, `ChargingWarden_80px.png`: editable 320 x 240 sheets, four columns and three rows of 80 x 80 cells. Top row idle, middle row movement, bottom row attack. Characters fit within 78 x 78 pixels, leaving a transparent margin.
- Each character shares a sixteen-color palette across all twelve frames, without dithering. Alpha is binary for clean pixel edges.
- The matching `_coarse.png` files are enlarged with nearest-neighbor sampling and repacked into the original 1448 x 1086 slice rectangles. Slightly overlapping old rectangles are kept clear of neighboring artwork.
- The three `Assets/Sprites/Characters/<name>/<name>_animations.png` files were replaced with the repacked sheets. The original sprite GUIDs, IDs, names, pivots, PPU and animation clips remain intact.
- Max Size is restored to 2048 in both importer locations, avoiding an additional filtered reduction of the deliberately coarse pixels. Point filtering, disabled mipmaps and uncompressed texture settings are retained.
- `Source/` contains the original built-in image generation results. The final production assets use the normalized 80-pixel frames and shared palette described above.
- Original backups include the prior 512 Max Size settings. Restore both the PNG and its matching metadata to fully return to the pre-redraw state.

## Final prompts

### Raider

Use case: style-transfer. Asset type: production Unity 2D enemy animation sprite sheet.
Input image 1 is the EDIT TARGET: Raider: orange-and-cream aggressive bird, swept orange crest, hooked dark beak, golden talons, broad wings. Keep the exact poses and facing directions in the source. Input images 2 and 3 are STYLE REFERENCES ONLY: the user's simple hand-drawn eagle and Medusa sprites. Match their restrained amateur hand-drawn retro sprite simplicity, NOT the target's current sophisticated illustration.
Redraw the target sheet as deliberately SIMPLE COARSE PIXEL ART, each character frame designed at approximately 80x80 logical pixels. Dramatically simplify shapes: merge feather strands into a few large flat feather clusters, remove tiny ornaments, cracks, metallic reflections, intricate shading and decorative texture. Only 2 or 3 flat shades per material, roughly 12-18 colors per entire character, sparse large pixel clusters, sharp one-logical-pixel dark outlines. Use the reference sprites' modest simple drawing style. Avoid detailed commercial pixel illustration, smooth rendered shading, gradients, anti-aliasing, microtexture and dithering. This must be a new simpler drawing, not merely a blurry or pixelated version of the detailed source.
LAYOUT INVARIANTS: exactly 12 complete separate frames in the SAME 4 columns x 3 rows in the target. Top row = idle 4 frames, middle row = movement 4 frames, bottom row = attack 4 frames. Frame n must preserve source frame n's pose, direction and distinctive motion; do not duplicate one pose throughout a row. Keep all characters completely inside their corresponding cells, with clear transparent gaps and comfortable padding; no parts from adjacent cells may touch. Keep the source canvas 4:3 aspect ratio, preferably 1448x1086; preserve normalized frame locations and relative character sizes. The full bodies, beaks, wing tips, crests and feet must remain visible, no cropping.
Keep each enemy's recognizable silhouette, main colors and design features, but represent them with much fewer marks. No writing, numbers, UI, labels, grid lines or props. Actual transparent background with clean alpha, not a painted checkerboard or solid backdrop.

### Spitter

Use case: style-transfer. Asset type: production Unity 2D enemy animation sprite sheet.
Input image 1 is the EDIT TARGET: Spitter: round teal-green bird, yellow-green inflated spotted throat sac, pale belly, hooked olive beak, small crest, green feet. Keep the exact poses and facing directions in the source. Input images 2 and 3 are STYLE REFERENCES ONLY: the user's simple hand-drawn eagle and Medusa sprites. Match their restrained amateur hand-drawn retro sprite simplicity, NOT the target's current sophisticated illustration.
Redraw the target sheet as deliberately SIMPLE COARSE PIXEL ART, each character frame designed at approximately 80x80 logical pixels. Dramatically simplify shapes: merge feather strands into a few large flat feather clusters, remove tiny ornaments, cracks, metallic reflections, intricate shading and decorative texture. Only 2 or 3 flat shades per material, roughly 12-18 colors per entire character, sparse large pixel clusters, sharp one-logical-pixel dark outlines. Use the reference sprites' modest simple drawing style. Avoid detailed commercial pixel illustration, smooth rendered shading, gradients, anti-aliasing, microtexture and dithering. This must be a new simpler drawing, not merely a blurry or pixelated version of the detailed source.
LAYOUT INVARIANTS: exactly 12 complete separate frames in the SAME 4 columns x 3 rows in the target. Top row = idle 4 frames, middle row = movement 4 frames, bottom row = attack 4 frames. Frame n must preserve source frame n's pose, direction and distinctive motion; do not duplicate one pose throughout a row. Keep all characters completely inside their corresponding cells, with clear transparent gaps and comfortable padding; no parts from adjacent cells may touch. Keep the source canvas 4:3 aspect ratio, preferably 1448x1086; preserve normalized frame locations and relative character sizes. The full bodies, beaks, wing tips, crests and feet must remain visible, no cropping.
Keep each enemy's recognizable silhouette, main colors and design features, but represent them with much fewer marks. No writing, numbers, UI, labels, grid lines or props. Actual transparent background with clean alpha, not a painted checkerboard or solid backdrop.

### ChargingWarden

Use case: style-transfer. Asset type: production Unity 2D enemy animation sprite sheet.
Input image 1 is the EDIT TARGET: Charging Warden: stocky dark navy bird knight, gold-trimmed horned helmet, simple shoulder armor and chest plate, gold legs and dark talons. Keep the exact poses and facing directions in the source. Input images 2 and 3 are STYLE REFERENCES ONLY: the user's simple hand-drawn eagle and Medusa sprites. Match their restrained amateur hand-drawn retro sprite simplicity, NOT the target's current sophisticated illustration.
Redraw the target sheet as deliberately SIMPLE COARSE PIXEL ART, each character frame designed at approximately 80x80 logical pixels. Dramatically simplify shapes: merge feather strands into a few large flat feather clusters, remove tiny ornaments, cracks, metallic reflections, intricate shading and decorative texture. Only 2 or 3 flat shades per material, roughly 12-18 colors per entire character, sparse large pixel clusters, sharp one-logical-pixel dark outlines. Use the reference sprites' modest simple drawing style. Avoid detailed commercial pixel illustration, smooth rendered shading, gradients, anti-aliasing, microtexture and dithering. This must be a new simpler drawing, not merely a blurry or pixelated version of the detailed source.
LAYOUT INVARIANTS: exactly 12 complete separate frames in the SAME 4 columns x 3 rows in the target. Top row = idle 4 frames, middle row = movement 4 frames, bottom row = attack 4 frames. Frame n must preserve source frame n's pose, direction and distinctive motion; do not duplicate one pose throughout a row. Keep all characters completely inside their corresponding cells, with clear transparent gaps and comfortable padding; no parts from adjacent cells may touch. Keep the source canvas 4:3 aspect ratio, preferably 1448x1086; preserve normalized frame locations and relative character sizes. The full bodies, beaks, wing tips, crests and feet must remain visible, no cropping.
Keep each enemy's recognizable silhouette, main colors and design features, but represent them with much fewer marks. No writing, numbers, UI, labels, grid lines or props. Actual transparent background with clean alpha, not a painted checkerboard or solid backdrop.

