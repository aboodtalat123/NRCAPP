"""
أداة فحص وتحديث موقع نقطة (NRCAPP) - سكربت طرفية تفاعلية
==========================================================
تسجل دخول للموقع بأي دور (أدمن / مؤسسة / مواطن)، وتكتشف الروابط
الداخلية تلقائياً (بمتابعة روابط <a> فقط)، وتحفظ محتوى كل صفحة
بقاعدة بيانات محلية (SQLite) على جهازك، جاهزة بعدين للمراجعة
والفلترة قبل تصديرها لقاعدة معرفة الذكاء الصناعي.

كيف تشغلها:
    python site_inspector.py

أول مرة بس، ثبّت المتطلبات:
    pip install playwright
    playwright install chromium
"""

import sqlite3
import sys
import getpass
import hashlib
from datetime import datetime
from urllib.parse import urljoin, urlparse

from playwright.sync_api import sync_playwright

# ==========================================================
# إعدادات ثابتة - عدّل هون لو حبيت
# ==========================================================
DB_FILE = "local_crawl_data.db"
MAX_PAGES = 60                 # حد أقصى لعدد الصفحات (حماية من زحف بلا نهاية)
MAX_DEPTH = 2                  # حد أقصى لعمق المسار (يمنع التكرار اللانهائي)
PAGE_LOAD_TIMEOUT_MS = 15000    # وقت انتظار كل صفحة (15 ثانية)

# كلمات خطرة - أي رابط فيه هاي الكلمة يتجاهله السكربت تماماً
# (حماية من الضغط بالغلط على حذف/خروج/رفض أثناء الفحص الآلي)
DANGEROUS_KEYWORDS = [
    "delete", "remove", "logout", "reject", "cancel",
    "حذف", "خروج", "رفض", "الغاء", "إلغاء",
]

# روابط لا تتابعها أبداً (ملفات، بروتوكولات خارجية)
IGNORED_PREFIXES = ("mailto:", "tel:", "javascript:", "#")
IGNORED_EXTENSIONS = (".pdf", ".jpg", ".png", ".zip", ".css", ".js", ".ico")


def print_banner():
    print("=" * 55)
    print("   أداة فحص وتحديث موقع نقطة (NRCAPP)")
    print("=" * 55)
    print()


def ask_inputs():
    """يسأل المستخدم كل المعلومات المطلوبة بوضوح"""
    site_url = input("أدخل رابط الموقع (مثال: https://nrcapp.onrender.com): ").strip()
    if not site_url:
        site_url = "https://nrcapp.onrender.com"
        print(f"   (استخدمت default: {site_url})")
    if not site_url.startswith("http"):
        site_url = "https://" + site_url
    site_url = site_url.rstrip("/")

    print()
    print("[دور] اختر الدور الذي تريد تسجيل الدخول به:")
    print("   1) مسؤول (Admin)")
    print("   2) مؤسسة (Organization)")
    print("   3) مواطن (Individual)")
    role_choice = input("اختيارك (1/2/3): ").strip()

    role_map = {
        "1": ("admin", "/admin/login"),
        "2": ("organization", "/org/login"),
        "3": ("individual", "/citizen/login"),
    }
    if role_choice not in role_map:
        print("[خطأ] اختيار غير صحيح، حاول من جديد.")
        sys.exit(1)

    role_name, login_path = role_map[role_choice]

    print()
    if role_name == "individual":
        username = input("أدخل رقم الهوية الوطنية (National ID): ").strip()
        password = None
    else:
        username = input("أدخل اسم المستخدم / رقم الترخيص: ").strip()
        password = getpass.getpass("أدخل كلمة السر (ما رح تظهر وأنت تكتب): ").strip()

    return site_url, role_name, login_path, username, password


def init_db():
    """ينشئ قاعدة بيانات محلية بسيطة لتخزين نتائج الفحص"""
    conn = sqlite3.connect(DB_FILE, timeout=10)
    conn.execute("""
        CREATE TABLE IF NOT EXISTS pages (
            url TEXT PRIMARY KEY,
            title TEXT,
            content TEXT,
            content_hash TEXT,
            role_crawled_as TEXT,
            last_crawled_at TEXT
        )
    """)
    conn.commit()
    return conn


def is_dangerous(url: str) -> bool:
    lowered = url.lower()
    return any(word in lowered for word in DANGEROUS_KEYWORDS)


def is_ignored(url: str) -> bool:
    if url.startswith(IGNORED_PREFIXES):
        return True
    if url.lower().endswith(IGNORED_EXTENSIONS):
        return True
    return False


