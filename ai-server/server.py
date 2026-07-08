import os, json, urllib.request, urllib.error
from contextlib import asynccontextmanager
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
import traceback
from pydantic import BaseModel
import chromadb
from google import genai
from google.genai import types as genai_types
from apscheduler.schedulers.background import BackgroundScheduler

import config as cfg

ai = None
collection = None
scheduler = BackgroundScheduler()

def fetch_live_stats() -> dict:
    url = f"{cfg.get('site_url')}/api/ai/knowledge-snapshot"
    try:
        req = urllib.request.Request(url, method="GET")
        with urllib.request.urlopen(req, timeout=10) as resp:
            return json.loads(resp.read().decode())
    except Exception as e:
        print(f"[STATS ERROR] {e}", flush=True)
        return {"error": str(e), "fallback": True}

def get_embedding(text: str) -> list[float]:
    result = ai.models.embed_content(
        model=cfg.get("embedding_model", "models/gemini-embedding-001"),
        contents=[text],
    )
    return result.embeddings[0].values

def build_chroma():
    global ai, collection
    ai = genai.Client(api_key=cfg.get("gemini_api_key"))
    db = chromadb.PersistentClient(path=cfg.get("chroma_dir"))
    existing = [c.name for c in db.list_collections()]
    col_name = cfg.get("chroma_collection_name", "site_content")
    if col_name in existing:
        collection = db.get_collection(name=col_name)
        print(f"[INIT] Loaded collection ({collection.count()} pages)", flush=True)
        return
    content_file = os.path.join(cfg.get("_base", "."), "site_content.json")
    with open(content_file, encoding="utf-8") as f:
        pages = json.load(f)
    col = db.create_collection(name=col_name)
    docs, metas, ids, embeddings = [], [], [], []
    for i, p in enumerate(pages):
        txt = f"الصفحة: {p['title']}\nالرابط: {p['url']}\nالمحتوى:\n{p['content']}"
        docs.append(txt)
        metas.append({"title": p["title"], "url": p["url"], "source": "site_content.json"})
        ids.append(f"page_{i}")
        print(f"[INIT] Embedding page {i+1}/{len(pages)}: {p['title']}", flush=True)
        embeddings.append(get_embedding(txt))
    col.add(documents=docs, metadatas=metas, ids=ids, embeddings=embeddings)
    collection = col
    print(f"[INIT] Built collection with {len(pages)} pages", flush=True)

def rebuild_chroma():
    global collection
    db = chromadb.PersistentClient(path=cfg.get("chroma_dir"))
    col_name = cfg.get("chroma_collection_name", "site_content")
    try:
        db.delete_collection(name=col_name)
    except Exception:
        pass
    collection = None
    build_chroma()
    print("[REBUILD] ChromaDB rebuilt successfully", flush=True)

def rebuild_chroma_job():
    try:
        rebuild_chroma()
    except Exception as e:
        print(f"[SCHEDULER ERROR] {e}", flush=True)

get_stats_func = genai_types.FunctionDeclaration(
    name="get_live_stats",
    description="الحصول على إحصائيات حية ومحدثة من قاعدة بيانات النظام",
    parameters=genai_types.Schema(
        type=genai_types.Type.OBJECT,
        properties={},
    ),
)
stats_tool = genai_types.Tool(function_declarations=[get_stats_func])

SYSTEM_PROMPT = f"""أنت مساعد "{cfg.get('site_name', 'الموقع')}".
مهمتك: الإجابة على أسئلة الزوار بناءً على المعلومات المقدمة لك.
قواعد صارمة:
1. أجب فقط بناءً على المحتوى المقدم في "المعلومات المتاحة".
2. إذا كان السؤال خارج المعلومات، قل "هذه المعلومة غير متوفرة."
3. استخدم اللغة العربية الفصحى البسيطة.
4. إذا سأل الزائر عن أرقام، استخدم get_live_stats.
5. إذا وجدت "بيانات المستخدم الحالي"، استخدمها لتخصيص الإجابة.

المعلومات المتاحة من الموقع:
{{context}}"""

