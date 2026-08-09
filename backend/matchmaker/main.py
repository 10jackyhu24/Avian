from collections import deque
from dataclasses import dataclass, asdict
import os
from threading import Lock
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field


app = FastAPI(title="Avian Matchmaker", version="1.0.0")


class JoinRequest(BaseModel):
    user_id: int = Field(gt=0)
    username: str = Field(min_length=1, max_length=32)


class CharacterSelectionRequest(BaseModel):
    user_id: int = Field(gt=0)
    character_id: str = Field(min_length=1, max_length=64)


class CharacterSelectionCancelRequest(BaseModel):
    user_id: int = Field(gt=0)


@dataclass
class QueueEntry:
    user_id: int
    username: str


queue = deque()
next_room_id = 1
next_seed = 100000
queue_lock = Lock()
matches_by_user_id = {}


def get_world_endpoint():
    return os.getenv("WORLD_ENDPOINT", "127.0.0.1:30080")


@app.get("/health")
def health():
    return {"ok": True, "queue_size": len(queue)}


@app.get("/match/queue/size")
def queue_size():
    with queue_lock:
        return {"queue_size": len(queue)}


@app.get("/match/queue/user")
def queue_users():
    with queue_lock:
        return {
            "queue_size": len(queue),
            "users": [asdict(entry) for entry in queue],
        }


@app.post("/match/join")
def join_match(payload: JoinRequest):
    global next_room_id, next_seed

    with queue_lock:
        # Re-joining should always start a fresh matchmaking attempt.
        matches_by_user_id.pop(payload.user_id, None)

        # Remove any stale queue entry for the same user before re-adding.
        global queue
        queue = deque(entry for entry in queue if entry.user_id != payload.user_id)

        entry = QueueEntry(user_id=payload.user_id, username=payload.username)
        queue.append(entry)

        if len(queue) >= 2:
            player_a = queue.popleft()
            player_b = queue.popleft()
            room_id = f"room-{next_room_id}"
            next_room_id += 1
            room_seed = next_seed
            next_seed += 1

            match_payload = {
                "status": "matched",
                "room_id": room_id,
                "seed": room_seed,
                "players": [asdict(player_a), asdict(player_b)],
                "world_endpoint": get_world_endpoint(),
                "selected_characters": {},
            }
            matches_by_user_id[player_a.user_id] = match_payload
            matches_by_user_id[player_b.user_id] = match_payload
            return match_payload

        return {"status": "queued", "queue_size": len(queue)}


@app.get("/match/status/{user_id}")
def match_status(user_id: int):
    with queue_lock:
        if user_id in matches_by_user_id:
            return matches_by_user_id[user_id]

        for position, entry in enumerate(queue, start=1):
            if entry.user_id == user_id:
                return {
                    "status": "queued",
                    "queue_size": len(queue),
                    "queue_position": position,
                }

    raise HTTPException(status_code=404, detail="Match request not found")


@app.post("/match/character-selection")
def select_character(payload: CharacterSelectionRequest):
    with queue_lock:
        match_payload = get_match_for_user(payload.user_id)
        selected_characters = match_payload.setdefault("selected_characters", {})
        selected_characters[str(payload.user_id)] = payload.character_id
        return build_character_selection_status(match_payload)


@app.post("/match/character-selection/cancel")
def cancel_character_selection(payload: CharacterSelectionCancelRequest):
    with queue_lock:
        match_payload = get_match_for_user(payload.user_id)
        selected_characters = match_payload.setdefault("selected_characters", {})
        selected_characters.pop(str(payload.user_id), None)
        return build_character_selection_status(match_payload)


@app.get("/match/character-selection/status/{user_id}")
def character_selection_status(user_id: int):
    with queue_lock:
        match_payload = get_match_for_user(user_id)
        return build_character_selection_status(match_payload)


def get_match_for_user(user_id: int):
    if user_id not in matches_by_user_id:
        raise HTTPException(status_code=404, detail="Match request not found")

    return matches_by_user_id[user_id]


def build_character_selection_status(match_payload):
    selected_characters = match_payload.setdefault("selected_characters", {})
    players = match_payload.get("players", [])
    all_ready = len(players) > 0 and all(
        str(player["user_id"]) in selected_characters
        for player in players
    )

    return {
        "status": "ready" if all_ready else "waiting",
        "all_ready": all_ready,
        "room_id": match_payload["room_id"],
        "seed": match_payload["seed"],
        "world_endpoint": match_payload["world_endpoint"],
        "selected_count": len(selected_characters),
        "player_count": len(players),
    }
