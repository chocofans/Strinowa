# <img src="https://files.catbox.moe/05krid.png" width="32" height="32" valign="middle"> Strinova Downloader

A high-performance, raw WPF (.NET 9) desktop utility built for downloading the **Strinova/Calabiyau** Game client and Launcher client. It bypasses restrictive launcher speeds from the original launcher.

[![Platform](https://img.shields.io/badge/Platform-Windows-0078d4?style=flat-square&logo=windows)](https://dotnet.microsoft.com/)
[![Framework](https://img.shields.io/badge/.NET-9.0_WPF-512bd4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/en-us/apps/wpf)
[![Language](https://img.shields.io/badge/Language-C%23_13-239120?style=flat-square&logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/License-PolyForm_Noncommercial-red.svg)](LICENSE.txt)

## ⚡ Core Architecture

* **WPF Foundation GUI:** Zero-bloat Windows Presentation Foundation shell utilizing native `TranslateTransform` text rendering.
* **Full Multi-CDN Engine:** Out-of-the-box support for all primary Strinova / IDreamSky content delivery nodes. Including 4 CN CDN's and 2 OS CDN's. As well as information on Internal CDN's.
* **Concurrent Async Pipeline:** Orchestrated via `HttpClient` connection pooling maximizing socket reuse with parallel worker configurations.
* **Interactive Terminal Shell:** Terminal items are animated and are able to be clicked and downloaded easily.

## 🛠️ Compilation
Requirements:

  - Windows 10 / 11 (x64)

  - NET 9.0 SDK

  - Visual Studio Preview 2026

## ❤️ Channels
Here are some Channels that can be used in the downloader. They follow Game_<Channel> and Launcher_<Channel>
- **Release**
- **TYF**
- **Preview**
- **PreTest**
- **KOL**

## 📜 License

Distributed under the PolyForm Noncommercial License 1.0.0.  See [LICENSE](LICENSE.txt).
