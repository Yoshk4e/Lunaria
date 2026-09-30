# Lunaria

<strong>Lun</strong> is a work-in-progress server emulator for a certain anime game.
The `SdkServer`, `GameServer`, and `QueryGateway` run as three .NET 10 processes.

[Discord](<invite link>)

## Warning

Lunaria is completely free and open source.
If anyone sold you this server or charged money to provide it, that was a scam.
Request a refund immediately and report the seller with any relevant proof.

## Overview

- `SdkServer`
  - HTTP APIs for signup, login, queue status, and hot-update manifests
  - Argon2id password hashing, per-account rate limiting, hour-long session tokens
  - Postgres with automatic SQLite fallback, so a single machine works out of the box
- `GameServer`
  - TCP + UDP game connections over the SilverNet protocol (DH handshake, AES-128-ECB framing, UDP establish channel)
  - The full gameplay loop and SQLite (WAL) save persistence via EF Core migrations
- `QueryGateway`
  - Instance registry: game servers register, heartbeat, and get allocated round-robin
- `assets/`
  - 152 gameplay table dumps plus banner and gameplay-policy configs, included, so the server runs without external data

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.400 or newer)

## Running

1. Restore dependencies and build.
```powershell
dotnet build -c Release
```
2. Start the three processes, in this order.
```powershell
dotnet run -c Release --project Source/Lunaria.QueryGateway
dotnet run -c Release --project Source/Lunaria.SdkServer
dotnet run -c Release --project Source/Lunaria.GameServer
```
3. Point the client at the server (see below).
4. Create the account through the sdk.
5. Enjoy.

### Connecting with the client

(placeholder)

```bash
LUNARIA_GameServer__Port=30000
LUNARIA_GameServer__AssetsDir=/srv/lun/assets
LUNARIA_SdkServer__Database__PostgresUrl=postgres://user:pass@db:5432/lun
```

All options are validated at startup.

## Feature List

* [x] Signup, login, session tokens, login queue
* [x] Role select and character roster with starter grant
* [x] Characters, level/exp growth, vitals, skills, talent nodes
* [x] Deployment teams and temporary teams (liquid pool)
* [x] Item bag and multi-currency wallet (change-tracked, item cooldowns)
* [x] Equipment (motives), growth, break, refine, equip
* [x] Gacha, banners, pity, rebate mask, newcomer grants
* [x] Shop with per-good quotas, charge rebate milestones, month card
* [x] Mail, delivery, claiming, expiry sweep, overflow sink
* [x] Quests, daily missions, achievements, battle pass, sign-in attendance
* [x] World, maps, sub-regions, savepoints, teleport unlocks, NPC groups
* [x] Gathering collections (distance and quota gates)
* [x] Wanted bounties, bless / relic / bond / bionic awards
* [x] Dungeons
* [x] House purchase and upgrade, silver creatures
* [x] Stamina and satiety meters, team level and world level progression
* [x] Guide/handbook unlocks and red-point notifications
* [x] Save persistence
* [ ] Friends, chat, and multiplayer systems (future if they do)
* [x] Full command coverage

## Legal Disclaimer

- Lun was developed for educational and research purposes.
- All trademarks, copyrights, and other intellectual property related to the original game
  belong to their respective owners.
- Use this software at your own risk. The authors assume no responsibility for any damages
  or legal consequences resulting from its use.

## License

not added yet.
