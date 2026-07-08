import os, json, threading, subprocess, shutil, re, sys
import tkinter as tk
from tkinter import scrolledtext, ttk, messagebox
from datetime import datetime

BASE = os.path.dirname(os.path.abspath(__file__))

FIELD_CONFIG = {
    "org": [
        ("رقم الترخيص:", "license_entry", ""),
        ("رمز الدخول:", "passcode_entry", "*"),
    ],
    "citizen": [
        ("رقم الهوية:", "license_entry", ""),
    ],
    "admin": [
        ("اسم المستخدم:", "license_entry", ""),
        ("كلمة السر:", "passcode_entry", "*"),
    ],
}


class App:
    def __init__(self, root):
        self.root = root
        root.title("نقطة - أدوات الذكاء")
        root.geometry("850+700")
        root.configure(bg="#f5f5f5")

        header = tk.Label(root, text="🧠 أدوات تحديث قاعدة معرفة الذكاء - نقطة",
                          font=("Arial", 15, "bold"), bg="#1a73e8", fg="white", pady=10)
        header.pack(fill="x")

        main_frame = tk.Frame(root, bg="#f5f5f5")
        main_frame.pack(pady=8, padx=12, fill="x")

        # row 0: site URL
        tk.Label(main_frame, text="🔗 رابط الموقع:", bg="#f5f5f5", font=("Arial", 10))\
            .grid(row=0, column=0, padx=4, pady=3, sticky="e")
        self.site_entry = tk.Entry(main_frame, width=45, font=("Arial", 10))
        self.site_entry.insert(0, "https://nrcapp.onrender.com")
        self.site_entry.grid(row=0, column=1, columnspan=3, padx=4, pady=3, sticky="w")

        # row 1: role selector
        tk.Label(main_frame, text="🎭 الدور:", bg="#f5f5f5", font=("Arial", 10))\
            .grid(row=1, column=0, padx=4, pady=3, sticky="e")
        self.role_var = tk.StringVar(value="org")
        role_menu = ttk.Combobox(main_frame, textvariable=self.role_var,
                                 values=["org", "citizen", "admin"], state="readonly", width=15, font=("Arial", 10))
        role_menu.grid(row=1, column=1, padx=4, pady=3, sticky="w")
        role_labels = {"org": "🏢 مؤسسة", "citizen": "👤 مواطن", "admin": "🔒 أدمن"}
        role_display = {v: k for k, v in role_labels.items()}
        role_menu.configure(values=list(role_labels.values()))
        self.role_var.set("🏢 مؤسسة")
        role_menu.bind("<<ComboboxSelected>>", self.on_role_change)

        self.role_text_var = role_menu

        # row 2: dynamic fields
        self.fields_frame = tk.Frame(main_frame, bg="#f5f5f5")
        self.fields_frame.grid(row=2, column=0, columnspan=4, pady=5, sticky="w")

        self.field_widgets = {}
        self._build_fields("org")

        # row 3: commit message
        self.commit_frame = tk.Frame(root, bg="#f5f5f5")
        self.commit_frame.pack(pady=5)

        # BUTTONS
        btn_frame = tk.Frame(root, bg="#f5f5f5")
        btn_frame.pack(pady=8)

        self.crawl_btn = tk.Button(btn_frame, text="1. 🕷️ زحف المحتوى", command=self.run_crawl,
                                   bg="#34a853", fg="white", font=("Arial", 11, "bold"), padx=14, pady=5, width=18)
        self.crawl_btn.grid(row=0, column=0, padx=8)

        self.compare_btn = tk.Button(btn_frame, text="2. 📊 مقارنة المحتوى", command=self.run_compare,
                                      bg="#fbbc04", fg="black", font=("Arial", 11, "bold"), padx=14, pady=5, width=18)
        self.compare_btn.grid(row=0, column=1, padx=8)

        self.deploy_btn = tk.Button(btn_frame, text="3. 🚀 رفع ونشر", command=self.run_deploy,
                                    bg="#1a73e8", fg="white", font=("Arial", 11, "bold"), padx=14, pady=5, width=18)
        self.deploy_btn.grid(row=0, column=2, padx=8)

        self.output = scrolledtext.ScrolledText(root, height=22, font=("Consolas", 10), wrap=tk.WORD, bg="#1e1e1e", fg="#d4d4d4")
        self.output.pack(fill="both", padx=10, pady=5, expand=True)

        self.status = tk.Label(root, text="✅ جاهز", bd=1, relief="sunken", anchor="w", bg="#e0e0e0", font=("Arial", 9))
        self.status.pack(fill="x")

        self.log("🧠 أدوات تحديث قاعدة معرفة الذكاء - نقطة")
        self.log("=" * 60)
        self.log("📌 سير العمل: 1. زحف ← 2. مقارنة ← 3. رفع ونشر")
        self.log("")
        self.log("⚠️  تأكد أن السيرفر شغال (محلياً أو عالمياً) قبل الزحف")

    def on_role_change(self, event=None):
        raw = self.role_var.get()
        role_map = {"🏢 مؤسسة": "org", "👤 مواطن": "citizen", "🔒 أدمن": "admin"}
        role = role_map.get(raw, "org")
        self._build_fields(role)

    def _build_fields(self, role):
        for w in self.field_widgets.values():
            w.destroy()
        self.field_widgets.clear()

        fields = FIELD_CONFIG[role]
        for i, (label, key, show) in enumerate(fields):
            lbl = tk.Label(self.fields_frame, text=label, bg="#f5f5f5", font=("Arial", 10))
            lbl.grid(row=0, column=i * 2, padx=(0, 2), pady=3, sticky="e")
            entry = tk.Entry(self.fields_frame, width=20, font=("Arial", 10), show=show)
            entry.grid(row=0, column=i * 2 + 1, padx=(0, 15), pady=3, sticky="w")
            self.field_widgets[key] = entry

        self.field_widgets.get("license_entry", tk.Entry()).delete(0, "end")
        self.field_widgets.get("passcode_entry", tk.Entry()).delete(0, "end")

    def _get_role(self):
        raw = self.role_var.get()
        role_map = {"🏢 مؤسسة": "org", "👤 مواطن": "citizen", "🔒 أدمن": "admin"}
        return role_map.get(raw, "org")

    def _get_credentials(self):
        license_val = self.field_widgets["license_entry"].get().strip()
        passcode_val = self.field_widgets["passcode_entry"].get().strip() if "passcode_entry" in self.field_widgets else ""
        return license_val, passcode_val

    def log(self, text):
        self.output.insert("end", text + "\n")
        self.output.see("end")
        self.root.update_idletasks()

    def set_loading(self, loading=True):
        state = "disabled" if loading else "normal"
        self.crawl_btn.config(state=state)
        self.compare_btn.config(state=state)
        self.deploy_btn.config(state=state)
        self.status.config(text="🔄 جاري التنفيذ..." if loading else "✅ جاهز")
        self.root.update_idletasks()

    # ===== BUTTON 1: CRAWL (Playwright) =====

    def run_crawl(self):
        t = threading.Thread(target=self._do_crawl, daemon=True)
        t.start()

    def _do_crawl(self):
        self.root.after(0, lambda: self.output.delete("1.0", "end"))
        self.root.after(0, self.set_loading, True)
        self.root.after(0, lambda: self.log("🕷️ بدء الزحف بـ Playwright..."))

        site = self.site_entry.get().strip().rstrip("/") or "https://nrcapp.onrender.com"
        role = self._get_role()
        username, password = self._get_credentials()

        if not username:
            self.root.after(0, lambda: self.log("❌ اسم المستخدم / رقم الترخيص مطلوب"))
            self.root.after(0, self.set_loading, False)
            return

        new_json = os.path.join(BASE, "site_content_new.json")
        cmd = [
            sys.executable or "python", "crawl_with_playwright.py",
            "--site", site,
            "--role", role,
            "--username", username,
            "--output", new_json,
        ]
        if password:
            cmd += ["--password", password]

        self.log(f"🚀 تشغيل: {' '.join(cmd[:6])} --password ***")
        self.log("")

        try:
            proc = subprocess.Popen(cmd, cwd=BASE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                    text=True, encoding="utf-8", bufsize=1)
            for line in proc.stdout:
                self.root.after(0, lambda l=line.rstrip(): self.log(f"  {l}"))
            proc.wait(timeout=300)

            if proc.returncode == 0 and os.path.exists(new_json):
                with open(new_json, encoding="utf-8") as f:
                    pages = json.load(f)
                self.root.after(0, lambda: self.log(f"\n✅ تم الزحف! {len(pages)} صفحة"))
            else:
                self.root.after(0, lambda: self.log(f"\n❌ فشل الزحف (رمز {proc.returncode})"))
        except Exception as e:
            self.root.after(0, lambda: self.log(f"\n❌ خطأ: {e}"))

        self.root.after(0, self.set_loading, False)

    # ===== BUTTON 2: COMPARE =====

    def run_compare(self):
        t = threading.Thread(target=self._do_compare, daemon=True)
        t.start()

    def _do_compare(self):
        self.root.after(0, lambda: self.output.delete("1.0", "end"))
        self.root.after(0, self.set_loading, True)
        self.root.after(0, lambda: self.log("📊 مقارنة المحتوى..."))

        OLD = os.path.join(BASE, "site_content.json")
        NEW = os.path.join(BASE, "site_content_new.json")

        if not os.path.exists(NEW):
            self.root.after(0, lambda: self.log("❌ site_content_new.json غير موجود — شغّل الزحف أولاً"))
            self.root.after(0, self.set_loading, False)
            return

        with open(OLD, encoding="utf-8") as f:
            old_list = json.load(f)
        with open(NEW, encoding="utf-8") as f:
            new_list = json.load(f)

        old_map = {p.get("url", "").rstrip("/"): p for p in old_list}
        new_map = {p.get("url", "").rstrip("/"): p for p in new_list}
        old_urls, new_urls = set(old_map.keys()), set(new_map.keys())

        added = new_urls - old_urls
        removed = old_urls - new_urls
        common = old_urls & new_urls

        same, changed = [], []
        for url in common:
            if old_map[url].get("content") == new_map[url].get("content"):
                same.append(url)
            else:
                changed.append(url)

        self.log(f"\n📊 الملخص:")
        self.log(f"   القديم: {len(old_list)} | الجديد: {len(new_list)}")
        self.log(f"   📗 جديد: {len(added)}  📝 متغير: {len(changed)}  📄 متطابق: {len(same)}  🗑️ محذوف: {len(removed)}")
        self.log("")

        for url in sorted(added):
            p = new_map[url]
            self.log(f"📗 جديد: {p['title']}")
            self.log(f"   المحتوى: {p.get('content', '')[:200]}...")
            self.log("")

        for url in sorted(changed):
            o, n = old_map[url], new_map[url]
            ol, nl = len(o.get("content", "")), len(n.get("content", ""))
            self.log(f"📝 متغير: {o['title']} ({ol}→{nl} حرف)")
            self.log("")

        for url in sorted(removed):
            self.log(f"🗑️ محذوف: {old_map[url]['title']}")

        if same:
            self.log(f"\n📄 متطابق ({len(same)}):")
            for url in sorted(same)[:5]:
                self.log(f"   {old_map[url]['title']}")
            if len(same) > 5:
                self.log(f"   ... +{len(same)-5}")

        ts = datetime.now().strftime("%Y%m%d_%H%M%S")
        os.makedirs(os.path.join(BASE, "backups"), exist_ok=True)
        shutil.copy2(OLD, os.path.join(BASE, "backups", f"backup_{ts}.json"))
        self.log(f"\n💾 نسخة احتياطية: backup_{ts}.json")

        self.root.after(0, self.set_loading, False)

    # ===== BUTTON 3: DEPLOY =====

    def run_deploy(self):
        t = threading.Thread(target=self._do_deploy, daemon=True)
        t.start()

    def _do_deploy(self):
        self.root.after(0, lambda: self.output.delete("1.0", "end"))
        self.root.after(0, self.set_loading, True)
        self.root.after(0, lambda: self.log("🚀 بدء النشر..."))

        NEW = os.path.join(BASE, "site_content_new.json")
        OLD = os.path.join(BASE, "site_content.json")

        if not os.path.exists(NEW):
            self.root.after(0, lambda: self.log("❌ site_content_new.json غير موجود"))
            self.root.after(0, self.set_loading, False)
            return

        try:
            with open(NEW, encoding="utf-8") as f:
                pages = json.load(f)
            if not pages:
                self.root.after(0, lambda: self.log("❌ الملف الجديد فارغ"))
                self.root.after(0, self.set_loading, False)
                return

            os.makedirs(os.path.join(BASE, "backups"), exist_ok=True)
            shutil.copy2(OLD, os.path.join(BASE, "backups", f"pre_deploy_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"))
            shutil.copy2(NEW, OLD)
            self.log("✅ site_content.json → محدث")
        except Exception as e:
            self.root.after(0, lambda: self.log(f"❌ {e}"))
            self.root.after(0, self.set_loading, False)
            return

        self.log("\n🧠 جاري بناء ChromaDB...")
        try:
            proc = subprocess.Popen(
                [sys.executable or "python", "build_knowledge_base.py"],
                cwd=BASE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                text=True, encoding="utf-8", bufsize=1,
            )
            for line in proc.stdout:
                self.root.after(0, lambda l=line.rstrip(): self.log(f"  {l}"))
            proc.wait(timeout=300)
            if proc.returncode == 0:
                self.root.after(0, lambda: self.log("\n✅ تم بناء قاعدة المعرفة بنجاح!"))
            else:
                self.root.after(0, lambda: self.log(f"\n❌ فشل البناء (رمز {proc.returncode})"))
        except subprocess.TimeoutExpired:
            self.root.after(0, lambda: self.log("❌ انتهت المهلة (أكثر من 5 دقائق)"))
        except Exception as e:
            self.root.after(0, lambda: self.log(f"❌ {e}"))

        self.log("")
        self.log("=" * 50)
        self.log("  ✅ تم النشر!")
        self.log("=" * 50)
        self.log("")
        self.log("للرفع على GitHub:")
        self.log("   git add ai-server/site_content.json")
        self.log('   git commit -m "تحديث محتوى الذكاء"')
        self.log("   git push")
        self.log("")
        self.log("ثم Render ← Manual Deploy ← Deploy latest commit")

        self.root.after(0, self.set_loading, False)


if __name__ == "__main__":
    root = tk.Tk()
    app = App(root)
    root.mainloop()
