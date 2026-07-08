"""
أداة بناء قاعدة المعرفة (ChromaDB) من site_content.json
تشغيل: python build_knowledge_base.py
"""
import os, json, sys
from dotenv import load_dotenv
import chromadb
from google import genai

BASE = os.path.dirname(os.path.abspath(__file__))
load_dotenv(os.path.join(BASE, ".env"))

CONTENT_FILE = os.path.join(BASE, "site_content.json")
CHROMA_DIR = os.path.join(BASE, "chroma_db")
COLLECTION_NAME = "site_content"
EMBED_MODEL = "models/gemini-embedding-001"

def main():
    api_key = os.getenv("GEMINI_API_KEY")
    if not api_key:
        print("❌ GEMINI_API_KEY غير موجود في ملف .env")
        sys.exit(1)

    ai = genai.Client(api_key=api_key)
    db = chromadb.PersistentClient(path=CHROMA_DIR)

    try:
        db.delete_collection(name=COLLECTION_NAME)
        print("🗑️  تم حذف قاعدة المعرفة القديمة")
    except Exception:
        pass

    with open(CONTENT_FILE, encoding="utf-8") as f:
        pages = json.load(f)

    if not pages:
        print("❌ site_content.json فارغ")
        sys.exit(1)

    col = db.create_collection(name=COLLECTION_NAME)
    docs, metas, ids, embeddings = [], [], [], []

    print(f"📦 جاري بناء {len(pages)} صفحة...")
    for i, p in enumerate(pages):
        txt = f"الصفحة: {p['title']}\nالرابط: {p['url']}\nالمحتوى:\n{p['content']}"
        docs.append(txt)
        metas.append({"title": p["title"], "url": p["url"], "source": "site_content.json"})
        ids.append(f"page_{i}")
        print(f"  [{i+1}/{len(pages)}] {p['title']}...", end="", flush=True)
        try:
            emb = ai.models.embed_content(model=EMBED_MODEL, contents=[txt])
            embeddings.append(emb.embeddings[0].values)
            print(" ✅")
        except Exception as e:
            print(f" ❌ {e}")
            sys.exit(1)

    col.add(documents=docs, metadatas=metas, ids=ids, embeddings=embeddings)
    print(f"\n✅ تم بناء قاعدة المعرفة بنجاح! {len(pages)} صفحة.")

if __name__ == "__main__":
    main()
