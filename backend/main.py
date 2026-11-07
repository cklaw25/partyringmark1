"""THE BROKER - slow path (strategist). FastAPI WebSocket server that asks Claude for one strategic
decision per request. The game never waits on this: if it is slow or down, the game uses fallback rules.

Run:  uvicorn main:app --port 8000      (from the backend/ folder)
Key:  put ANTHROPIC_API_KEY=... in backend/.env  (never commit it)
"""
import json
import os
import time
from pathlib import Path

import anthropic
from fastapi import FastAPI, WebSocket, WebSocketDisconnect

from prompts import SYSTEM_PROMPT, BROKER_TOOL, ACTIONS


def load_env():
    env = Path(__file__).with_name(".env")
    if env.exists():
        for line in env.read_text(encoding="utf-8").splitlines():
            if "=" in line and not line.strip().startswith("#"):
                k, v = line.split("=", 1)
                os.environ.setdefault(k.strip(), v.strip().strip('"'))


load_env()
MODEL = os.environ.get("BROKER_MODEL", "claude-opus-5-5")
EFFORT = os.environ.get("BROKER_EFFORT", "low")
client = anthropic.AsyncAnthropic()
app = FastAPI()
USE_FALLBACKS = True  # server-side refusal fallback; switched off automatically if the SDK/API rejects it


@app.get("/health")
async def health():
    return {"ok": True, "model": MODEL, "effort": EFFORT, "key_set": bool(os.environ.get("ANTHROPIC_API_KEY"))}


async def call_claude(state: dict, history: list) -> dict:
    global USE_FALLBACKS
    user_msg = (
        "Live game state (JSON). Decide your next move by calling broker_decide exactly once.\n"
        + json.dumps({"state": state, "your_recent_decisions": history[-5:]}, separators=(",", ":"))
    )
    kwargs = dict(
        model=MODEL,
        max_tokens=2000,
        system=[{"type": "text", "text": SYSTEM_PROMPT, "cache_control": {"type": "ephemeral"}}],
        tools=[BROKER_TOOL],
        tool_choice={"type": "auto"},
        messages=[{"role": "user", "content": user_msg}],
        extra_body={"output_config": {"effort": EFFORT}},
    )
    if USE_FALLBACKS:
        try:
            resp = await client.beta.messages.create(
                betas=["server-side-fallback-2026-07-01"],
                **{**kwargs, "extra_body": {**kwargs["extra_body"], "fallbacks": "default"}},
            )
        except anthropic.BadRequestError as e:
            print("fallbacks param rejected, retrying without it:", e)
            USE_FALLBACKS = False
            resp = await client.messages.create(**kwargs)
    else:
        resp = await client.messages.create(**kwargs)

    if resp.stop_reason == "refusal":
        raise RuntimeError("model refused")
    for block in resp.content:
        if block.type == "tool_use" and block.name == "broker_decide":
            dec = dict(block.input)
            if dec.get("action") not in ACTIONS:
                raise ValueError(f"bad action {dec.get('action')}")
            return dec
    raise ValueError("no tool call in response")


@app.websocket("/ws")
async def ws_endpoint(ws: WebSocket):
    await ws.accept()
    history: list = []
    try:
        while True:
            msg = json.loads(await ws.receive_text())
            if msg.get("type") != "decide":
                continue
            t0 = time.perf_counter()
            try:
                dec = await call_claude(msg["state"], history)
                dec["source"] = "claude"
                history.append({k: dec.get(k) for k in ("action", "target", "reason", "prediction")}
                               | {"outcome": (msg["state"].get("last_decision") or {}).get("outcome")})
                await ws.send_text(json.dumps({"id": msg["id"], "decision": dec, "model": MODEL,
                                               "server_ms": round((time.perf_counter() - t0) * 1000)}))
            except Exception as e:  # noqa: BLE001 - any failure -> client uses fallback rules
                print("strategist error:", type(e).__name__, e)
                await ws.send_text(json.dumps({"id": msg["id"], "error": f"{type(e).__name__}: {e}"}))
    except WebSocketDisconnect:
        pass
