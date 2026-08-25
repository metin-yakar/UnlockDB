# 🔓 AxarDB - 原生 JavaScript NoSQL 数据库

![AxarDB Logo](../wwwroot/AxarDBLogo.png)

[![License: Metin YAKAR](https://img.shields.io/badge/License-Metin_YAKAR-blue.svg)](../LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?logo=docker&logoColor=white)](../Dockerfile)

> **AxarDB** 是一个高性能的内存 NoSQL 数据库，允许您直接使用 **JavaScript** 编写查询。基于 ASP.NET Core 8.0 构建。

---

## 🌍 语言

| [English](../README.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [中文](README.zh.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [العربية](README.ar.md) | [Nederlands](README.nl.md) | [Български](README.bg.md) | [Italiano](README.it.md) | [Español](README.es.md) |
|---|---|---|---|---|---|---|---|---|---|---|

---

![AxarDB Web UI](../image.png)

## 🚀 主要特性

| 特性 | 描述 |
|:---|:---|
| **📜 JavaScript 查询** | 完整的 JS 语法：`db.users.findall(x => x.active)`。在 ResultSets 和原生数组上支持 `count()` 和 `distinct()` 等新扩展。 |
| **⚡ 高性能** | 采用 `ConcurrentDictionary` 的内存存储，基于 PLINQ 的延迟计算（Lazy Evaluation），以及严格限制的 40% 动态 RAM 缓存。 |
| **📄 CSV 引擎** | 双向强大的 CSV 支持。通过 `csv(input)` 将文本转换为集合或将集合转换为 CSV。 |
| **🔍 智能索引** | 支持 ASC/DESC 索引。 |
| **🔗 连接 (Joins)** | 集合连接： `db.join(users, orders)`。 |
| **🛡️ 安全** | 基本认证 (支持 SHA256 哈希), 注入防护和保护保留的 `sys` 前缀集合。 |
| **🛠️ 实用工具** | 内置辅助函数：`md5`, `sha256`, `encrypt`, `random`, `base64`。 |

---

## ⚙️ 配置

服务器设置存储在 `sysconfig` 系统集合中。`memoryLimitPercentage`、`bulkStoreMaxCacheBytes`、`maxRecursionDepth`、`queryTimeoutMinutes` 和 `queuePollIntervalSeconds` 的更改在重启后生效。以 `sys` 为前缀的集合名称保留用于内部基础设施。

```javascript
// 更新配置 (需要重启)
db.sysconfig.update(x => true, { queryTimeoutMinutes: 15 });
```

---

## 👨‍💻 开发者

**Metin YAKAR**  
*软件开发人员 & .NET 专家*  
土耳其，伊斯坦布尔 🇹🇷

拥有 **始于 2011 年** 的经验。

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0077B5?style=for-the-badge&logo=linkedin)](https://www.linkedin.com/in/metin-yakar/)

---

## 🤝 支持与贡献

我们需要您的帮助！
- [ ] 高级配置
- [ ] 实时同步
- [ ] 集群监控

### 💖 赞助项目

| **Buy Me a Coffee** | **Ethereum** |
|:---:|:---:|
| <a href="https://buymeacoffee.com/metinyakar"><img src="../buymecoffie.png" style="width:100px;height:100px;"/></a> | ![QR](https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=0x1245CEB6c9A5485286975eEC1B2F9D496016761C) |

**[预约咨询 (Cal.com)](https://cal.com/metin-yakar-dfij9e)**

---

## 📄 许可证
**开源 (受限)** - 详见 [LICENSE](../LICENSE)。
