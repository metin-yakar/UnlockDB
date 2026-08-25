# 🔓 AxarDB - Il Database NoSQL Nativo JavaScript

![AxarDB Logo](../wwwroot/AxarDBLogo.png)

[![Licenza: Metin YAKAR](https://img.shields.io/badge/License-Metin_YAKAR-blue.svg)](../LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?logo=docker&logoColor=white)](../Dockerfile)

> **AxarDB** è un server database NoSQL in-memory ad alte prestazioni che consente di scrivere query direttamente in **JavaScript**.

---

## 🌍 Lingue

| [English](../README.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [中文](README.zh.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [العربية](README.ar.md) | [Nederlands](README.nl.md) | [Български](README.bg.md) | [Italiano](README.it.md) | [Español](README.es.md) |
|---|---|---|---|---|---|---|---|---|---|---|

---

![AxarDB Web UI](../image.png)

## 🚀 Caratteristiche Principali

| Caratteristica | Descrizione |
|:---|:---|
| **📜 Query JavaScript** | Sintassi JS completa: `db.users.findall(x => x.active).toList()`. Supporta nuove estensioni come `count()` e `distinct()` sia su ResultSets che su array nativi. |
| **⚡ Alte Prestazioni** | Archiviazione in-memory con `ConcurrentDictionary`, Lazy Evaluation tramiite PLINQ e limite rigoroso del 40% di RAM per la cache dinamica. |
| **📄 Motore CSV** | Robusto supporto bidirezionale CSV. Converti testo in collezioni o collezioni in CSV tramite `csv(input)`. |
| **🛡️ Sicuro** | Basic Auth (supporta hash SHA256), **Protezione da Injection** & Protezione delle collezioni `sys` riservate. |
| **🛠️ Utilità** | Funzioni helper integrate: `md5`, `sha256`, `encrypt`, `random`, `base64`. |

---

## ⚙️ Configurazione

Le impostazioni del server sono memorizzate nella collezione di sistema `sysconfig`. Le modifiche a `memoryLimitPercentage`, `bulkStoreMaxCacheBytes`, `maxRecursionDepth`, `queryTimeoutMinutes` e `queuePollIntervalSeconds` hanno effetto dopo il riavvio. I nomi delle collezioni con prefisso `sys` sono riservati per l'infrastruttura interna.

```javascript
// Aggiornare la configurazione (richiede riavvio)
db.sysconfig.update(x => true, { queryTimeoutMinutes: 15 });
```

---

## 👨‍💻 Sviluppatore

**Metin YAKAR**  
*Sviluppatore Software & Esperto .NET*  
Istanbul, Turchia 🇹🇷

Esperienza **dal 2011**.

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0077B5?style=for-the-badge&logo=linkedin)](https://www.linkedin.com/in/metin-yakar/)

---

## 🤝 Supporto

### 💖 Sostieni il Progetto

| **Buy Me a Coffee** | **Ethereum** |
|:---:|:---:|
| <a href="https://buymeacoffee.com/metinyakar"><img src="../buymecoffie.png" style="width:100px;height:100px;"/></a> | ![QR](https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=0x1245CEB6c9A5485286975eEC1B2F9D496016761C) |

**[Prenota una consulenza (Cal.com)](https://cal.com/metin-yakar-dfij9e)**

---

## 📄 Licenza
**Open Source (Limitata)** - Vedi [LICENSE](../LICENSE).
