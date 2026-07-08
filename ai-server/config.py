import os, json
from dotenv import load_dotenv

BASE = os.path.dirname(os.path.abspath(__file__))
_CONFIG = None

def load():
    global _CONFIG
    if _CONFIG:
        return _CONFIG

    load_dotenv(os.path.join(BASE, ".env"))

    with open(os.path.join(BASE, "config.json"), encoding="utf-8") as f:
        cfg = json.load(f)

    cfg["_base"] = BASE
    cfg["gemini_api_key"] = os.getenv("GEMINI_API_KEY", "")
    cfg["refresh_key"] = os.getenv("ADMIN_REFRESH_KEY", "")
    cfg["chroma_dir"] = os.path.join(BASE, cfg.get("chroma_dir", "chroma_db"))
    cfg["site_url"] = cfg.get("site_url", "").rstrip("/")

    _CONFIG = cfg
    return cfg

def get(key, default=None):
    return load().get(key, default)

def get_account(role):
    for a in get("accounts", []):
        if a["role"] == role:
            return a
    return None
