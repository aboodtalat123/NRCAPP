# نشر NRCAPP

هذا المشروع تطبيق ASP.NET Core Blazor Server، لذلك يحتاج استضافة Backend وليس استضافة ملفات static فقط.

## تشغيل Docker

```bash
docker build -t nrcapp .
docker run -p 8080:8080 nrcapp
```

ثم افتح:

```text
http://localhost:8080
```

## بيانات تجربة جاهزة

مؤسسة:

```text
UNRWA-GZA-001
123456
```

مواطن:

```text
900112233
الرمال
```

## ملاحظات نشر

- المنفذ داخل الحاوية هو `8080`.
- بدون Connection String يستخدم التطبيق قاعدة بيانات InMemory للعرض التجريبي.
- للإنتاج الحقيقي أضف Connection String باسم `ConnectionStrings__ReliefDb` على منصة الاستضافة.
