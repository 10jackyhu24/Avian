"""Build Unity sprite metadata, flipbook clips and controllers from the AI sheets.

Images are only read, never modified. Tight sprite outlines isolate neighbouring
poses even where their rectangular bounds overlap. Run with Pillow, NumPy,
opencv-python and Shapely installed:
python Tools/EnemyAnimationAssets/build_enemy_animation_assets.py
"""

from __future__ import annotations

import hashlib
import json
import re
import uuid
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from shapely.geometry import Polygon


ROOT = Path(__file__).resolve().parents[2]
CHARACTERS = ROOT / "Assets/Sprites/Characters"
ENEMIES = ("Raider", "Spitter", "ChargingWarden")
STATES = ("idle", "movement", "attack")
DURATIONS = (1.0, 0.4, 0.6)
NAMESPACE = uuid.UUID("08217d65-e0b3-4679-a3bc-c787432443e9")
HEADER = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"


def guid(path: Path) -> str:
    return uuid.uuid5(NAMESPACE, path.relative_to(ROOT).as_posix()).hex


def write(path: Path, content: str) -> None:
    content = "\n".join(line.rstrip() for line in content.splitlines()) + "\n"
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_text(encoding="utf-8") != content:
        path.write_text(content, encoding="utf-8", newline="\n")


