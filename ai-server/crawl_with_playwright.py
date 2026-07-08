#!/usr/bin/env python3
"""
Non-interactive Playwright crawler.
Reads config from config.json + .env by default.
Override any value via CLI args.
"""
import argparse, json, os, sys
from urllib.parse import urljoin, urlparse
from playwright.sync_api import sync_playwright

import config as cfg

DANGEROUS_KEYWORDS = ["delete", "remove", "logout", "reject", "cancel", "حذف", "خروج", "رفض", "الغاء", "إلغاء"]
IGNORED_PREFIXES = ("mailto:", "tel:", "javascript:", "#")
IGNORED_EXTENSIONS = (".pdf", ".jpg", ".png", ".zip", ".css", ".js", ".ico")

def get_title(path):
    titles = {}
    for r in cfg.get("routes", []):
        if r == path:
            parts = path.strip("/").split("/")
            name = parts[-1] if parts[-1] else "الرئيسية"
            return f"{cfg.get('site_name', 'الموقع')} - {name}"
    return cfg.get("site_name", "الموقع")

def do_login(page, site_url, role, username, password):
    paths = {"org": "/org/login", "citizen": "/citizen/login", "admin": "/admin/login"}
    page.goto(site_url + paths[role], timeout=20000)
    page.wait_for_load_state("networkidle")

    text_input = page.locator("input[type='text'], input[type='number'], input:not([type])").first
    text_input.fill(username)

    if role != "citizen":
        pwd = page.locator("input[type='password']").first
        pwd.fill(password)

    page.locator("button[type='submit'], input[type='submit'], button").first.click()
    page.wait_for_load_state("networkidle")

def crawl(site_url, role, username, password, routes, max_pages=30):
    site_url = site_url.rstrip("/")
    pages = []
    visited = set()

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()

        print(f"[...] {role}@{site_url}", flush=True)
        try:
            do_login(page, site_url, role, username, password)
            print("[تم] تسجيل الدخول", flush=True)
        except Exception as e:
            print(f"[خطأ] فشل تسجيل الدخول: {e}", flush=True)
            browser.close()
            return []

        for path in routes:
            url = site_url + path
            if url in visited:
                continue
            visited.add(url)
            try:
                page.goto(url, timeout=20000)
                page.wait_for_load_state("networkidle")
            except Exception as e:
                print(f"  [تخطي] {path}", flush=True)
                continue

            title = page.title() or get_title(path)
            content = ""
            try:
                content = page.locator("body").inner_text(timeout=5000)[:5000]
            except:
                content = title

            pages.append({
                "url": f"{cfg.get('site_url')}{path}",
                "title": title,
                "content": content or title,
            })
            print(f"  [جيد] ({len(pages)}) {title} — {len(content)} حرف", flush=True)

        browser.close()
    return pages

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--site", default="")
    parser.add_argument("--role", choices=["org", "citizen", "admin"], default="")
    parser.add_argument("--username", default="")
    parser.add_argument("--password", default="")
    parser.add_argument("--output", default="")
    args = parser.parse_args()

    site_url = args.site or cfg.get("site_url", "")
    role = args.role or "org"
    account = cfg.get_account(role)
    username = args.username or (account.get("username", "") if account else "")
    password = args.password or (account.get("password", "") if account else "")
    routes = cfg.get("routes", ["/"])
    output = args.output or os.path.join(cfg.get("_base", "."), "site_content_new.json")

    if not site_url:
        print("[خطأ] site_url غير موجود في config.json ولا CLI")
        sys.exit(1)
    if not username:
        print(f"[خطأ] لا يوجد حساب للدور '{role}' في config.accounts")
        sys.exit(1)

    print(f"[...] {site_url} | {role} | {username}", flush=True)
    pages = crawl(site_url, role, username, password, routes)

    with open(output, "w", encoding="utf-8") as f:
        json.dump(pages, f, ensure_ascii=False, indent=2)

    print(f"\n[تم] {len(pages)} صفحة -> {output}", flush=True)

if __name__ == "__main__":
    main()
