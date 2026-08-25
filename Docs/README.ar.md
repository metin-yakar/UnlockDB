# 🔓 AxarDB - قاعدة بيانات NoSQL تعتمد على JavaScript

![AxarDB Logo](../wwwroot/AxarDBLogo.png)

[![الرخصة: Metin YAKAR](https://img.shields.io/badge/License-Metin_YAKAR-blue.svg)](../LICENSE)

> **AxarDB** هو خادم قاعدة بيانات NoSQL عالي الأداء في الذاكرة يسمح لك بكتابة استعلامات قاعدة البيانات مباشرة باستخدام **JavaScript**.

---

## 🌍 اللغات

| [English](../README.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [中文](README.zh.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [العربية](README.ar.md) | [Nederlands](README.nl.md) | [Български](README.bg.md) | [Italiano](README.it.md) | [Español](README.es.md) |
|---|---|---|---|---|---|---|---|---|---|---|

---

![AxarDB Web UI](../image.png)

## 🚀 الميزات الرئيسية

| الميزة | الوصف |
|:---|:---|
| **📜 استعلامات JavaScript** | استخدم صيغة JS الكاملة: `db.users.findall(x => x.active)`. يدعم ملحقات جديدة مثل `count()` و `distinct()` على كل من ResultSets والمصفوفات الأصلية. |
| **⚡ أداء عالي** | تخزين في الذاكرة باستخدام `ConcurrentDictionary`، والتقييم الكسول عبر PLINQ، وانتهاء صلاحية ذاكرة التخزين المؤقت الديناميكية بنسبة 40٪. |
| **📄 محرك CSV** | دعم CSV قوي وثنائي الاتجاه. قم بتحويل النص إلى مجموعات أو المجموعات إلى CSV عبر `csv(input)`. |
| **🛡️ آمن** | المصادقة الأساسية (دعم هاش SHA256)، **منع الحقن**، وحماية المجموعات المحجوزة ذات البادئة `sys`. |
| **🛠️ أدوات مساعدة** | وظائف مساعدة مدمجة: `md5`, `sha256`, `encrypt`, `random`, `base64`. |

---

## ⚙️ الإعدادات

يتم تخزين إعدادات الخادم في مجموعة النظام `sysconfig`. التغييرات على `memoryLimitPercentage` و`bulkStoreMaxCacheBytes` و`maxRecursionDepth` و`queryTimeoutMinutes` و`queuePollIntervalSeconds` تسري بعد إعادة التشغيل. أسماء المجموعات التي تبدأ بـ `sys` محجوزة للبنية التحتية الداخلية.

```javascript
// تحديث الإعدادات (يتطلب إعادة التشغيل)
db.sysconfig.update(x => true, { queryTimeoutMinutes: 15 });
```

---

## 👨‍💻 المطور

**متين ياكار (Metin YAKAR)**  
*مطور برمجيات وخبير .NET*  
إسطنبول، تركيا 🇹🇷

خبرة **منذ عام 2011**.

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0077B5?style=for-the-badge&logo=linkedin)](https://www.linkedin.com/in/metin-yakar/)

---

## 🤝 الدعم والمساهمة

نبحث عن مساهمين!

### 💖 دعم المشروع

| **Buy Me a Coffee** | **Ethereum** |
|:---:|:---:|
| <a href="https://buymeacoffee.com/metinyakar"><img src="../buymecoffie.png" style="width:100px;height:100px;"/></a> | ![QR](https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=0x1245CEB6c9A5485286975eEC1B2F9D496016761C) |

**[حجز استشارة (Cal.com)](https://cal.com/metin-yakar-dfij9e)**

---

## 📄 الرخصة
**مفتوح المصدر (مقيد)** - انظر [LICENSE](../LICENSE).
