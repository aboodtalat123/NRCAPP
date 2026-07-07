import os, json, threading
import tkinter as tk
from tkinter import messagebox, scrolledtext

BASE = os.path.dirname(os.path.abspath(__file__))
SITE = "https://nrcapp.onrender.com"

class App:
    def __init__(self, root):
        self.root = root
        root.title("نقطة - أدوات الذكاء")
        root.geometry("750x600")
        root.configure(bg="#f5f5f5")

        header = tk.Label(root, text="أدوات تحديث محتوى الذكاء - نقطة",
                          font=("Arial", 16, "bold"), bg="#1a73e8", fg="white", pady=10)
        header.pack(fill="x")

        frame = tk.Frame(root, bg="#f5f5f5")
        frame.pack(pady=10)

        tk.Label(frame, text="رقم الترخيص:", bg="#f5f5f5").grid(row=0, column=0, padx=5, pady=5)
        self.license_entry = tk.Entry(frame, width=20)
        self.license_entry.insert(0, "123456")
        self.license_entry.grid(row=0, column=1, padx=5, pady=5)

        tk.Label(frame, text="رمز الدخول:", bg="#f5f5f5").grid(row=0, column=2, padx=5, pady=5)
        self.passcode_entry = tk.Entry(frame, width=20)
        self.passcode_entry.insert(0, "123456")
        self.passcode_entry.grid(row=0, column=3, padx=5, pady=5)

        frame2 = tk.Frame(root, bg="#f5f5f5")
        frame2.pack(pady=5)
        tk.Label(frame2, text="ملخص التغيير:", bg="#f5f5f5").pack(side="left", padx=5)
        self.commit_entry = tk.Entry(frame2, width=50)
        self.commit_entry.insert(0, "تحديث محتوى الذكاء")
        self.commit_entry.pack(side="left", padx=5)

        btn_frame = tk.Frame(root, bg="#f5f5f5")
        btn_frame.pack(pady=10)

        self.crawl_btn = tk.Button(btn_frame, text="1. زحف المحتوى", command=self.run_crawl,
                                   bg="#34a853", fg="white", font=("Arial", 12), padx=15, pady=5)
        self.crawl_btn.grid(row=0, column=0, padx=10)

        self.compare_btn = tk.Button(btn_frame, text="2. مقارنة المحتوى", command=self.run_compare,
                                     bg="#fbbc04", fg="black", font=("Arial", 12), padx=15, pady=5)
        self.compare_btn.grid(row=0, column=1, padx=10)

        self.deploy_btn = tk.Button(btn_frame, text="3. رفع ونشر", command=self.run_deploy,
                                    bg="#1a73e8", fg="white", font=("Arial", 12), padx=15, pady=5)
        self.deploy_btn.grid(row=0, column=2, padx=10)

        self.output = scrolledtext.ScrolledText(root, height=20, font=("Consolas", 10), wrap=tk.WORD)
        self.output.pack(fill="both", padx=10, pady=10, expand=True)

        self.status = tk.Label(root, text="جاهز", bd=1, relief="sunken", anchor="w", bg="#e0e0e0")
        self.status.pack(fill="x")

    def log(self, text):
        self.output.insert("end", text + "\n")
        self.output.see("end")

    def set_loading(self, loading=True):
        state = "disabled" if loading else "normal"
        self.crawl_btn.config(state=state)
        self.compare_btn.config(state=state)
        self.deploy_btn.config(state=state)
        self.status.config(text="جاري التنفيذ..." if loading else "جاهز")

    def run_crawl(self):
        t = threading.Thread(target=self._do_crawl, daemon=True)
        t.start()

    def _do_crawl(self):
        self.root.after(0, lambda: self.output.delete("1.0", "end"))
        self.root.after(0, lambda: self.log("جاري زحف الموقع..."))
        self.root.after(0, self.set_loading, True)

        license_id = self.license_entry.get().strip() or "123456"
        passcode = self.passcode_entry.get().strip() or "123456"

        import requests
        session = requests.Session()

        self.root.after(0, lambda: self.log(f"تسجيل الدخول برخصة: {license_id}..."))
        try:
            r = session.post(f"{SITE}/api/auth/organization/register", json={
                "ngoName": "أداة الزحف", "licenseId": license_id,
                "authorizedPerson": "النظام", "passcode": passcode
            }, timeout=15)
            if r.status_code not in (200, 201):
                r = session.post(f"{SITE}/api/auth/organization", json={
                    "licenseId": license_id, "passcode": passcode
                }, timeout=15)
            data = r.json()
            self.root.after(0, lambda: self.log(f"✅ {data.get('message', 'تم الدخول')}"))
        except Exception as e:
            self.root.after(0, lambda: self.log(f"❌ فشل تسجيل الدخول: {e}"))
            self.root.after(0, self.set_loading, False)
            return

        pages = []
        pages.append({"url": f"{SITE}/", "title": "نقطة - بوابة الدخول",
            "content": "نقطة - مركز تنسيق الإغاثة في غزة. بوابة الدخول الرئيسية."})
        pages.append({"url": f"{SITE}/admin/login", "title": "نقطة - دخول مسؤول النظام",
            "content": "دخول مسؤول النظام. مخصص للإدارة العليا لمراجعة المؤسسات والخطط والتعارضات."})

        try:
            r = session.get(f"{SITE}/api/dashboard/summary", timeout=10)
            if r.status_code == 200:
                d = r.json()
                text = f"ملخص الموقع: {d.get('activeOrganizations',0)} مؤسسات نشطة، {d.get('authorizedPlans',0)} خطة معتمدة."
                pages.append({"url": f"{SITE}/org/dashboard", "title": "نقطة - لوحة المؤسسة", "content": text})
                self.root.after(0, lambda: self.log("✅ ملخص الموقع: تم"))
        except Exception as e:
            self.root.after(0, lambda: self.log(f"⚠️ API dashboard: {e}"))

        try:
            r = session.get(f"{SITE}/citizen/profile", timeout=10)
            if r.status_code == 200:
                pages.append({"url": f"{SITE}/citizen/profile", "title": "نقطة - ملف المواطن",
                    "content": "ملف المواطن. يعرض المؤسسات النشطة حسب القطاع والتسجيلات الحالية."})
                self.root.after(0, lambda: self.log("✅ ملف المواطن: تم"))
        except:
            pass

        static = [
            ("aid-distribution", "نقطة - الجدولة والتوزيع", "تخطيط التوزيع الذكي."),
            ("analytics", "نقطة - التحليلات", "التحليلات: إحصائيات التوزيع."),
            ("gap-detector", "نقطة - كشف الفجوات", "خريطة الفجوات."),
            ("volunteers", "نقطة - المتطوعون", "الموارد والتحقق الميداني."),
            ("settings", "نقطة - الإعدادات", "إعدادات النموذج التجريبي."),
        ]
        for path, title, content in static:
            pages.append({"url": f"{SITE}/{path}", "title": title, "content": content})

        out = os.path.join(BASE, "site_content_new.json")
        with open(out, "w", encoding="utf-8") as f:
            json.dump(pages, f, ensure_ascii=False, indent=2)

        self.root.after(0, lambda: self.log(f"\n✅ تم! {len(pages)} صفحة محفوظة في site_content_new.json"))
        for p in pages:
            self.root.after(0, lambda t=p['title']: self.log(f"   - {t}"))
        self.root.after(0, self.set_loading, False)

    def run_compare(self):
        t = threading.Thread(target=self._do_compare, daemon=True)
        t.start()

    def _do_compare(self):
        self.root.after(0, lambda: self.output.delete("1.0", "end"))
        self.root.after(0, lambda: self.log("جاري مقارنة المحتوى..."))
        self.root.after(0, self.set_loading, True)

        OLD = os.path.join(BASE, "site_content.json")
        NEW = os.path.join(BASE, "site_content_new.json")
        BACKUP_DIR = os.path.join(BASE, "backups")
        os.makedirs(BACKUP_DIR, exist_ok=True)

        def load(path):
            if not os.path.exists(path):
                return [], {}
            with open(path, encoding="utf-8") as f:
                data = json.load(f)
            return data, {p.get("url","").rstrip("/"): p for p in data}

        old_list, old_map = load(OLD)
        new_list, new_map = load(NEW)

        if not new_list:
            self.root.after(0, lambda: self.log("❌ لا يوجد site_content_new.json. شغّل الزحف أولاً."))
            self.root.after(0, self.set_loading, False)
            return

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

        self.root.after(0, lambda: self.log(f"\n📊 المقارنة:"))
        self.root.after(0, lambda: self.log(f"   القديم: {len(old_list)} | الجديد: {len(new_list)}"))
        self.root.after(0, lambda: self.log(f"   📗 جديد: {len(added)} | 📝 متغير: {len(changed)} | 📄 متطابق: {len(same)} | 🗑️ محذوف: {len(removed)}"))

        for url in sorted(added):
            self.root.after(0, lambda u=url: self.log(f"   ✅ جديد: {new_map[u]['title']}"))
        for url in sorted(changed):
            o, n = old_map[url], new_map[url]
            self.root.after(0, lambda u=url,o=o,n=n: self.log(f"   🔄 متغير: {o['title']} ({len(o.get('content',''))}→{len(n.get('content',''))} حرف)"))
        for url in sorted(removed):
            self.root.after(0, lambda u=url: self.log(f"   ❌ محذوف: {old_map[u]['title']}"))

        from datetime import datetime
        ts = datetime.now().strftime("%Y%m%d_%H%M%S")
        backup = os.path.join(BACKUP_DIR, f"backup_{ts}.json")
        import shutil
        shutil.copy2(OLD, backup)
        self.root.after(0, lambda: self.log(f"\n💾 نسخة احتياطية: backup_{ts}.json"))

        final = list(new_list)
        for url in removed:
            final.append(old_map[url])

        final_urls = set()
        unique = []
        for p in final:
            u = p.get("url","").rstrip("/")
            if u not in final_urls:
                final_urls.add(u)
                unique.append(p)
        final = unique

        with open(OLD, "w", encoding="utf-8") as f:
            json.dump(final, f, ensure_ascii=False, indent=2)

        self.root.after(0, lambda: self.log(f"✅ تم حفظ {len(final)} صفحة في site_content.json"))
        self.root.after(0, self.set_loading, False)

    def run_deploy(self):
        t = threading.Thread(target=self._do_deploy, daemon=True)
        t.start()

    def _do_deploy(self):
        self.root.after(0, lambda: self.output.delete("1.0", "end"))
        self.root.after(0, lambda: self.log("جاري رفع التحديثات إلى GitHub..."))
        self.root.after(0, self.set_loading, True)

        repo = os.path.dirname(BASE)
        msg = self.commit_entry.get().strip() or "تحديث محتوى الذكاء"

        import subprocess

        # 1. git add
        self.root.after(0, lambda: self.log(f"\n1. git add ai-server/site_content.json..."))
        r = subprocess.run(["git", "add", "ai-server/site_content.json"], capture_output=True, text=True, cwd=repo)
        if r.returncode != 0:
            self.root.after(0, lambda: self.log(f"❌ git add فشل: {r.stderr.strip()}"))
            self.root.after(0, self.set_loading, False)
            return
        self.root.after(0, lambda: self.log("✅ git add تم"))

        # 2. git commit
        self.root.after(0, lambda: self.log(f'\n2. git commit -m "{msg}"...'))
        r = subprocess.run(["git", "commit", "-m", msg], capture_output=True, text=True, cwd=repo)
        if r.returncode not in (0, 1):
            self.root.after(0, lambda: self.log(f"❌ git commit فشل: {r.stderr.strip()}"))
            self.root.after(0, self.set_loading, False)
            return
        if "nothing to commit" in r.stdout.lower() or "nothing to commit" in r.stderr.lower():
            self.root.after(0, lambda: self.log("ℹ️ لا يوجد تغييرات جديدة للرفع"))
        else:
            self.root.after(0, lambda: self.log(f"✅ git commit تم"))

        # 3. git push
        self.root.after(0, lambda: self.log("\n3. git push..."))
        r = subprocess.run(["git", "push"], capture_output=True, text=True, cwd=repo)
        if r.returncode != 0:
            self.root.after(0, lambda: self.log(f"❌ git push فشل: {r.stderr.strip()}"))
            self.root.after(0, self.set_loading, False)
            return
        self.root.after(0, lambda: self.log("✅ git push تم"))

        self.root.after(0, lambda: self.log("\n" + "="*50))
        self.root.after(0, lambda: self.log("  ✅ تم رفع التحديثات إلى GitHub بنجاح!"))
        self.root.after(0, lambda: self.log("="*50))
        self.root.after(0, lambda: self.log(""))
        self.root.after(0, lambda: self.log("الآن اذهب إلى Render Dashboard:"))
        self.root.after(0, lambda: self.log("   1. https://dashboard.render.com"))
        self.root.after(0, lambda: self.log('   2. خدمة "nrcapp-ai-assistant"'))
        self.root.after(0, lambda: self.log("   3. Manual Deploy ← Deploy latest commit"))
        self.root.after(0, lambda: self.log(""))
        self.root.after(0, lambda: self.log("⏳ بعد 3 دقائق اختبر:"))
        self.root.after(0, lambda: self.log("   https://nrcapp-ai-assistant.onrender.com/health"))
        self.root.after(0, self.set_loading, False)

if __name__ == "__main__":
    root = tk.Tk()
    app = App(root)
    root.mainloop()
