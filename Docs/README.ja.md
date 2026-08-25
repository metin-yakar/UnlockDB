# 🔓 AxarDB - JavaScript ネイティブ NoSQL データベース

![AxarDB Logo](../wwwroot/AxarDBLogo.png)

[![ライセンス: Metin YAKAR](https://img.shields.io/badge/License-Metin_YAKAR-blue.svg)](../LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?logo=docker&logoColor=white)](../Dockerfile)

> **AxarDB** は、**JavaScript** で直接データベースクエリを記述できる、高性能なインメモリ NoSQL データベースサーバーです。

---

## 🌍 言語

| [English](../README.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [中文](README.zh.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [العربية](README.ar.md) | [Nederlands](README.nl.md) | [Български](README.bg.md) | [Italiano](README.it.md) | [Español](README.es.md) |
|---|---|---|---|---|---|---|---|---|---|---|

---

![AxarDB Web UI](../image.png)

## 🚀 主な機能

| 機能 | 説明 |
|:---|:---|
| **📜 JavaScript クエリ** | 完全な JS 構文: `db.users.findall(x => x.active)`。 ResultSet および ネイティブ配列の両方で `count()` や `distinct()` などの新しい拡張機能がサポートされます。 |
| **⚡ 高パフォーマンス** | `ConcurrentDictionary` を使用したインメモリ ストレージ、PLINQによる遅延評価（Lazy Evaluation）、および厳格な40%のRAMキャッシュ制限。 |
| **📄 CSV エンジン** | 双方向の堅牢なCSVサポート。 `csv(input)` でテキストをコレクションに、またはコレクションをCSVに変換します。 |
| **🔍 スマート インデックス** | ASC/DESC インデックス作成。 |
| **🔗 ジョイン** | コレクション間の結合: `db.join(users, orders)`. |
| **🛡️ セキュア** | Basic認証 (SHA256ハッシュ対応)、**インジェクション防止**、予約された `sys` プレフィックスコレクションの保護。 |
| **🛠️ ユーティリティ** | 組み込みヘルパー関数：`md5`、`sha256`、`encrypt`、`random` など。 |

---

## ⚙️ 設定

サーバー設定は `sysconfig` システムコレクションに保存されます。`memoryLimitPercentage`、`bulkStoreMaxCacheBytes`、`maxRecursionDepth`、`queryTimeoutMinutes`、`queuePollIntervalSeconds` の変更は再起動後に反映されます。`sys` プレフィックスを持つコレクション名は内部インフラストラクチャ用に予約されています。

```javascript
// 設定の更新 (再起動が必要)
db.sysconfig.update(x => true, { queryTimeoutMinutes: 15 });
```

---

## 👨‍💻 開発者

**Metin YAKAR**  
*ソフトウェア開発者 & .NET エキスパート*  
イスタンブール、トルコ 🇹🇷

**2011年** からの経験。

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0077B5?style=for-the-badge&logo=linkedin)](https://www.linkedin.com/in/metin-yakar/)

---

## 🤝 貢献とサポート

コントリビューターを募集中です！
- [ ] 高度な設定システム
- [ ] リアルタイム同期
- [ ] モニタリング

### 💖 プロジェクトのサポート

| **Buy Me a Coffee** | **Ethereum** |
|:---:|:---:|
| <a href="https://buymeacoffee.com/metinyakar"><img src="../buymecoffie.png" style="width:100px;height:100px;"/></a> | ![QR](https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=0x1245CEB6c9A5485286975eEC1B2F9D496016761C) |

**[コンサルティングの予約 (Cal.com)](https://cal.com/metin-yakar-dfij9e)**

---

## 📄 ライセンス
**オープンソース (制限付き)** - 詳細は [LICENSE](../LICENSE) を参照してください。
