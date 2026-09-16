# Flexhub Video PD

Flexhub Video PD is a URL service for an existing Angular (.NET) credit-officer app and a Kotlin field-officer app. Those products only open this app in a browser or Android WebView. Scheduling exists to mint links, not as the officers’ daily workspace.

The media layer is **LiveKit open source** (self-hosted SFU). The product API is **ASP.NET Core 8**. The meeting UI is **Next.js**.

## Link contract

Each credit officer gets a **permanent host slug** at user creation (unguessable, never rotated unless you reset it). Angular always opens the same URL. Scheduling does not change it.

| Who | URL |
| --- | --- |
| Credit officer (static) | `https://meet.example.com/host/{hostSlug}` |
| Field officer (open, no login) | `https://meet.example.com/join/{hostSlug}?bank=SBI&groupId=G-22&memberId=M-10482` |

`POST /api/meetings` (credit officer or admin) creates/updates the visit that `{hostSlug}` currently points at and returns `hostUrl` plus `fieldUrl` with `bank`, `groupId`, and `memberId`. Flexhub Video PD **does not validate** those query values against the scheduled visit. They are stored as-is so recordings, duration, chat, and stills can be identified later. Kotlin and Angular are responsible for putting the right values on the link.

**Current visit for a slug:** the officer’s meeting in `WaitingForFieldOfficer` or `InProgress`, else the next `Scheduled` by time. Opening `/host/{slug}` always shows the lobby, even with no field officers and no scheduled visit (a desk visit is created when someone is admitted).

**Field join:** `POST /api/join` puts the field officer in the **waiting queue** (not the LiveKit call). Missing params are stored empty. Wrong IDs are never rejected. The credit officer sees each waiting FO with their bank / group / member, **chats only with waiting officers**, then taps **Admit**. Admitted officers speak on the call; chat is not used in-call.

**Recording segments:** recording runs while at least one **admitted** field officer is in the LiveKit room. Start on 0→1 FO; stop on last FO leaving.

`recordings/{bank}/{groupId}/{memberId}/{meetingId}/seg-{n}.mp4`

## Demo accounts

| Role | Email | Password | Static host URL |
| --- | --- | --- | --- |
| Credit officer (Priya Shah) | `credit@visit.local` | `Credit@123` | `/host/h8k2m9q4w1` |
| Admin | `admin@visit.local` | `Admin@123` | Opens a credit officer’s host URL after login |

Field officers have **no Flexhub Video PD account**. Open the join link, optionally set a display name (default “Field officer”), wait in the lobby, and speak after the credit officer admits you.

A sample visit `VK7M2Q` is seeded for bank `SBI`, group `G-22`, member `M-10482`. Field-officer URL:

`/join/h8k2m9q4w1?bank=SBI&groupId=G-22&memberId=M-10482`

## Auth

- **Field officer:** anonymous `POST /api/join` (rate-limited by IP). Guest JWT is issued so lobby chat works, then a meeting-scoped token after admit.
- **Credit officer:** JWT required to schedule and to open `/host/{slug}`. If there is no session, Flexhub Video PD redirects to login and then back into the room. The logged-in user must be that credit officer (or an admin). Wrong slug → 404.
- **Admin:** can schedule on behalf of a credit officer (`creditOfficerId`) and can open that officer’s `/host/{slug}` after login.

## Run locally

Needs .NET 8, Node 22, and the LiveKit server binary (or Docker).

```bash
# 1. SFU (keys visitmeet / visitmeet_dev_secret_change_me_32b)
livekit-server --config infra/livekit/livekit.local.yaml

# or Docker (Redis + LiveKit; add --profile recording for Egress)
docker compose -f infra/livekit/docker-compose.yml up

# 2. API  http://127.0.0.1:5088
cd api/VisitMeet.Api
ASPNETCORE_URLS=http://127.0.0.1:5088 dotnet run

# 3. Web UI  http://127.0.0.1:43123
cd web
cp .env.example .env.local
npm install
npm run dev
```

Without LiveKit running you can still sign in, schedule visits, and copy join links. Video connects only when the SFU is up. Room-composite MP4s need the Egress worker; if Egress is down the API still stores recording-segment metadata so the visit can complete.

The local SQLite database is recreated on API start so the demo seed (including host slug `h8k2m9q4w1`) is always present.

## What is in the product

- Host lobby open all day: waiting FO list, lobby chat, credit officer Admit
- Open field-officer join with bank / group / member tagging (no ID validation)
- Recording segments start and stop on FO join/leave webhooks
- Capture a frame from live video, crop it, store it against bank/group/member
- Meeting APIs under `/api/meetings`
- LiveKit webhook at `/api/webhooks/livekit`

## AWS EC2

A single **c6i.2xlarge** (or **c7i.2xlarge**) is enough for about **100 concurrent users** in 3-person rooms (1 credit officer + 2 field officers). That is the SFU and API. Room-composite recording is the limiter (~4 vCPU per concurrent Chrome job). Two overlapping recordings can stay on that box; more should use a second Egress instance.

Follow [infra/ec2/README.md](infra/ec2/README.md) for ports, DNS, Caddy, and systemd. This repo does not provision AWS for you.

## Layout

- `api/VisitMeet.Api` — JWT auth, meetings, anonymous join, host tokens, webhooks, Egress, SignalR, SQLite
- `web` — schedule-only dashboard, static host room, open join page, LiveKit call, chat, snapshot crop
- `infra/livekit` — Compose + YAML
- `infra/ec2` — security group, env sample, runbook
