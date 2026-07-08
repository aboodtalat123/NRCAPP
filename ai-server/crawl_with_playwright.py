#!/usr/bin/env python3
"""
Non-interactive Playwright crawler for NRCAPP.
Usage:
    python crawl_with_playwright.py --site http://localhost:8080 --role org --username 123456 --password 123456
    python crawl_with_playwright.py --site https://nrcapp.onrender.com --role citizen --username NATIONAL_ID
    python crawl_with_playwright.py --site http://localhost:8080 --role admin --username admin --password 123456
"""
import argparse, json, os, sys
from urllib.parse import urljoin, urlparse
from playwright.sync_api import sync_playwright

KNOWN_STATIC = [
    "/", "/admin/login", "/org/login", "/org/register",
    "/citizen/login", "/citizen/register",
    "/aid-distribution", "/analytics", "/gap-detector",
    "/volunteers", "/settings",
]

def get_title(path):
    titles = {
        "/": "نقطة - بوابة الدخول",
        "/admin/login": "نقطة - دخول مسؤول النظام", "/admin/dashboard": "نقطة - لوحة الأدمن",
        "/org/login": "نقطة - دخول المؤسسة", "/org/register": "نقطة - تسجيل مؤسسة", "/org/dashboard": "نقطة - لوحة المؤسسة",
        "/citizen/login": "نقطة - دخول المواطن", "/citizen/register": "نقطة - تسجيل مواطن", "/citizen/profile": "نقطة - ملف المواطن",
        "/aid-distribution": "نقطة - الجدولة والتوزيع",
        "/analytics": "نقطة - التحليلات",
        "/gap-detector": "نقطة - كشف الفجوات",
        "/volunteers": "نقطة - المتطوعون",
        "/settings": "نقطة - الإعدادات",
    }
    return titles.get(path, f"نقطة - {path.strip('/')}")

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

def extract_text(page):
    try:
        return page.locator("body").inner_text(timeout=5000)[:5000]
    except:
        return ""

def crawl(site_url, role, username, password, max_pages=30):
    site_url = site_url.rstrip("/")
    pages = []
    visited = set()
    to_visit = [site_url + "/"]

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()

        print(f"🔐 {role}@{site_url} ...", flush=True)
        try:
            do_login(page, site_url, role, username, password)
            print("✅ تم تسجيل الدخول", flush=True)
        except Exception as e:
            print(f"❌ فشل تسجيل الدخول: {e}", flush=True)
            browser.close()
            return []

        while to_visit and len(pages) < max_pages:
            url = to_visit.pop(0)
            if url in visited:
                continue
            visited.add(url)

            try:
                page.goto(url, timeout=20000)
                page.wait_for_load_state("networkidle")
            except Exception as e:
                continue

            path = urlparse(url).path
            title = page.title() or get_title(path)
            content = extract_text(page) or title

            pages.append({
                "url": f"https://nrcapp.onrender.com{path}",
                "title": title,
                "content": content,
            })
            print(f"  ✅ ({len(pages)}) {title} — {len(content)} حرف", flush=True)

            try:
                hrefs = page.locator("a").evaluate_all("els => els.map(e => e.getAttribute('href'))")
                for h in hrefs:
                    if not h:
                        continue
                    full = urljoin(url, h)
                    if full not in visited and full not in to_visit and urlparse(site_url).netloc == urlparse(full).netloc:
                        to_visit.append(full)
            except:
                pass

        browser.close()

    return pages

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--site", default="https://nrcapp.onrender.com")
    parser.add_argument("--role", choices=["org", "citizen", "admin"], default="org")
    parser.add_argument("--username", required=True)
    parser.add_argument("--password", default="")
    parser.add_argument("--output", default="")
    args = parser.parse_args()

    pages = crawl(args.site, args.role, args.username, args.password)

    output = args.output or os.path.join(os.path.dirname(os.path.abspath(__file__)), "site_content_new.json")
    with open(output, "w", encoding="utf-8") as f:
        json.dump(pages, f, ensure_ascii=False, indent=2)

    print(f"\n✅ {len(pages)} صفحة → {output}", flush=True)

if __name__ == "__main__":
    main()
