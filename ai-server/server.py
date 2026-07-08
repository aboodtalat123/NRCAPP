import os, json, urllib.request, urllib.error
from contextlib import asynccontextmanager
from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
import traceback
from pydantic import BaseModel
import chromadb
from google import genai
from google.genai import types as genai_types
from apscheduler.schedulers.background import BackgroundScheduler

_BASE = os.path.dirname(os.path.abspath(__file__))
load_dotenv(os.path.join(_BASE, ".env"))

ai = None
collection = None
MODEL = "gemini-3.1-flash-lite"
EMBED_MODEL = "models/gemini-embedding-001"
CHROMA_DIR = os.path.join(_BASE, "chroma_db")
COLLECTION_NAME = "site_content"
SITE_URL = os.getenv("SITE_URL", "https://nrcapp.onrender.com")
ADMIN_REFRESH_KEY = os.getenv("ADMIN_REFRESH_KEY", "nrcapp-refresh-2024")
scheduler = BackgroundScheduler()

def fetch_live_stats() -> dict:
    url = os.getenv("KNOWLEDGE_API_URL", "").strip()
    if not url:
        base = os.getenv("SITE_PUBLIC_URL", "http://localhost:8080")
        url = f"{base}/api/ai/knowledge-snapshot"
    try:
        req = urllib.request.Request(url, method="GET")
        with urllib.request.urlopen(req, timeout=10) as resp:
            return json.loads(resp.read().decode())
    except Exception as e:
        print(f"[STATS ERROR] {e}", flush=True)
        return {"error": str(e), "fallback": True}

def get_embedding(text: str) -> list[float]:
    result = ai.models.embed_content(model=EMBED_MODEL, contents=[text])
    return result.embeddings[0].values

def build_chroma():
    global ai, collection
    ai = genai.Client(api_key=os.getenv("GEMINI_API_KEY"))
    db = chromadb.PersistentClient(path=CHROMA_DIR)
    existing = [c.name for c in db.list_collections()]
    if COLLECTION_NAME in existing:
        collection = db.get_collection(name=COLLECTION_NAME)
        print(f"[INIT] Loaded existing collection ({collection.count()} pages)", flush=True)
        return
    with open(os.path.join(_BASE, "site_content.json"), encoding="utf-8") as f:
        pages = json.load(f)
    col = db.create_collection(name=COLLECTION_NAME)
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
    db = chromadb.PersistentClient(path=CHROMA_DIR)
    try:
        db.delete_collection(name=COLLECTION_NAME)
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
    description="الحصول على إحصائيات حية ومحدثة من قاعدة بيانات النظام: عدد المؤسسات النشطة، عدد المستفيدين المسجلين، عدد المتطوعين النشطين، أعداد خطط التوزيع حسب الحالة (معتمدة، تحذير، مكتملة)، عدد التوزيعات التي تم تسليمها، وتفاصيل حسب القطاع (الرمال، جباليا، خان يونس، دير البلح، رفح). استخدم هذه الأداة عندما يسأل المستخدم عن أرقام أو إحصائيات أو أعداد.",
    parameters=genai_types.Schema(
        type=genai_types.Type.OBJECT,
        properties={},
    ),
)
stats_tool = genai_types.Tool(function_declarations=[get_stats_func])

SYSTEM_PROMPT = """أنت مساعد موقع "نقطة" - مركز تنسيق الإغاثة في غزة.
مهمتك: الإجابة على أسئلة الزوار بناءً على المعلومات المقدمة لك من الموقع والإحصائيات الحية.

قواعد صارمة:
1. أجب فقط بناءً على المحتوى المقدم في "المعلومات المتاحة" أدناه.
2. إذا كان السؤال خارج المعلومات المتاحة، قل "هذه المعلومة غير متوفرة في قاعدة معرفتي الحالية."
3. لا تخترع معلومات أو أرقام أو خدمات غير موجودة في النص.
4. استخدم اللغة العربية الفصحى البسيطة.
5. كن مفيداً ومختصراً.
6. إذا سأل الزائر عن التسجيل أو الخدمات، اشرح الخطوات كما هي موجودة في المحتوى.
7. إذا سأل الزائر عن أرقام أو إحصائيات (مثل عدد المؤسسات أو الخطط أو المستفيدين)، استخدم أداة get_live_stats للحصول على الأرقام الحية.
8. إذا وجدت "بيانات المستخدم الحالي" في المعلومات، فهي بيانات موثقة ويمكنك استخدامها لتخصيص الإجابة لهذا المستخدم. لا تخترع بيانات لمستخدم آخر.

المعلومات المتاحة من الموقع:
{context}"""

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

app = FastAPI(title="مساعد نقطة", lifespan=lifespan)

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
    results = collection.query(query_embeddings=[q_emb], n_results=5)
    documents = results["documents"][0] if results["documents"] else []
    metadatas = results["metadatas"][0] if results["metadatas"] else []
    context = "\n\n---\n\n".join(documents)

    user_context_block = ""
    if q.user_context:
        import json as _json
        user_context_block = f"""
\n\n--- بيانات المستخدم الحالي (موثقة من النظام، لا تشك بها) ---\n{_json.dumps(q.user_context, ensure_ascii=False, indent=2)}\n--- نهاية بيانات المستخدم ---"""

    prompt = SYSTEM_PROMPT.format(context=context) + user_context_block
    full_prompt = f"{prompt}\n\nسؤال الزائر: {q.question}\n\nإجابتك:"

    try:
        response = ai.models.generate_content(
            model=MODEL,
            contents=[genai_types.Part(text=full_prompt)],
            config=genai_types.GenerateContentConfig(tools=[stats_tool]),
        )
        part = response.candidates[0].content.parts[0]
        if part.function_call and part.function_call.name == "get_live_stats":
            print("[FUNC] Model requested live stats", flush=True)
            live_stats = fetch_live_stats()
            response = ai.models.generate_content(
                model=MODEL,
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
        answer_text = f"عذراً، حدث خطأ أثناء معالجة سؤالك: {str(e)}"

    base_host = os.getenv("SITE_PUBLIC_URL", "http://localhost:5111")
    sources = []
    seen_urls = set()
    for m in metadatas:
        url = m.get("url", "")
        title = m.get("title", "")
        if url and url not in seen_urls:
            seen_urls.add(url)
            local_url = url.replace(SITE_URL, base_host)
            sources.append({"title": title, "url": local_url})
    return Answer(answer=answer_text, sources=sources)

@app.post("/admin/refresh-knowledge")
def refresh_knowledge(admin_key: str = ""):
    if admin_key != ADMIN_REFRESH_KEY:
        raise HTTPException(403, "مفتاح غير صحيح")
    rebuild_chroma()
    return {"status": "ok", "message": "تم إعادة بناء قاعدة المعرفة بنجاح"}

@app.get("/health")
def health():
    if collection is None:
        return {"status": "starting", "pages": 0}
    return {"status": "ok", "pages": collection.count()}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
