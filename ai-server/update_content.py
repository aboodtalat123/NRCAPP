import os
import json
import shutil
from datetime import datetime

BASE = os.path.dirname(os.path.abspath(__file__))
OLD_FILE = os.path.join(BASE, "site_content.json")
BACKUP_DIR = os.path.join(BASE, "backups")
CRAWLER_FILE = os.path.join(BASE, "..", "..", "C:\\Users\\UNRWA\\Desktop\\site-ai-assistant\\crawler.py")

os.makedirs(BACKUP_DIR, exist_ok=True)

def load_json(path):
    if not os.path.exists(path):
        return []
    with open(path, encoding="utf-8") as f:
        return json.load(f)

def save_json(path, data):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

def get_key(page):
    return page.get("url", "").rstrip("/") or "/"

def print_header(text):
    print(f"\n{'='*60}")
    print(f"  {text}")
    print(f"{'='*60}")

print_header("نظام تحديث محتوى الذكاء - نقطة")

# 1. تشغيل الكراولر
print("\n1. تشغيل الكراولر لجلب المحتوى الجديد...")
LICENSE = input("   رقم ترخيص المؤسسة (123456): ").strip() or "123456"
PASSCODE = input("   رمز دخول المؤسسة (123456): ").strip() or "123456"

# نشغل الكراولر مع المتغيرات
import subprocess
env = os.environ.copy()
env["SITE_URL"] = "https://nrcapp.onrender.com/"
env["SITE_LICENSE"] = LICENSE
env["SITE_PASSWORD"] = PASSCODE

# نجيب المحتوى عبر API
import requests

session = requests.Session()
print("\n   جاري تسجيل الدخول...")
try:
    r = session.post("https://nrcapp.onrender.com/api/auth/organization/register", json={
        "ngoName": "أداة التحديث",
        "licenseId": LICENSE,
        "authorizedPerson": "النظام",
        "passcode": PASSCODE
    })
    if r.status_code not in (200, 201):
        r = session.post("https://nrcapp.onrender.com/api/auth/organization", json={
            "licenseId": LICENSE,
            "passcode": PASSCODE
        })
    print(f"   {r.json().get('message', 'تم الدخول')}")
except Exception as e:
    print(f"   خطأ: {e}")
    exit(1)

# نجيب المحتوى من API
print("   جاري جمع المحتوى من API...")
new_pages = []

# قائمة المسارات المعروفة
routes = [
    ("/api/dashboard/summary", "نقطة - ملخص API"),
]

# الصفحات الثابتة
static_pages = [
    {"url": "https://nrcapp.onrender.com/", "title": "نقطة - بوابة الدخول", "content": ""},
    {"url": "https://nrcapp.onrender.com/admin/login", "title": "نقطة - دخول مسؤول النظام", "content": ""},
    {"url": "https://nrcapp.onrender.com/aid-distribution", "title": "نقطة - الجدولة والتوزيع", "content": ""},
    {"url": "https://nrcapp.onrender.com/analytics", "title": "نقطة - التحليلات", "content": ""},
    {"url": "https://nrcapp.onrender.com/gap-detector", "title": "نقطة - كشف الفجوات", "content": ""},
    {"url": "https://nrcapp.onrender.com/volunteers", "title": "نقطة - المتطوعون", "content": ""},
    {"url": "https://nrcapp.onrender.com/settings", "title": "نقطة - الإعدادات", "content": ""},
]

new_pages.extend(static_pages)

# نجيب ملف المواطن
try:
    r = session.get("https://nrcapp.onrender.com/api/dashboard/summary")
    if r.status_code == 200:
        data = r.json()
        orgs = data.get("activeOrganizations", 0)
        plans = data.get("authorizedPlans", 0)
        new_pages.append({
            "url": "https://nrcapp.onrender.com/api/dashboard/summary",
            "title": "نقطة - ملخص اللوحة",
            "content": f"ملخص موقع نقطة: {orgs} مؤسسات نشطة، {plans} خطة معتمدة"
        })
