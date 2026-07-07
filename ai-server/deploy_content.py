import os

BASE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(BASE)

print("="*60)
print("  الأداة 3: نشر التحديثات - نقطة")
print("="*60)

print(f"\nالمستودع: {REPO}")
print(f"\nلرفع التعديلات على GitHub:")

msg = input("\nملخص التغيير (مثلاً: تحديث محتوى الذكاء): ").strip() or "تحديث محتوى الذكاء"

print(f"\nانسخ هذه الأوامر بالترتيب:\n")
print(f"1. cd {REPO}")
print(f'2. git add ai-server/site_content.json')
print(f'3. git commit -m "{msg}"')
print(f"4. git push")
print(f"\n5. اذهب https://dashboard.render.com")
print(f"6. خدمة nrcapp-ai-assistant ← Manual Deploy ← Deploy latest commit")
print(f"\n✅ بعد 3 دقايق اختبر:")
print(f"   https://nrcapp-ai-assistant.onrender.com/health")
