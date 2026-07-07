import os, json, sys
import requests

BASE = os.path.dirname(os.path.abspath(__file__))
SITE = "https://nrcapp.onrender.com"

print("="*60)
print("  الأداة 1: زحف المحتوى - نقطة")
print("="*60)

license_id = input("\nرقم ترخيص المؤسسة (123456): ").strip() or "123456"
passcode = input("رمز الدخول (123456): ").strip() or "123456"

session = requests.Session()

# تسجيل/دخول
print("\nجاري تسجيل الدخول...")
try:
    r = session.post(f"{SITE}/api/auth/organization/register", json={
        "ngoName": "أداة الزحف", "licenseId": license_id,
        "authorizedPerson": "النظام", "passcode": passcode
    })
    if r.status_code not in (200, 201):
        r = session.post(f"{SITE}/api/auth/organization", json={
            "licenseId": license_id, "passcode": passcode
        })
    data = r.json()
    print(f"   {data.get('message', 'تم الدخول')}")
except Exception as e:
    print(f"   خطأ: {e}")
    sys.exit(1)

# جمع المحتوى
pages = []

# الصفحات الرئيسية
pages.append({"url": f"{SITE}/", "title": "نقطة - بوابة الدخول",
    "content": "نقطة - مركز تنسيق الإغاثة في غزة. بوابة الدخول الرئيسية. تسجيل ودخول المؤسسات والمواطنين ومسؤولي النظام."})

pages.append({"url": f"{SITE}/admin/login", "title": "نقطة - دخول مسؤول النظام",
    "content": "دخول مسؤول النظام. مخصص للإدارة العليا لمراجعة المؤسسات والخطط والتعارضات وسجل المزامنة."})

# ملخص API
try:
    r = session.get(f"{SITE}/api/dashboard/summary")
    if r.status_code == 200:
        d = r.json()
        text = f"ملخص الموقع: {d['activeOrganizations']} مؤسسات نشطة، {d['authorizedPlans']} خطة معتمدة، {d['warningPlans']} تحذير، {d['pendingSyncItems']} مزامنة معلقة."
        pages.append({"url": f"{SITE}/org/dashboard", "title": "نقطة - لوحة المؤسسة", "content": text})
except:
    pass

# صفحات ثابتة
static = [
    ("aid-distribution", "نقطة - الجدولة والتوزيع", "تخطيط التوزيع الذكي. الخطة تُفحص حسب القطاع ونوع المساعدة والتاريخ. إذا تكررت خلال 48 ساعة تُحفظ كتحذير."),
    ("analytics", "نقطة - التحليلات", "التحليلات: معدل منع التكرار، متوسط زمن الاستجابة، طلبات في الانتظار. تغطية المساعدات حسب القطاع."),
    ("gap-detector", "نقطة - كشف الفجوات", "خريطة فجوات نقدية. أزرق مخدومة جيداً، ذهبي مخدومة حديثاً، أحمر فجوات حرجة."),
    ("volunteers", "نقطة - المتطوعون", "الموارد والتحقق الميداني. تأكيد تسليم الميدان مع حفظ محلي."),
    ("settings", "نقطة - الإعدادات", "إعدادات النموذج التجريبي: تفعيل وضع عدم الاتصال، إظهار الفجوات الحرجة، محاكاة تأخير المزامنة."),
]
for path, title, content in static:
    pages.append({"url": f"{SITE}/{path}", "title": title, "content": content})

# ملف المواطن
try:
    r = session.get(f"{SITE}/citizen/profile")
    if r.status_code == 200:
        pages.append({"url": f"{SITE}/citizen/profile", "title": "نقطة - ملف المواطن",
            "content": "ملف المواطن. يعرض المؤسسات النشطة حسب القطاع والتسجيلات الحالية والتوزيعات القادمة."})
except:
    pass

# حفظ
out = os.path.join(BASE, "site_content_new.json")
with open(out, "w", encoding="utf-8") as f:
    json.dump(pages, f, ensure_ascii=False, indent=2)

print(f"\n✅ تم! {len(pages)} صفحة محفوظة في site_content_new.json")
for p in pages:
    print(f"   - {p['title']}")