except:
    pass

print(f"   تم جمع {len(new_pages)} صفحة")

# 2. تحميل القديم
old_pages = load_json(OLD_FILE)

# 3. المقارنة
print_header("نتيجة المقارنة")

old_keys = {get_key(p): p for p in old_pages}
new_keys = {get_key(p): p for p in new_pages}

old_urls = set(old_keys.keys())
new_urls = set(new_keys.keys())

# صفحات جديدة
added = new_urls - old_urls
# صفحات محذوفة (موجودة بالقديم مش بالجديد)
removed = old_urls - new_urls
# صفحات مشتركة
common = old_urls & new_urls

# صفحات متطابقة (نفس المحتوى)
same = []
changed = []
for url in common:
    if old_keys[url].get("content", "") == new_keys[url].get("content", ""):
        same.append(url)
    else:
        changed.append(url)

print(f"\n📊 الإحصائيات:")
print(f"   الصفحات القديمة: {len(old_pages)}")
print(f"   الصفحات الجديدة: {len(new_pages)}")
print(f"   📗 صفحات جديدة: {len(added)}")
print(f"   📝 صفحات تغير محتواها: {len(changed)}")
print(f"   📄 صفحات متطابقة (مكررة): {len(same)}")
print(f"   🗑️ صفحات محذوفة (موجودة بالقديم فقط): {len(removed)}")

if added:
    print(f"\n📗 صفحات جديدة:")
    for url in sorted(added):
        print(f"   ✅ {new_keys[url]['title']} - {url}")

if changed:
    print(f"\n📝 صفحات تغيرت:")
    for url in sorted(changed):
        old_len = len(old_keys[url].get("content", ""))
        new_len = len(new_keys[url].get("content", ""))
        diff = new_len - old_len
        sign = "+" if diff >= 0 else ""
        print(f"   🔄 {old_keys[url]['title']} - {old_len} → {new_len} حرف ({sign}{diff})")

if removed:
    print(f"\n🗑️ صفحات موجودة بالقديم فقط (ممكن تحذف):")
    for url in sorted(removed):
        print(f"   ❌ {old_keys[url]['title']} - {url}")

if same:
    print(f"\n📄 صفحات متطابقة (ما تغيرت):")
    for url in sorted(same):
        print(f"   ➖ {old_keys[url]['title']}")

# 4. اختيار الصفحات للاحتفاظ
print_header("تأكيد المحتوى النهائي")

# نسأل عن المحذوفة
keep_removed = []
for url in sorted(removed):
    ans = input(f"   الصفحة '{old_keys[url]['title']}' موجودة بالقديم فقط. تحتفظ فيها؟ (Y/n): ").strip().lower()
    if ans != "n":
        keep_removed.append(url)
        new_pages.append(old_keys[url])

# نسأل عن الجديدة
print(f"\n📗 الصفحات الجديدة:")
for url in sorted(added):
    print(f"   ✅ {new_keys[url]['title']} ({url})")

# 5. حفظ النتيجة
timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
backup_file = os.path.join(BACKUP_DIR, f"site_content_backup_{timestamp}.json")

# نسخة احتياطية
save_json(backup_file, old_pages)
print(f"\n💾 نسخة احتياطية: {backup_file}")

# حفظ الجديد
save_json(OLD_FILE, new_pages)
print(f"💾 تم حفظ {len(new_pages)} صفحة في site_content.json")

# 6. عرض الأوامر للرفع
print_header("لرفع التعديلات على GitHub و Render")

print("انسخ هذه الأوامر بالترتيب:\n")
print(f"   cd {os.path.dirname(BASE)}")
print("   git add ai-server/site_content.json")
print('   git commit -m "تحديث محتوى الذكاء"')
print("   git push")
print("\nوبعدها:")
print("   1. افتح https://dashboard.render.com")
print("   2. خدمة nrcapp-ai-assistant → Manual Deploy → Deploy latest commit")
print(f"\n✅ تم! {len(new_pages)} صفحة في قاعدة المعرفة.")
