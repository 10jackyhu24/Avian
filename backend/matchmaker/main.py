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


@dataclass
class RoomRecord:
    room_id: str
    room_mode: str
    status: str
    seed: int
    world_endpoint: str
    players: list
    selected_characters: dict
    server_name: str
    node_port: int | None = None


@dataclass
class RoomServerAllocation:
    room_mode: str
    status: str
    world_endpoint: str
    server_name: str
    node_port: int | None = None


queue = deque()
next_room_id = 1
next_seed = 100000
next_node_port = None
queue_lock = Lock()
matches_by_user_id = {}
rooms_by_id = {}


def get_room_mode():
    room_mode = os.getenv("ROOM_MODE", "static").strip().lower()
    if room_mode not in ("static", "k8s"):
        raise HTTPException(
            status_code=500,
            detail=f"Unsupported ROOM_MODE: {room_mode}",
        )

    return room_mode


def get_world_endpoint():
    return os.getenv("WORLD_ENDPOINT", "127.0.0.1:30080")


def get_static_room_seed():
    raw_seed = os.getenv("STATIC_ROOM_SEED", "").strip()
    if not raw_seed:
        return None

    try:
        return int(raw_seed)
    except ValueError:
        raise HTTPException(
            status_code=500,
            detail="STATIC_ROOM_SEED must be an integer",
        )


def get_int_env(name, default_value):
    raw_value = os.getenv(name, str(default_value)).strip()

    try:
        return int(raw_value)
    except ValueError:
        raise HTTPException(
            status_code=500,
            detail=f"{name} must be an integer",
        )


def get_room_endpoint_host():
    return os.getenv("ROOM_ENDPOINT_HOST", "127.0.0.1").strip() or "127.0.0.1"


def get_room_node_port_start():
    return get_int_env("ROOM_NODE_PORT_START", 30081)


def get_room_node_port_end():
    return get_int_env("ROOM_NODE_PORT_END", 30084)


def get_room_image():
    return os.getenv("ROOM_IMAGE", "avian-dedicated-server:v1").strip() or "avian-dedicated-server:v1"


def get_room_namespace():
    return os.getenv("ROOM_NAMESPACE", "default").strip() or "default"


def get_room_game_port():
    return get_int_env("ROOM_GAME_PORT", 7777)


def allocate_room_id():
    global next_room_id

    room_id = f"room-{next_room_id}"
    next_room_id += 1
    return room_id


def allocate_room_seed():
    global next_seed

    static_room_seed = get_static_room_seed()
    if static_room_seed is not None:
        return static_room_seed

    room_seed = next_seed
    next_seed += 1
    return room_seed


def allocate_static_room_server():
    return RoomServerAllocation(
        room_mode="static",
        status="ready",
        world_endpoint=get_world_endpoint(),
        server_name="static-world-server",
    )


def allocate_node_port():
    global next_node_port

    start_port = get_room_node_port_start()
    end_port = get_room_node_port_end()
    if start_port > end_port:
        raise HTTPException(
            status_code=500,
            detail="ROOM_NODE_PORT_START must be less than or equal to ROOM_NODE_PORT_END",
        )

    if next_node_port is None:
        next_node_port = start_port

    if next_node_port > end_port:
        raise HTTPException(
            status_code=503,
            detail="No room server ports available",
        )

    node_port = next_node_port
    next_node_port += 1
    return node_port


def allocate_k8s_room_server(room_id, room_seed):
    from k8s_rooms import K8sRoomSettings, create_room_server

    node_port = allocate_node_port()
    settings = K8sRoomSettings(
        namespace=get_room_namespace(),
        image=get_room_image(),
        game_port=get_room_game_port(),
    )
    room_server = create_room_server(
        room_id=room_id,
        room_seed=room_seed,
        node_port=node_port,
        settings=settings,
    )

    return RoomServerAllocation(
        room_mode="k8s",
        status="starting",
        world_endpoint=f"{get_room_endpoint_host()}:{room_server.node_port}",
        server_name=room_server.server_name,
        node_port=room_server.node_port,
    )


def allocate_room_server(room_id, room_seed):
    room_mode = get_room_mode()

    if room_mode == "static":
        return allocate_static_room_server()

    if room_mode == "k8s":
        return allocate_k8s_room_server(room_id, room_seed)

    raise HTTPException(
        status_code=500,
        detail=f"Unsupported ROOM_MODE: {room_mode}",
    )


def create_match_room(player_a, player_b):
    room_id = allocate_room_id()
    room_seed = allocate_room_seed()
    allocation = allocate_room_server(room_id, room_seed)

    room = RoomRecord(
        room_id=room_id,
        room_mode=allocation.room_mode,
        status=allocation.status,
        seed=room_seed,
        world_endpoint=allocation.world_endpoint,
        players=[asdict(player_a), asdict(player_b)],
        selected_characters={},
        server_name=allocation.server_name,
        node_port=allocation.node_port,
    )

    rooms_by_id[room_id] = room
    return room


def build_match_payload(room):
    return {
        "status": "matched",
        "room_mode": room.room_mode,
        "room_id": room.room_id,
        "seed": room.seed,
        "players": room.players,
        "world_endpoint": room.world_endpoint,
        "selected_characters": room.selected_characters,
    }


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


@app.get("/match/rooms")
def list_rooms():
    with queue_lock:
        return {
            "room_count": len(rooms_by_id),
            "rooms": [asdict(room) for room in rooms_by_id.values()],
        }


@app.get("/match/rooms/{room_id}")
def room_status(room_id: str):
    with queue_lock:
        if room_id not in rooms_by_id:
            raise HTTPException(status_code=404, detail="Room not found")

        return asdict(rooms_by_id[room_id])


@app.post("/match/join")
def join_match(payload: JoinRequest):
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
            room = create_match_room(player_a, player_b)
            matches_by_user_id[player_a.user_id] = room.room_id
            matches_by_user_id[player_b.user_id] = room.room_id
            return build_match_payload(room)

        return {"status": "queued", "queue_size": len(queue)}


@app.get("/match/status/{user_id}")
def match_status(user_id: int):
    with queue_lock:
        if user_id in matches_by_user_id:
            return build_match_payload(get_match_for_user(user_id))

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
        room = get_match_for_user(payload.user_id)
        room.selected_characters[str(payload.user_id)] = payload.character_id
        return build_character_selection_status(room)


@app.post("/match/character-selection/cancel")
def cancel_character_selection(payload: CharacterSelectionCancelRequest):
    with queue_lock:
        room = get_match_for_user(payload.user_id)
        room.selected_characters.pop(str(payload.user_id), None)
        return build_character_selection_status(room)


@app.get("/match/character-selection/status/{user_id}")
def character_selection_status(user_id: int):
    with queue_lock:
        room = get_match_for_user(user_id)
        return build_character_selection_status(room)


def get_match_for_user(user_id: int):
    if user_id not in matches_by_user_id:
        raise HTTPException(status_code=404, detail="Match request not found")

    room_id = matches_by_user_id[user_id]
    if room_id not in rooms_by_id:
        raise HTTPException(status_code=404, detail="Room not found")

    return rooms_by_id[room_id]


def build_character_selection_status(room):
    selected_characters = room.selected_characters
    players = room.players
    all_ready = len(players) > 0 and all(
        str(player["user_id"]) in selected_characters
        for player in players
    )

    return {
        "status": "ready" if all_ready else "waiting",
        "all_ready": all_ready,
        "room_id": room.room_id,
        "seed": room.seed,
        "world_endpoint": room.world_endpoint,
        "selected_count": len(selected_characters),
        "player_count": len(players),
    }
