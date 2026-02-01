# ScaleRecordApp

ScaleRecordApp is a cross-platform application built with .NET MAUI for managing agricultural weighing records.
It is designed for weighbridge operators and farm staff to record, track, and analyze crop transportation and storage operations.

The application focuses on simplicity, offline-first usage, and fast data entry in real-world agricultural environments.

---

## Features

- Recording vehicle weighings (gross, tare, net weight)
- Vehicle and tare weight management
- Crop and cargo type tracking (type, variety, season)
- Source and destination tracking (fields, storage, cleaning, export, etc.)
- Seasonal data separation (e.g. Harvest 2024)
- Fast, operator-friendly data entry
- Cross-platform support (Windows, Android, Linux-ready backend)

---

## Tech Stack

- .NET 8
- .NET MAUI
- MVVM architecture
- SQLite (local storage)
- Cross-platform UI

---

## Project Status

This project is under active development.
The data model and core workflows are stable, while UI and reporting features are still evolving.

---

## Getting Started

### Prerequisites

- .NET SDK 8+
- Visual Studio 2022  Rider with MAUI workload
- Android SDK (for Android builds)

### Run locally

```bash
git clone httpsgithub.comyour-usernameScaleRecordApp.git
cd ScaleRecordApp
