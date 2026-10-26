"""Validate the real item resources, icon references, alpha and loot configuration."""
import re
from pathlib import Path

import yaml
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def asset(path):
    text = path.read_text(encoding="utf-8")
    text = re.sub(r"^%.*\n", "", text, flags=re.MULTILINE)
    text = re.sub(r"^--- !u!\d+ &-?\d+", "---", text, flags=re.MULTILINE)
    return yaml.safe_load(text)["MonoBehaviour"]


def main():
    all_items = []
    for path in (ROOT / "Assets/Resources/Items").glob("*.asset"):
        data = asset(path)
        if "itemId" in data:
            all_items.append(data)
    assert len({item["itemId"] for item in all_items}) == len(all_items), "Duplicate item IDs"
    expected = [("AttackSpeed", "attack_speed", 2, 20), ("DamageBonus", "damage_bonus", 3, 25),
                ("CriticalRate", "critical_rate", 4, 15)]
    item_guids = set()
    for filename, item_id, item_type, percent in expected:
        path = ROOT / f"Assets/Resources/Items/{filename}.asset"
        data = asset(path)
        assert (data["itemId"], data["itemType"], data["bonusPercent"]) == (item_id, item_type, percent)
        assert not data["isWeapon"] and data["baseWeight"] == 1 and data["maxCount"] == 10
        assert data["displayName"] and data["description"]
        item_guids.add(yaml.safe_load(path.with_name(path.name + ".meta").read_text())["guid"])
        icon_guid = data["icon"]["guid"]
        found = [meta for meta in (ROOT / "Assets/Sprites/Items/Attachments").glob("*.png.meta")
                 if yaml.safe_load(meta.read_text())["guid"] == icon_guid]
        assert len(found) == 1, "Unresolved icon reference"
        metadata = yaml.safe_load(found[0].read_text())["TextureImporter"]
        assert metadata["spriteMode"] == 1 and metadata["textureSettings"]["filterMode"] == 0
        assert metadata["spritePixelsToUnits"] == 2000
        with Image.open(found[0].with_suffix("")) as image:
            assert image.mode == "RGBA" and image.getpixel((0, 0))[3] == 0
            assert image.getchannel("A").getextrema()[1] > 200
        print(f"PASS {data['displayName']}: +{percent}%, unique icon, transparent alpha, stack and weight")

    settings = asset(ROOT / "Assets/Resources/WorldItemSpawnSettings.asset")
    assert {item["guid"] for item in settings["items"]} == item_guids
    assert 0 < settings["spawnChance"] <= 1 and settings["cellSize"] >= 4 and settings["maxSpawnsPerTick"] >= 1
    registered = (ROOT / "Assets/DefaultNetworkPrefabs.asset").read_text()
    assert "1925943fbf5c4b8fbc441056a8b72f18" in registered, "Dropped item prefab must be registered"
    print("PASS seeded loot settings: all three types and registered network prefab")


if __name__ == "__main__":
    main()
