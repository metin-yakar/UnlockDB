# 🔓 AxarDB - NoSQL База данных на JavaScript

![AxarDB Logo](../wwwroot/AxarDBLogo.png)

[![Лицензия: Metin YAKAR](https://img.shields.io/badge/License-Metin_YAKAR-blue.svg)](../LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?logo=docker&logoColor=white)](../Dockerfile)

> **AxarDB** — это высокопроизводительная NoSQL база данных в памяти (in-memory), позволяющая писать запросы непосредственно на **JavaScript**. Построена на платформе ASP.NET Core 8.0.

---

## 🌍 Языки

| [English](../README.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [中文](README.zh.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [العربية](README.ar.md) | [Nederlands](README.nl.md) | [Български](README.bg.md) | [Italiano](README.it.md) | [Español](README.es.md) |
|---|---|---|---|---|---|---|---|---|---|---|

---

![AxarDB Web UI](../image.png)

## 🚀 Ключевые Возможности

| Возможность | Описание |
|:---|:---|
| **📜 JavaScript Запросы** | Полный синтаксис JS: `db.users.findall(x => x.active)`. Поддерживает новые расширения, такие как `count()` и `distinct()`, как в ResultSets, так и в нативных массивах. |
| **⚡ Высокая Производительность** | Хранение в памяти с `ConcurrentDictionary`, ленивые вычисления (Lazy Evaluation) через PLINQ и строгий динамический лимит кэша в размере 40% ОЗУ. |
| **📄 Движок CSV** | Двунаправленная надежная поддержка CSV. Преобразуйте текст в коллекции или коллекции в CSV с помощью `csv(input)`. |
| **🔍 Умная Индексация** | Поддержка ASC/DESC индексов. |
| **🔗 Объединения (Joins)** | Объединение коллекций: `db.join(users, orders)`. |
| **🛡️ Безопасность** | Basic Auth (поддержка хеша SHA256), защита от инъекций и защита системных коллекций с префиксом `sys`. |
| **🛠️ Утилиты** | Встроенные функции: `md5`, `sha256`, `encrypt`, `random`, `base64`. |

---

## ⚙️ Конфигурация

Настройки сервера хранятся в системной коллекции `sysconfig`. Изменения `memoryLimitPercentage`, `bulkStoreMaxCacheBytes`, `maxRecursionDepth`, `queryTimeoutMinutes` и `queuePollIntervalSeconds` вступают в силу только после перезапуска. Имена коллекций с префиксом `sys` зарезервированы для внутренней инфраструктуры.

```javascript
// Обновление конфигурации (требуется перезапуск)
db.sysconfig.update(x => true, { queryTimeoutMinutes: 15 });
```

---

## 👨‍💻 Разработчик

**Метин ЯКАР (Metin YAKAR)**  
*Разработчик ПО & .NET Эксперт*  
Стамбул, Турция 🇹🇷

Опыт **с 2011 года**.

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0077B5?style=for-the-badge&logo=linkedin)](https://www.linkedin.com/in/metin-yakar/)

---

## 🤝 Поддержка

Мы ищем контрибьюторов!
- [ ] Расширенная конфигурация
- [ ] Синхронизация в реальном времени
- [ ] Мониторинг

### 💖 Поддержать проект

| **Buy Me a Coffee** | **Ethereum** |
|:---:|:---:|
| <a href="https://buymeacoffee.com/metinyakar"><img src="../buymecoffie.png" style="width:100px;height:100px;"/></a> | ![QR](https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=0x1245CEB6c9A5485286975eEC1B2F9D496016761C) |

**[Записаться на консультацию (Cal.com)](https://cal.com/metin-yakar-dfij9e)**

---

## 📄 Лицензия
**Open Source (Ограниченная)** - См. [LICENSE](../LICENSE).