def same_domain(base_url: str, target_url: str) -> bool:
    return urlparse(base_url).netloc == urlparse(target_url).netloc

def path_depth(url: str) -> int:
    """عدد أجزاء المسار بعد النطاق (مثلاً /org/citizen/profile → 3)"""
    path = urlparse(url).path.strip("/")
    return len(path.split("/")) if path else 0


def login(page, site_url, role_name, login_path, username, password):
    """يسجل دخول حسب الدور"""
    full_login_url = urljoin(site_url + "/", login_path.lstrip("/"))
    print(f"[...] جاري فتح صفحة تسجيل الدخول: {full_login_url}")
    page.goto(full_login_url, timeout=PAGE_LOAD_TIMEOUT_MS)
    page.wait_for_load_state("networkidle")

    text_input = page.locator(
        "input[type='text'], input[type='number'], input:not([type])"
    ).first
    text_input.fill(username)

    if password:
        password_input = page.locator("input[type='password']").first
        password_input.fill(password)

    submit_button = page.locator(
        "button[type='submit'], input[type='submit'], button"
    ).first
    submit_button.click()
    page.wait_for_load_state("networkidle")

    print("[تم] تم إرسال بيانات تسجيل الدخول.")


def extract_page_text(page) -> str:
    """يسحب النص الظاهر بالصفحة فقط (بدون كود HTML/CSS/JS)"""
    try:
        return page.locator("body").inner_text(timeout=5000)
    except Exception:
        return ""


def crawl(site_url, role_name, page, conn):
    """يكتشف الروابط الداخلية تلقائياً ويحفظ محتوى كل صفحة"""
    visited = set()
    to_visit = [urljoin(site_url + "/", "")]
    saved_count = 0

    print()
    print("[...] جاري فحص الصفحات (اكتشاف تلقائي للروابط)...")
    print()

    while to_visit and saved_count < MAX_PAGES:
        current_url = to_visit.pop(0)

        if current_url in visited:
            continue
        visited.add(current_url)

        if is_ignored(current_url) or is_dangerous(current_url):
            continue
        if not same_domain(site_url, current_url):
            continue

        try:
            page.goto(current_url, timeout=PAGE_LOAD_TIMEOUT_MS)
            page.wait_for_load_state("networkidle")
        except Exception as e:
            print(f"   [تخطي] {current_url} (خطأ تحميل: {e})")
            continue

        title = page.title()
        content = extract_page_text(page)
        content_hash = hashlib.sha256(content.encode("utf-8")).hexdigest()

        conn.execute(
            """
            INSERT INTO pages (url, title, content, content_hash, role_crawled_as, last_crawled_at)
            VALUES (?, ?, ?, ?, ?, ?)
            ON CONFLICT(url) DO UPDATE SET
                title=excluded.title,
                content=excluded.content,
                content_hash=excluded.content_hash,
                role_crawled_as=excluded.role_crawled_as,
                last_crawled_at=excluded.last_crawled_at
            """,
            (current_url, title, content, content_hash, role_name, datetime.now().isoformat()),
        )
        conn.commit()
        saved_count += 1
        print(f"   [جيد] ({saved_count}) {current_url} - تم الحفظ")

        try:
            hrefs = page.locator("a").evaluate_all("els => els.map(e => e.getAttribute('href'))")
        except Exception:
            hrefs = []

        for href in hrefs:
            if not href:
                continue
            full_link = urljoin(current_url, href)
            if full_link not in visited and full_link not in to_visit:
                if same_domain(site_url, full_link) and not is_ignored(full_link) and not is_dangerous(full_link):
                    if path_depth(full_link) <= MAX_DEPTH:
                        to_visit.append(full_link)

    return saved_count


def main():
    print_banner()
    site_url, role_name, login_path, username, password = ask_inputs()

    conn = init_db()

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()

        login(page, site_url, role_name, login_path, username, password)
        total_saved = crawl(site_url, role_name, page, conn)

        browser.close()

    conn.close()

    print()
    print("=" * 55)
    print(f"[تم] انتهى الفحص! تم حفظ {total_saved} صفحة بقاعدة البيانات المحلية")
    print(f"[ملف] {DB_FILE}")
    print()
    print("[تنبيه] تذكير مهم: راجع محتوى الملف وافلتر أي بيانات شخصية")
    print("   حساسة (أسماء، أرقام هوية، أرقام هاتف) قبل ما تصدر أي")
    print("   شي منه لقاعدة معرفة الذكاء الصناعي.")
    print("=" * 55)


if __name__ == "__main__":
    main()