def folder(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():
        write(meta, f"fileFormatVersion: 2\nguid: {guid(path)}\nfolderAsset: yes\n"
              "DefaultImporter:\n  externalObjects: {}\n  userData:\n"
              "  assetBundleName:\n  assetBundleVariant:\n")


def native_meta(path: Path, file_id: int) -> None:
    write(path.with_name(path.name + ".meta"),
          f"fileFormatVersion: 2\nguid: {guid(path)}\nNativeFormatImporter:\n"
          f"  externalObjects: {{}}\n  mainObjectFileID: {file_id}\n"
          "  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def analyse(enemy: str) -> tuple[list[dict], float]:
    directory = CHARACTERS / enemy
    sheet = directory / f"{enemy}_animations.png"
    with Image.open(sheet) as image:
        width, height = image.size
        alpha = np.array(image.getchannel("A"))
    if alpha[0, 0] != 0:
        raise ValueError(f"{sheet}: transparent background required")
    count, labels, stats, _ = cv2.connectedComponentsWithStats((alpha > 64).astype(np.uint8), 8)
    rows = [[] for _ in STATES]
    for label in range(1, count):
        x, y, w, h, area = map(int, stats[label])
        if area < 2000:
            continue
        row = min(2, int((y + h * 0.5) * 3 / height))
        rows[row].append((label, x, y, w, h))
    if [len(row) for row in rows] != [4, 4, 4]:
        raise ValueError(f"{sheet}: expected four isolated poses in each of three rows")

    original = directory / f"{enemy}.png"
    with Image.open(original) as image:
        ys, _ = np.nonzero(np.array(image.getchannel("A")) > 64)
        original_height = int(ys.max() - ys.min() + 1)
    original_ppu = float(re.search(r"spritePixelsToUnits: ([\d.]+)",
                                  original.with_name(original.name + ".meta").read_text()).group(1))
    idle_height = float(np.median([pose[4] for pose in rows[0]]))
    ppu = round(original_ppu * idle_height / original_height, 4)
    frames = []
    for row, poses in enumerate(rows):
        for column, (label, x, y, w, h) in enumerate(sorted(poses, key=lambda pose: pose[1])):
            left, top = max(0, x - 1), max(0, y - 1)
            right, bottom = min(width, x + w + 1), min(height, y + h + 1)
            rect_width, rect_height = right - left, bottom - top
            contours, _ = cv2.findContours((labels[top:bottom, left:right] == label).astype(np.uint8),
                                          cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
            contour = max(contours, key=cv2.contourArea)
            polygon = cv2.approxPolyDP(contour, 0.45, True).reshape(-1, 2)
            shape = Polygon(polygon)
            if not shape.is_valid:
                # Remove self-touching one-pixel feather spurs before Unity triangulates.
                shape = shape.buffer(0)
                if shape.geom_type == "MultiPolygon":
                    shape = max(shape.geoms, key=lambda part: part.area)
                polygon = np.asarray(shape.exterior.coords[:-1])
            outline = [[round(float(px) - rect_width * 0.5, 3),
                        round(rect_height * 0.5 - float(py), 3)] for px, py in polygon]
            frames.append({
                "name": f"{enemy}_{STATES[row]}_{column}", "file_id": 21310000 + row * 4 + column,
                "rect": [left, height - bottom, rect_width, rect_height],
                # Same foot height in world space across idle, steps and crouching poses.
                "pivot": [0.5, round((idle_height * 0.5 + 1) / rect_height, 6)],
                "outline": outline,
            })
    return frames, ppu


def texture_meta(enemy: str, frames: list[dict], ppu: float) -> None:
    sheet = CHARACTERS / enemy / f"{enemy}_animations.png"
    parts = [f"fileFormatVersion: 2\nguid: {guid(sheet)}\nTextureImporter:\n  internalIDToNameTable:\n"]
    for frame in frames:
        parts.append(f"  - first:\n      213: {frame['file_id']}\n    second: {frame['name']}\n")
    parts.append("  externalObjects: {}\n  serializedVersion: 13\n"
                 "  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n    sRGBTexture: 1\n"
                 "  isReadable: 0\n  textureFormat: 1\n  maxTextureSize: 2048\n"
                 "  textureSettings:\n    serializedVersion: 2\n    filterMode: 0\n    aniso: 1\n"
                 "    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n"
                 "  nPOTScale: 0\n  spriteMode: 2\n  spriteExtrude: 0\n  spriteMeshType: 0\n"
                 f"  alignment: 0\n  spritePivot: {{x: 0.5, y: 0.5}}\n  spritePixelsToUnits: {ppu}\n"
                 "  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n  spriteGenerateFallbackPhysicsShape: 0\n"
                 "  alphaUsage: 1\n  alphaIsTransparency: 1\n  textureType: 8\n  textureShape: 1\n"
                 "  platformSettings:\n  - serializedVersion: 4\n    buildTarget: DefaultTexturePlatform\n"
                 "    maxTextureSize: 2048\n    textureFormat: -1\n    textureCompression: 0\n"
                 "    overridden: 0\n  spriteSheet:\n    serializedVersion: 2\n    sprites:\n")
    for frame in frames:
        x, y, width, height = frame["rect"]
        px, py = frame["pivot"]
        sprite_id = uuid.uuid5(NAMESPACE, frame["name"]).hex
        parts.append(f"    - serializedVersion: 2\n      name: {frame['name']}\n"
                     f"      rect:\n        serializedVersion: 2\n        x: {x}\n        y: {y}\n"
                     f"        width: {width}\n        height: {height}\n      alignment: 9\n"
                     f"      pivot: {{x: {px}, y: {py}}}\n      border: {{x: 0, y: 0, z: 0, w: 0}}\n"
                     "      customData:\n      outline:\n")
        for index, (ox, oy) in enumerate(frame["outline"]):
            parts.append(f"      {'- -' if index == 0 else '  -'} {{x: {ox}, y: {oy}}}\n")
        parts.append("      physicsShape: []\n      tessellationDetail: 0\n      bones: []\n"
                     f"      spriteID: {sprite_id}\n      internalID: {frame['file_id']}\n"
                     "      vertices: []\n      indices:\n      edges: []\n      weights: []\n")
    parts.append("    outline: []\n    customData:\n    physicsShape: []\n    bones: []\n"
                 f"    spriteID: {guid(sheet)}\n    internalID: 0\n    vertices: []\n    indices:\n"
                 "    edges: []\n    weights: []\n    secondaryTextures: []\n    nameFileIdTable:\n")
    for frame in frames:
        parts.append(f"      {frame['name']}: {frame['file_id']}\n")
    parts.append("  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
    write(sheet.with_name(sheet.name + ".meta"), "".join(parts))


def clip(enemy: str, row: int, frames: list[dict]) -> Path:
    path = CHARACTERS / enemy / "Animations" / f"{enemy}_{STATES[row]}.anim"
    texture_guid = guid(CHARACTERS / enemy / f"{enemy}_animations.png")
    duration = DURATIONS[row]
    sequence = frames[row * 4:row * 4 + 4]
    sequence = sequence + [sequence[0] if row < 2 else sequence[-1]]
    refs = [f"{{fileID: {frame['file_id']}, guid: {texture_guid}, type: 3}}" for frame in sequence]
    keys = "".join(f"    - time: {duration * index / 4:.6f}\n      value: {ref}\n"
                   for index, ref in enumerate(refs))
    mapping = "".join(f"    - {ref}\n" for ref in refs)
    content = HEADER + f"""--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {enemy}_{STATES[row]}
  serializedVersion: 7
  m_Legacy: 0
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves: []
  m_PositionCurves: []
  m_ScaleCurves: []
  m_FloatCurves: []
  m_PPtrCurves:
  - serializedVersion: 2
    curve:
{keys}    attribute: m_Sprite
    path:
    classID: 212
    script: {{fileID: 0}}
    flags: 2
  m_SampleRate: 20
  m_WrapMode: 0
  m_Bounds:
    m_Center: {{x: 0, y: 0, z: 0}}
    m_Extent: {{x: 0, y: 0, z: 0}}
  m_ClipBindingConstant:
    genericBindings:
    - serializedVersion: 2
      path: 0
      attribute: 0
      script: {{fileID: 0}}
      typeID: 212
      customType: 23
      isPPtrCurve: 1
      isIntCurve: 0
      isSerializeReferenceCurve: 0
    pptrCurveMapping:
{mapping}  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {{fileID: 0}}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: {duration}
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: {1 if row < 2 else 0}
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 0
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 0
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves: []
  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
"""
    write(path, content)
    native_meta(path, 7400000)
    return path


def controller(enemy: str, clips: list[Path]) -> Path:
    path = CHARACTERS / enemy / f"AC_{enemy}.controller"
    states = [110200001, 110200002, 110200003]
    # source, destination, required bool conditions; attack has priority.
    transitions = [(0, 2, [("IsAttacking", True)]),
                   (0, 1, [("IsMoving", True), ("IsAttacking", False)]),
                   (1, 2, [("IsAttacking", True)]),
                   (1, 0, [("IsMoving", False), ("IsAttacking", False)]),
                   (2, 1, [("IsAttacking", False), ("IsMoving", True)]),
                   (2, 0, [("IsAttacking", False), ("IsMoving", False)])]
    parts = [HEADER, "--- !u!91 &9100000\nAnimatorController:\n  m_ObjectHideFlags: 0\n"
             f"  m_Name: AC_{enemy}\n  serializedVersion: 5\n  m_AnimatorParameters:\n"]
    for name, parameter_type, default in [("IsMoving", 4, 0), ("IsAttacking", 4, 0), ("AttackSpeed", 1, 1)]:
        parts.append(f"  - m_Name: {name}\n    m_Type: {parameter_type}\n    m_DefaultFloat: {default}\n"
                     "    m_DefaultInt: 0\n    m_DefaultBool: 0\n    m_Controller: {fileID: 9100000}\n")
    parts.append("  m_AnimatorLayers:\n  - serializedVersion: 5\n    m_Name: Base Layer\n"
                 "    m_StateMachine: {fileID: 110700001}\n    m_Mask: {fileID: 0}\n    m_Motions: []\n"
                 "    m_Behaviours: []\n    m_BlendingMode: 0\n    m_SyncedLayerIndex: -1\n"
                 "    m_DefaultWeight: 1\n    m_IKPass: 0\n    m_SyncedLayerAffectsTiming: 0\n"
                 "    m_Controller: {fileID: 9100000}\n"
                 "--- !u!1107 &110700001\nAnimatorStateMachine:\n  serializedVersion: 6\n"
                 "  m_ObjectHideFlags: 1\n  m_Name: Base Layer\n  m_ChildStates:\n")
    for index, state in enumerate(states):
        parts.append(f"  - serializedVersion: 1\n    m_State: {{fileID: {state}}}\n"
                     f"    m_Position: {{x: {250 + index * 250}, y: 120, z: 0}}\n")
    parts.append("  m_ChildStateMachines: []\n  m_AnyStateTransitions: []\n  m_EntryTransitions: []\n"
                 "  m_StateMachineTransitions: {}\n  m_StateMachineBehaviours: []\n"
                 "  m_AnyStatePosition: {x: 40, y: 20, z: 0}\n  m_EntryPosition: {x: 40, y: 120, z: 0}\n"
                 "  m_ExitPosition: {x: 1050, y: 120, z: 0}\n"
                 "  m_ParentStateMachinePosition: {x: 1050, y: 20, z: 0}\n"
                 f"  m_DefaultState: {{fileID: {states[0]}}}\n")
    for index, state in enumerate(states):
        parts.append(f"--- !u!1102 &{state}\nAnimatorState:\n  serializedVersion: 6\n"
                     f"  m_ObjectHideFlags: 1\n  m_Name: {STATES[index].title()}\n  m_Speed: 1\n"
                     "  m_CycleOffset: 0\n  m_Transitions:\n")
        for transition_index, (source, _, _) in enumerate(transitions):
            if source == index:
                parts.append(f"  - {{fileID: {110100001 + transition_index}}}\n")
        parts.append("  m_StateMachineBehaviours: []\n  m_Position: {x: 50, y: 50, z: 0}\n"
                     "  m_IKOnFeet: 0\n  m_WriteDefaultValues: 1\n  m_Mirror: 0\n"
                     f"  m_SpeedParameterActive: {1 if index == 2 else 0}\n"
                     "  m_MirrorParameterActive: 0\n  m_CycleOffsetParameterActive: 0\n"
                     "  m_TimeParameterActive: 0\n"
                     f"  m_Motion: {{fileID: 7400000, guid: {guid(clips[index])}, type: 2}}\n"
                     f"  m_SpeedParameter: {'AttackSpeed' if index == 2 else ''}\n")
    for index, (_, destination, conditions) in enumerate(transitions):
        parts.append(f"--- !u!1101 &{110100001 + index}\nAnimatorStateTransition:\n"
                     "  m_ObjectHideFlags: 1\n  m_Name:\n  m_Conditions:\n")
        for parameter, value in conditions:
            parts.append(f"  - m_ConditionMode: {1 if value else 2}\n"
                         f"    m_ConditionEvent: {parameter}\n    m_EventTreshold: 0\n")
        parts.append(f"  m_DstStateMachine: {{fileID: 0}}\n  m_DstState: {{fileID: {states[destination]}}}\n"
                     "  m_Solo: 0\n  m_Mute: 0\n  m_IsExit: 0\n  serializedVersion: 3\n"
                     "  m_TransitionDuration: 0\n  m_TransitionOffset: 0\n  m_ExitTime: 0\n"
                     "  m_HasExitTime: 0\n  m_HasFixedDuration: 1\n  m_InterruptionSource: 0\n"
                     "  m_OrderedInterruption: 1\n  m_CanTransitionToSelf: 0\n")
    write(path, "".join(parts))
    native_meta(path, 9100000)
    return path


def main() -> None:
    manifest = {}
    for enemy in ENEMIES:
        folder(CHARACTERS / enemy)
        folder(CHARACTERS / enemy / "Animations")
        frames, ppu = analyse(enemy)
        texture_meta(enemy, frames, ppu)
        clips = [clip(enemy, row, frames) for row in range(3)]
        asset = controller(enemy, clips)
        sheet = CHARACTERS / enemy / f"{enemy}_animations.png"
        manifest[enemy] = {"controller_guid": guid(asset), "texture_guid": guid(sheet),
                           "sheet_sha256": hashlib.sha256(sheet.read_bytes()).hexdigest(),
                           "ppu": ppu,
                           "frames": [{key: value for key, value in frame.items() if key != "outline"}
                                      for frame in frames]}
        print(f"{enemy}: 12 frames, 3 clips, controller {guid(asset)}, PPU={ppu}")
    write(Path(__file__).with_name("animation_manifest.json"), json.dumps(manifest, indent=2) + "\n")


if __name__ == "__main__":
    main()
