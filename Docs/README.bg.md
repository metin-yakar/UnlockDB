# 🔓 AxarDB - NoSQL база данни на JavaScript

![AxarDB Logo](../wwwroot/AxarDBLogo.png)

[![Лиценз: Metin YAKAR](https://img.shields.io/badge/License-Metin_YAKAR-blue.svg)](../LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?logo=docker&logoColor=white)](../Dockerfile)

> **AxarDB** е високопроизводителна in-memory NoSQL база данни, която позволява писане на заявки директно на **JavaScript**.

---

## 🌍 Езици

| [English](../README.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [中文](README.zh.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [العربية](README.ar.md) | [Nederlands](README.nl.md) | [Български](README.bg.md) | [Italiano](README.it.md) | [Español](README.es.md) |
|---|---|---|---|---|---|---|---|---|---|---|

---

![AxarDB Web UI](../image.png)

## 🚀 Основни характеристики

| Характеристика | Описание |
|:---|:---|
| **📜 JavaScript заявки** | Пълен JS синтаксис: `db.users.findall(x => x.active)`. Поддържа нови разширения като `count()` и `distinct()` както за ResultSets, така и за Native масиви. |
| **⚡ Висока производителност** | Съхранение в паметта с `ConcurrentDictionary`, мързеливо оценяване (Lazy Evaluation) чрез PLINQ и строго ограничено динамично кеширане в RAM паметта до 40%. |
| **📄 CSV Двигател** | Двупосочна поддръжка на CSV. Преобразувайте текст в колекции или колекции в CSV чрез `csv(input)`. |
| **🛡️ Сигурност** | Basic Auth (поддържа SHA256 хеш), **Защита от инжекции** и защита на резервирани `sys` колекции. |
| **🛠️ Инструменти** | Вградени помощни функции: `md5`, `sha256`, `encrypt`, `random`, `base64`. |

---

## ⚙️ Конфигурация

Настройките на сървъра се съхраняват в системната колекция `sysconfig`. Промените на `memoryLimitPercentage`, `bulkStoreMaxCacheBytes`, `maxRecursionDepth`, `queryTimeoutMinutes` и `queuePollIntervalSeconds` влизат в сила след рестартиране. Имената на колекции с префикс `sys` са резервирани за вътрешната инфраструктура.

```javascript
// Актуализиране на конфигурацията (изисква рестартиране)
db.sysconfig.update(x => true, { queryTimeoutMinutes: 15 });
```

---

## 👨‍💻 Разработчик

**Metin YAKAR**  
*Софтуерен разработчик & .NET експерт*  
Истанбул, Турция 🇹🇷

Опит **от 2011 г.**

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0077B5?style=for-the-badge&logo=linkedin)](https://www.linkedin.com/in/metin-yakar/)

---

## 🤝 Подкрепа

### 💖 Подкрепете проекта

| **Buy Me a Coffee** | **Ethereum** |
|:---:|:---:|
| <a href="https://buymeacoffee.com/metinyakar"><img src="../buymecoffie.png" style="width:100px;height:100px;"/></a> | ![QR](https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=0x1245CEB6c9A5485286975eEC1B2F9D496016761C) |

**[Резервирай консултация (Cal.com)](https://cal.com/metin-yakar-dfij9e)**

---

## 📄 Лиценз
**Open Source (Ограничен)** - Вижте [LICENSE](../LICENSE).
