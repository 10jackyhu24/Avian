from collections import deque
from dataclasses import dataclass, asdict
from fastapi import FastAPI
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


@app.get("/health")
def health():
    return {"ok": True, "queue_size": len(queue)}


@app.post("/match/join")
def join_match(payload: JoinRequest):
    global next_room_id

    entry = QueueEntry(user_id=payload.user_id, username=payload.username)
    queue.append(entry)

    if len(queue) < 2:
        return {"status": "queued", "queue_size": len(queue)}

    player_a = queue.popleft()
    player_b = queue.popleft()
    room_id = f"room-{next_room_id}"
    next_room_id += 1

    return {
        "status": "matched",
        "room_id": room_id,
        "players": [asdict(player_a), asdict(player_b)],
        "world_endpoint": "http://world-service:5000",
    }
