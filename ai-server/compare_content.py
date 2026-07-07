import os, json, shutil
from datetime import datetime

BASE = os.path.dirname(os.path.abspath(__file__))
OLD = os.path.join(BASE, "site_content.json")
NEW = os.path.join(BASE, "site_content_new.json")
BACKUP_DIR = os.path.join(BASE, "backups")

os.makedirs(BACKUP_DIR, exist_ok=True)

def load(path):
    if not os.path.exists(path): return [], {}
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    return data, {p.get("url","").rstrip("/"): p for p in data}

print("="*60)
print("  الأداة 2: مقارنة المحتوى - نقطة")
print("="*60)

old_list, old_map = load(OLD)
new_list, new_map = load(NEW)

old_urls = set(old_map.keys())
new_urls = set(new_map.keys())

added = new_urls - old_urls
removed = old_urls - new_urls
common = old_urls & new_urls

same, changed = [], []
for url in common:
    if old_map[url].get("content") == new_map[url].get("content"):
        same.append(url)
    else:
        changed.append(url)

print(f"\n📊 المقارنة:")
print(f"   القديم: {len(old_list)} صفحة")
print(f"   الجديد: {len(new_list)} صفحة")
print(f"   📗 جديد: {len(added)}")
print(f"   📝 متغير: {len(changed)}")
print(f"   📄 متطابق: {len(same)}")
print(f"   🗑️ محذوف: {len(removed)}")

if added:
    print(f"\n📗 صفحات جديدة:")
    for url in sorted(added):
        print(f"   ✅ {new_map[url]['title']}")

if changed:
    print(f"\n📝 صفحات تغيرت:")
    for url in sorted(changed):
        o, n = old_map[url], new_map[url]
        print(f"   🔄 {o['title']}: {len(o.get('content',''))} → {len(n.get('content',''))} حرف")

if removed:
    print(f"\n🗑️ صفحات موجودة بالقديم فقط:")
    for url in sorted(removed):
        print(f"   ❌ {old_map[url]['title']}")

if same:
    print(f"\n📄 صفحات متطابقة ({len(same)}):")
    for url in sorted(list(same)[:5]):
        print(f"   ➖ {old_map[url]['title']}")
    if len(same) > 5:
        print(f"   ... و {len(same)-5} أخرى")

# نسخ احتياطي ودمج
ts = datetime.now().strftime("%Y%m%d_%H%M%S")
backup = os.path.join(BACKUP_DIR, f"backup_{ts}.json")
shutil.copy2(OLD, backup)
print(f"\n💾 نسخة احتياطية: {backup}")

print("\nاختيار المحتوى النهائي:")
ans = input("تحتفظ بالصفحات المحذوفة من القديم؟ (y/N): ").strip().lower()
final = list(new_list)
if ans == "y":
    for url in removed:
        final.append(old_map[url])
    print(f"   تم إضافة {len(removed)} صفحة من القديم")

# إزالة المتطابق
ans = input("تحذف الصفحات المتطابقة من النتيجة؟ (Y/n): ").strip().lower()
if ans != "n":
    final_urls = set()
    unique = []
    for p in final:
        u = p.get("url","").rstrip("/")
        if u not in final_urls:
            final_urls.add(u)
            unique.append(p)
    final = unique
    print(f"   تم إزالة التكرار، بقيت {len(final)} صفحة")

with open(OLD, "w", encoding="utf-8") as f:
    json.dump(final, f, ensure_ascii=False, indent=2)

print(f"\n✅ تم حفظ {len(final)} صفحة في site_content.json")
