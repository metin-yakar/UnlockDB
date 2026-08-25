# 🔓 AxarDB - De JavaScript-Native NoSQL Database

![AxarDB Logo](../wwwroot/AxarDBLogo.png)

[![Licentie: Metin YAKAR](https://img.shields.io/badge/License-Metin_YAKAR-blue.svg)](../LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?logo=docker&logoColor=white)](../Dockerfile)

> **AxarDB** is een high-performance in-memory NoSQL database server waarmee je queries direct in **JavaScript** kunt schrijven.

---

## 🌍 Talen

| [English](../README.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [中文](README.zh.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [العربية](README.ar.md) | [Nederlands](README.nl.md) | [Български](README.bg.md) | [Italiano](README.it.md) | [Español](README.es.md) |
|---|---|---|---|---|---|---|---|---|---|---|

---

![AxarDB Web UI](../image.png)

## 🚀 Kernfuncties

| Functie | Beschrijving |
|:---|:---|
| **📜 JavaScript Queries** | Gebruik volledige JS syntax: `db.users.findall(x => x.active).toList()`. Ondersteunt nieuwe functies zoals `count()` en `distinct()` op zowel ResultSets als Native Arrays. |
| **⚡ Hoge Prestaties** | In-memory opslag met `ConcurrentDictionary`, Luie Evaluatie (Lazy Evaluation) via PLINQ en een strikte dynamische cachelimiet van 40% RAM. |
| **📄 CSV Engine** | Bidirectionele robuuste CSV-ondersteuning. Converteer tekst naar collecties of collecties naar CSV via `csv(input)`. |
| **🛡️ Veilig** | Basic Auth (ondersteunt SHA256-hash), **Injectie Preventie** & Bescherming van gereserveerde `sys` collecties. |
| **🛠️ Hulpfuncties** | Ingebouwde functies: `md5`, `sha256`, `encrypt`, `random`, `base64`. |

---

## ⚙️ Configuratie

Serverinstellingen worden opgeslagen in de `sysconfig` systeemcollectie. Wijzigingen aan `memoryLimitPercentage`, `bulkStoreMaxCacheBytes`, `maxRecursionDepth`, `queryTimeoutMinutes` en `queuePollIntervalSeconds` worden pas actief na een herstart. Collectienamen met het voorvoegsel `sys` zijn gereserveerd voor interne infrastructuur.

```javascript
// Configuratie bijwerken (herstart vereist)
db.sysconfig.update(x => true, { queryTimeoutMinutes: 15 });
```

---

## 👨‍💻 Ontwikkelaar

**Metin YAKAR**  
*Softwareontwikkelaar & .NET Expert*  
Istanbul, Turkije 🇹🇷

Ervaring **sinds 2011**.

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0077B5?style=for-the-badge&logo=linkedin)](https://www.linkedin.com/in/metin-yakar/)

---

## 🤝 Ondersteuning

### 💖 Steun het Project

| **Buy Me a Coffee** | **Ethereum** |
|:---:|:---:|
| <a href="https://buymeacoffee.com/metinyakar"><img src="../buymecoffie.png" style="width:100px;height:100px;"/></a> | ![QR](https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=0x1245CEB6c9A5485286975eEC1B2F9D496016761C) |

**[Boek een sessie op Cal.com](https://cal.com/metin-yakar-dfij9e)**

---

## 📄 Licentie
**Open Source (Beperkt)** - Zie [LICENSE](../LICENSE).
