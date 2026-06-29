from fastapi import FastAPI
from pydantic import BaseModel, Field


app = FastAPI(title="Avian World Room", version="1.0.0")


class JoinRoomRequest(BaseModel):
    user_id: int = Field(gt=0)
    username: str = Field(min_length=1, max_length=32)


@app.get("/health")
def health():
    return {"ok": True}


@app.get("/room/{room_id}")
def room_info(room_id: str):
    return {
        "room_id": room_id,
        "status": "ready",
    }


@app.post("/room/{room_id}/join")
def join_room(room_id: str, payload: JoinRoomRequest):
    return {
        "room_id": room_id,
        "status": "joined",
        "user_id": payload.user_id,
        "username": payload.username,
    }
