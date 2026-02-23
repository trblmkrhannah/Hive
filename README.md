Hive
====

Hive is a hexagonal puzzle game played on a  grid. The goal is to score points by matching tiles of the same color.

Game Play
---------

### Basic Controls

- **Tap** between 3 hexagons to rotate them
- **Toggle** the rotation direction using the button in the top-right corner (clockwise/counter-clockwise)

Building and Running
--------------------

### Prerequisites

- .NET 10 SDK

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run --project Hive.Desktop
```

Docker
------

### Building

```bash
docker build -t hive-web:latest .
```

### Running

1. Create `compose.yml`.

```yml
services:
  hive-browser:
    image: hive-web
    container_name: hive-web
    ports:
      - "5000:5000"
    restart: unless-stopped
```

2. Compose up.

```bash
docker compose up -d --force-recreate
```

Flatpak
-------

### Building

```bash
flatpak-builder --user --install build-dir party.trblmkr.hive.yml --force-clean
```

### Running

```bash
flatpak run party.trblmkr.hive
```