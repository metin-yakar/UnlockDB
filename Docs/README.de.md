# 🔓 AxarDB - Die JavaScript-NoSQL-Datenbank

![AxarDB Logo](../wwwroot/AxarDBLogo.png)

[![Lizenz: Metin YAKAR](https://img.shields.io/badge/License-Metin_YAKAR-blue.svg)](../LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?logo=docker&logoColor=white)](../Dockerfile)

> **AxarDB** ist eine leistungsstarke In-Memory-NoSQL-Datenbank, die es Ihnen ermöglicht, Datenbankabfragen direkt in **JavaScript** zu schreiben.

---

## 🌍 Sprachen

| [English](../README.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [中文](README.zh.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [العربية](README.ar.md) | [Nederlands](README.nl.md) | [Български](README.bg.md) | [Italiano](README.it.md) | [Español](README.es.md) |
|---|---|---|---|---|---|---|---|---|---|---|

---

![AxarDB Web UI](../image.png)

## 🚀 Hauptmerkmale

| Merkmal | Beschreibung |
|:---|:---|
| **📜 JavaScript-Abfragen** | Volle JS-Syntax: `db.users.findall(x => x.active).toList()`. Unterstützt neue Erweiterungen wie `count()` und `distinct()` sowohl auf ResultSets als auch auf nativen Arrays. |
| **⚡ Hohe Leistung** | In-Memory-Speicher mit `ConcurrentDictionary`, Lazy Evaluation über PLINQ und streng begrenztem dynamischen 40% RAM-Cache. |
| **📄 CSV Engine** | Bidirektionale robuste CSV-Unterstützung. Konvertieren Sie Text in Sammlungen oder Sammlungen in CSV über `csv(input)`. |
| **🔍 Intelligente Indizierung** | ASC/DESC Indexe. |
| **🔗 Joins** | Sammlungen verknüpfen: `db.join(users, orders)`. |
| **🛡️ Sicher** | Basic Auth (unterstützt SHA256-Hashing), **Injektionsschutz** & Schutz reservierter `sys`-Sammlungen. |
| **🛠️ Hilfsprogramme** | Integrierte Hilfsfunktionen: `md5`, `sha256`, `encrypt`, `random`, `base64`. |

---

## ⚙️ Konfiguration

Servereinstellungen werden in der `sysconfig` Systemkollektion gespeichert. Änderungen an `memoryLimitPercentage`, `bulkStoreMaxCacheBytes`, `maxRecursionDepth`, `queryTimeoutMinutes` und `queuePollIntervalSeconds` werden erst nach einem Neustart wirksam. Sammlungsnamen mit dem Präfix `sys` sind für die interne Infrastruktur reserviert.

```javascript
// Konfiguration aktualisieren (Neustart erforderlich)
db.sysconfig.update(x => true, { queryTimeoutMinutes: 15 });
```

---

## 👨‍💻 Entwickler

**Metin YAKAR**  
*Softwareentwickler & .NET Experte*  
Istanbul, Türkei 🇹🇷

Erfahrung **seit 2011**.

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0077B5?style=for-the-badge&logo=linkedin)](https://www.linkedin.com/in/metin-yakar/)

---

## 🤝 Unterstützung & Beitrag

Wir suchen Mitwirkende!
- [ ] Erweiterte Konfiguration
- [ ] Echtzeit-Synchronisation
- [ ] Überwachung

### 💖 Unterstützen

| **Buy Me a Coffee** | **Ethereum** |
|:---:|:---:|
| <a href="https://buymeacoffee.com/metinyakar"><img src="../buymecoffie.png" style="width:100px;height:100px;"/></a> | ![QR](https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=0x1245CEB6c9A5485286975eEC1B2F9D496016761C) |

**[Beratung buchen (Cal.com)](https://cal.com/metin-yakar-dfij9e)**

---

## 📄 Lizenz
**Open Source (Eingeschränkt)** - Siehe [LICENSE](../LICENSE).
