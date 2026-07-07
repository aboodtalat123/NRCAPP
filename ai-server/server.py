import os
import json
from contextlib import asynccontextmanager
from dotenv import load_dotenv
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
import traceback
from pydantic import BaseModel
import chromadb
from google import genai
from google.genai import types as genai_types

_BASE = os.path.dirname(os.path.abspath(__file__))
load_dotenv(os.path.join(_BASE, ".env"))

ai = None
collection = None
MODEL = "gemini-3.1-flash-lite"
EMBED_MODEL = "models/gemini-embedding-001"
CHROMA_DIR = os.path.join(_BASE, "chroma_db")
COLLECTION_NAME = "site_content"
SITE_URL = os.getenv("SITE_URL", "https://nrcapp.onrender.com")

def get_embedding(text: str) -> list[float]:
    result = ai.models.embed_content(
        model=EMBED_MODEL,
        contents=[text],
    )
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

@asynccontextmanager
async def lifespan(app: FastAPI):
    try:
        build_chroma()
    except Exception as e:
        print(f"[FATAL] Startup failed: {type(e).__name__}: {e}", flush=True)
        traceback.print_exc()
        raise
    yield

app = FastAPI(title="مساعد نقطة", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

class Question(BaseModel):
    question: str

class Answer(BaseModel):
    answer: str
    sources: list

SYSTEM_PROMPT = """أنت مساعد موقع "نقطة" - مركز تنسيق الإغاثة في غزة.
مهمتك: الإجابة على أسئلة الزوار بناءً فقط على المعلومات المقدمة لك من الموقع.

قواعد صارمة:
1. أجب فقط بناءً على المحتوى المقدم في "المعلومات المتاحة" أدناه.
2. إذا كان السؤال خارج المعلومات المتاحة، قل "هذه المعلومة غير متوفرة في قاعدة معرفتي الحالية."
3. لا تخترع معلومات أو أرقام أو خدمات غير موجودة في النص.
4. استخدم اللغة العربية الفصحى البسيطة.
5. كن مفيداً ومختصراً.
6. إذا سأل الزائر عن التسجيل أو الخدمات، اشرح الخطوات كما هي موجودة في المحتوى.

المعلومات المتاحة من الموقع:
{context}"""

@app.post("/ask", response_model=Answer)
def ask_question(q: Question):
    q_emb = get_embedding(q.question)
    results = collection.query(
        query_embeddings=[q_emb],
        n_results=5
    )

    documents = results["documents"][0] if results["documents"] else []
    metadatas = results["metadatas"][0] if results["metadatas"] else []
    context = "\n\n---\n\n".join(documents)

    prompt = SYSTEM_PROMPT.format(context=context)
    full_prompt = f"{prompt}\n\nسؤال الزائر: {q.question}\n\nإجابتك:"

    try:
        response = ai.models.generate_content(
            model=MODEL,
            contents=[genai_types.Part(text=full_prompt)]
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

@app.get("/health")
def health():
    if collection is None:
        return {"status": "starting", "pages": 0}
    return {"status": "ok", "pages": collection.count()}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
