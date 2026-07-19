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
