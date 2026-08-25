# 🔓 AxarDB - La Base de Datos NoSQL Nativa de JavaScript

![AxarDB Logo](../wwwroot/AxarDBLogo.png)

[![Licencia: Metin YAKAR](https://img.shields.io/badge/License-Metin_YAKAR-blue.svg)](../LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?logo=docker&logoColor=white)](../Dockerfile)

> **AxarDB** es un servidor de base de datos NoSQL en memoria de alto rendimiento que le permite escribir consultas de base de datos directamente en **JavaScript**.

---

## 🌍 Idiomas

| [English](../README.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [中文](README.zh.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [العربية](README.ar.md) | [Nederlands](README.nl.md) | [Български](README.bg.md) | [Italiano](README.it.md) | [Español](README.es.md) |
|---|---|---|---|---|---|---|---|---|---|---|

---

![AxarDB Web UI](../image.png)

## 🚀 Características Clave

| Característica | Descripción |
|:---|:---|
| **📜 Consultas JavaScript** | Sintaxis JS completa: `db.users.findall(x => x.active)`. Soporta nuevas extensiones como `count()` y `distinct()` tanto en ResultSets como en matrices nativas. |
| **⚡ Alto Rendimiento** | Almacenamiento en memoria con `ConcurrentDictionary`, evaluación diferida (Lazy Evaluation) vía PLINQ, y límite estricto del 40% de RAM para caché dinámico. |
| **📄 Motor CSV** | Soporte bidireccional robusto para CSV. Convierta texto a colecciones o colecciones a CSV mediante `csv(input)`. |
| **🛡️ Seguro** | Autenticación básica (soporta hash SHA256), **prevención de inyecciones** y protección de colecciones reservadas con prefijo `sys`. |
| **🛠️ Utilidades** | Funciones auxiliares integradas: `md5`, `sha256`, `encrypt`, `random`, `base64`. |

---

## ⚙️ Configuración

Los ajustes del servidor se almacenan en la colección de sistema `sysconfig`. Los cambios en `memoryLimitPercentage`, `bulkStoreMaxCacheBytes`, `maxRecursionDepth`, `queryTimeoutMinutes` y `queuePollIntervalSeconds` surten efecto tras reiniciar. Los nombres de colección con prefijo `sys` están reservados para la infraestructura interna.

```javascript
// Actualizar configuración (requiere reinicio)
db.sysconfig.update(x => true, { queryTimeoutMinutes: 15 });
```

---

## 👨‍💻 Desarrollador

**Metin YAKAR**  
*Desarrollador de Software y Experto en .NET*  
Estambul, Turquía 🇹🇷

Experiencia **desde 2011**.

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0077B5?style=for-the-badge&logo=linkedin)](https://www.linkedin.com/in/metin-yakar/)

---

## 🤝 Soporte

### 💖 Apoya el Proyecto

| **Buy Me a Coffee** | **Ethereum** |
|:---:|:---:|
| <a href="https://buymeacoffee.com/metinyakar"><img src="../buymecoffie.png" style="width:100px;height:100px;"/></a> | ![QR](https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=0x1245CEB6c9A5485286975eEC1B2F9D496016761C) |

**[Reserva una sesión (Cal.com)](https://cal.com/metin-yakar-dfij9e)**

---

## 📄 Licencia
**Código Abierto (Restringido)** - Ver [LICENSE](../LICENSE).