@asynccontextmanager
async def lifespan(app: FastAPI):
    try:
        build_chroma()
        scheduler.add_job(rebuild_chroma_job, "interval", hours=12, id="rebuild_kb", replace_existing=True)
        scheduler.start()
        print("[SCHEDULER] Started (every 12 hours)", flush=True)
    except Exception as e:
        print(f"[FATAL] Startup failed: {type(e).__name__}: {e}", flush=True)
        traceback.print_exc()
        raise
    yield
    scheduler.shutdown(wait=False)

app = FastAPI(title=cfg.get("site_name", "AI Assistant"), lifespan=lifespan)
app.add_middleware(
    CORSMiddleware, allow_origins=["*"], allow_credentials=True,
    allow_methods=["*"], allow_headers=["*"],
)

class Question(BaseModel):
    question: str
    user_context: dict | None = None

class Answer(BaseModel):
    answer: str
    sources: list

@app.post("/ask", response_model=Answer)
def ask_question(q: Question):
    q_emb = get_embedding(q.question)
    col_name = cfg.get("chroma_collection_name", "site_content")
    db = chromadb.PersistentClient(path=cfg.get("chroma_dir"))
    col = db.get_collection(name=col_name)
    results = col.query(query_embeddings=[q_emb], n_results=5)
    documents = results["documents"][0] if results["documents"] else []
    metadatas = results["metadatas"][0] if results["metadatas"] else []
    context = "\n\n---\n\n".join(documents)

    user_context_block = ""
    if q.user_context:
        user_context_block = f"\n\n--- بيانات المستخدم الحالي ---\n{json.dumps(q.user_context, ensure_ascii=False, indent=2)}\n--- نهاية بيانات المستخدم ---"

    prompt = SYSTEM_PROMPT.format(context=context) + user_context_block
    full_prompt = f"{prompt}\n\nسؤال الزائر: {q.question}\n\nإجابتك:"

    try:
        response = ai.models.generate_content(
            model=cfg.get("gemini_model", "gemini-2.0-flash-lite"),
            contents=[genai_types.Part(text=full_prompt)],
            config=genai_types.GenerateContentConfig(tools=[stats_tool]),
        )
        part = response.candidates[0].content.parts[0]
        if part.function_call and part.function_call.name == "get_live_stats":
            print("[FUNC] Model requested live stats", flush=True)
            live_stats = fetch_live_stats()
            response = ai.models.generate_content(
                model=cfg.get("gemini_model", "gemini-2.0-flash-lite"),
                contents=[
                    genai_types.Content(role="user", parts=[genai_types.Part(text=full_prompt)]),
                    genai_types.Content(role="model", parts=[genai_types.Part(function_call=part.function_call)]),
                    genai_types.Content(role="function", parts=[genai_types.Part(
                        function_response=genai_types.FunctionResponse(name="get_live_stats", response=live_stats),
                    )]),
                ],
                config=genai_types.GenerateContentConfig(tools=[stats_tool]),
            )
        answer_text = response.text
    except Exception as e:
        print(f"[AI ERROR] {type(e).__name__}: {e}", flush=True)
        traceback.print_exc()
        answer_text = f"عذراً، حدث خطأ: {str(e)}"

    sources = []
    seen_urls = set()
    for m in metadatas:
        url = m.get("url", "")
        title = m.get("title", "")
        if url and url not in seen_urls:
            seen_urls.add(url)
            sources.append({"title": title, "url": url})
    return Answer(answer=answer_text, sources=sources)

@app.post("/admin/refresh-knowledge")
def refresh_knowledge(admin_key: str = ""):
    if admin_key != cfg.get("refresh_key", ""):
        raise HTTPException(403, "مفتاح غير صحيح")
    rebuild_chroma()
    return {"status": "ok", "message": "تم إعادة بناء قاعدة المعرفة"}

@app.get("/health")
def health():
    global collection
    if collection is None:
        return {"status": "starting", "pages": 0}
    return {"status": "ok", "pages": collection.count()}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
