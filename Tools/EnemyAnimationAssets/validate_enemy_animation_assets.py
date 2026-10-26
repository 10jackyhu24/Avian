"""Validate sheet slicing, sprite isolation, clip links and Animator references."""

import re
from pathlib import Path

import cv2
import numpy as np
import yaml
from PIL import Image
from shapely.geometry import Polygon

from build_enemy_animation_assets import CHARACTERS, ENEMIES, ROOT, STATES, guid


def unity_documents(path):
    text = path.read_text(encoding="utf-8")
    text = re.sub(r"^%.*\n", "", text, flags=re.MULTILINE)
    text = re.sub(r"^--- !u!\d+ &-?\d+", "---", text, flags=re.MULTILINE)
    return list(yaml.safe_load_all(text))


def main():
    prefab = (ROOT / "Assets/Prefabs/Enemies/Enemy.prefab").read_text()
    original_guids = {"Raider": "2769b56a674c49afa51047045989ce11",
                      "Spitter": "43274b3e5cd14b37bbcd05eb52c95e66",
                      "ChargingWarden": "f5d0c617f139455b9aac60c0aee33f71"}
    for enemy in ENEMIES:
        directory = CHARACTERS / enemy
        assert original_guids[enemy] in (directory / f"{enemy}.png.meta").read_text(), "Original GUID changed"
        sheet = directory / f"{enemy}_animations.png"
        metadata = yaml.safe_load(sheet.with_name(sheet.name + ".meta").read_text())["TextureImporter"]
        sprites = metadata["spriteSheet"]["sprites"]
        assert len(sprites) == 12
        assert metadata["spriteMeshType"] == 0 and metadata["textureSettings"]["filterMode"] == 0
        sprite_ids = {sprite["internalID"] for sprite in sprites}
        assert len(sprite_ids) == 12
        with Image.open(sheet) as image:
            alpha = np.array(image.getchannel("A"))
        _, labels, stats, _ = cv2.connectedComponentsWithStats((alpha > 64).astype(np.uint8), 8)
        main_labels = [index for index in range(1, len(stats)) if stats[index, 4] > 2000]
        foot_anchors = []
        for sprite in sprites:
            rect = sprite["rect"]
            x, y, width, height = (int(rect[key]) for key in ("x", "y", "width", "height"))
            top = alpha.shape[0] - y - height
            mask = np.zeros((height, width), dtype=np.uint8)
            polygon = np.array([[round(point["x"] + width * 0.5),
                                 round(height * 0.5 - point["y"])] for point in sprite["outline"][0]],
                               dtype=np.int32)
            assert Polygon(polygon).is_valid, f"{sprite['name']} has an invalid mesh outline"
            cv2.fillPoly(mask, [polygon], 1)
            selected = labels[top:top + height, x:x + width][mask != 0]
            counts = np.bincount(selected, minlength=len(stats))
            own = max(main_labels, key=lambda index: counts[index])
            foreign = sum(counts[index] for index in main_labels if index != own)
            assert foreign == 0, f"{sprite['name']} contains pixels from another frame: {foreign}"
            coverage = counts[own] / stats[own, 4]
            assert coverage > 0.99, f"{sprite['name']} cuts off character pixels: {coverage:.3%}"
            foot_anchors.append(height * sprite["pivot"]["y"] / metadata["spritePixelsToUnits"])
        assert max(foot_anchors) - min(foot_anchors) < 0.0001, "Feet will jump between frames"

        clip_guids = set()
        for index, state in enumerate(STATES):
            path = directory / "Animations" / f"{enemy}_{state}.anim"
            clip = unity_documents(path)[0]["AnimationClip"]
            keys = clip["m_PPtrCurves"][0]["curve"]
            assert [key["value"]["fileID"] for key in keys[:4]] == list(range(21310000 + index * 4, 21310004 + index * 4))
            assert len(keys) == 5 and keys[-1]["time"] == clip["m_AnimationClipSettings"]["m_StopTime"]
            assert clip["m_AnimationClipSettings"]["m_LoopTime"] == (state != "attack")
            for key in keys:
                assert key["value"]["fileID"] in sprite_ids and key["value"]["guid"] == guid(sheet)
            clip_guids.add(guid(path))

        controller = directory / f"AC_{enemy}.controller"
        docs = unity_documents(controller)
        states = [doc["AnimatorState"] for doc in docs if "AnimatorState" in doc]
        transitions = [doc["AnimatorStateTransition"] for doc in docs if "AnimatorStateTransition" in doc]
        assert len(states) == 3 and len(transitions) == 6
        assert {state["m_Motion"]["guid"] for state in states} == clip_guids
        assert guid(controller) in prefab, "Prefab is not using this Animator"
        print(f"PASS {enemy}: 12 isolated frames, aligned feet, 3 clips, 6 transitions, prefab link")
    assert not (CHARACTERS / "NewEnemies").exists()
    print("PASS folder migration: original texture GUIDs preserved; no obsolete NewEnemies folder")


if __name__ == "__main__":
    main()
