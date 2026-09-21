# Flexhub Video PD

Flexhub Video PD is a URL service for an existing Angular (.NET) credit-officer app and a Kotlin field-officer app. Those products only open this app in a browser or Android WebView. Scheduling exists to mint links, not as the officers’ daily workspace.

The media layer is **LiveKit open source** (self-hosted SFU). The product API is **ASP.NET Core 8**. The meeting UI is **Next.js**.

## Link contract

Each credit officer gets a **permanent host slug** at user creation (unguessable, never rotated unless you reset it). Angular always opens the same URL. Scheduling does not change it.

| Who | URL |
| --- | --- |
| Credit officer (static) | `https://meet.example.com/host/{hostSlug}` |
| Field officer (open, no login) | `https://meet.example.com/join/{hostSlug}?bank=SBI&branch=Mumbai-Central&groupId=G-22&memberId=M-10482` |

`POST /api/meetings` (credit officer or admin) creates/updates the Video PD that `{hostSlug}` currently points at and returns `hostUrl` plus `fieldUrl` with `bank`, `branch`, `groupId`, and `memberId`. Flexhub Video PD **does not validate** those query values. They are stored as-is so recordings, duration, chat, and stills can be identified later.

**Current visit for a slug:** the officer’s meeting in `WaitingForFieldOfficer` or `InProgress`, else the next `Scheduled` by time. Opening `/host/{slug}` always shows the lobby, even with no field officers and no scheduled visit (a desk visit is created when someone is admitted).

**Field join:** `POST /api/join` puts the field officer in the **waiting queue** (not the LiveKit call). Missing params are stored empty. Wrong IDs are never rejected. The credit officer sees Bank → Branch → Group → Member, can chat with Everyone or privately, and then taps **Admit**. Only one FO is admitted at a time; admitted officers speak on the call and leave lobby chat.

**Recording segments:** recording starts when the admitted field officer connects and stops when they leave. Rejoining creates a new segment.

`recordings/{bank}/{branch}/{groupId}/{memberId}/{meetingId}/seg-{n}.mp4`

## Demo accounts

| Role | Email | Password | Static host URL |
| --- | --- | --- | --- |
| Credit officer (Priya Shah) | `credit@visit.local` | `Credit@123` | `/host/h8k2m9q4w1` |
| Admin | `admin@visit.local` | `Admin@123` | Opens a credit officer’s host URL after login |

Field officers have **no Flexhub Video PD account**. Open the join link, optionally set a display name (default “Field officer”), wait in the lobby, and speak after the credit officer admits you.

A sample Video PD `VK7M2Q` is seeded for bank `SBI`, branch `Mumbai Central`, group `G-22`, member `M-10482`. Field-officer URL:

`/join/h8k2m9q4w1?bank=SBI&branch=Mumbai-Central&groupId=G-22&memberId=M-10482`

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

The local SQLite database is recreated on API start (`EnsureDeletedAsync`) so the demo seed (including host slug `h8k2m9q4w1`) is always present. That wipe is **OK for local validation only**. Production must not call `EnsureDeleted`. Postgres/RDS is preferred later.

## Put this code on GitHub, then pull it onto EC2

Use this sequence: download the project → create a **private GitHub** repo → clone on the server → `git pull` for later updates. Server install, env, systemd, and ALB notes live in [infra/ec2/README.md](infra/ec2/README.md) — do not paste a temporary Cloud Agent remote.

### 1. Get the code

From this Cloud Agent workspace, download a zip of the project (or copy the files). You can also create a GitHub repository and push this project there, then clone that repo everywhere else.

Do **not** treat a Cloud Agent remote as production. Production clones come from **your GitHub repo**.

### 2. Create a private GitHub repo and push `main`

On GitHub: **New repository** → private → name it something like `Flexhub-VideoPD` (or `FlexHub-VideoPD` if you already use that). Do not initialize with a README if you are pushing an existing tree.

On your laptop, in the project folder:

```bash
git init
git add .
git commit -m "Flexhub Video PD"
git branch -M main
git remote add origin git@github.com:YOUR_ORG/Flexhub-VideoPD.git
git push -u origin main
```

If this folder is already a git repo, skip `git init` and only add the GitHub remote (use another remote name if `origin` is taken):

```bash
git remote add github git@github.com:YOUR_ORG/Flexhub-VideoPD.git
git push -u github main
```

### 3. On the EC2 server: install git, clone, pull later

```bash
sudo apt-get update
sudo apt-get install -y git
sudo mkdir -p /opt/videopd
sudo chown "$USER":"$USER" /opt/videopd
cd /opt/videopd
git clone git@github.com:YOUR_ORG/Flexhub-VideoPD.git .
```

Later updates:

```bash
cd /opt/videopd
git pull origin main
```

Then follow [infra/ec2/README.md](infra/ec2/README.md) to run LiveKit, the ASP.NET API (5088), and Next.js (43123 locally; `videopd.nocpl.in` sits behind the ALB).

## What is in the product

- Admin officer creation with permanent host URLs
- Host lobby: waiting durations, Everyone/private chat, Admit and Remove
- One active FO at a time with focused video and field-officer timer
- Open field-officer join with bank / branch / group / member tagging (no ID validation)
- Recording segments start and stop on FO join/leave webhooks
- Capture a frame from live video, crop it, stamp the field officer’s phone GPS and time, and store it against bank/group/member. Field officers must allow GPS; the call will not start if they block it.
- Meeting APIs under `/api/meetings`
- LiveKit webhook at `/api/webhooks/livekit`

## AWS EC2

A single **c6i.2xlarge** (or **c7i.2xlarge**) is enough for about **100 concurrent users** in 3-person rooms (1 credit officer + 2 field officers). That is the SFU and API. Room-composite recording is the limiter (~4 vCPU per concurrent Chrome job). Two overlapping recordings can stay on that box; more should use a second Egress instance.

Production: `videopd.nocpl.in` on **ALB + ACM**; `livekit.nocpl.in` and `turn.nocpl.in` stay on the instance.

Follow [infra/ec2/README.md](infra/ec2/README.md) for clone/pull, quoted SQLite env, systemd unit files, ALB/Caddy, and the three processes. This repo does not provision AWS for you.

## Layout

- `api/VisitMeet.Api` — JWT auth, meetings, anonymous join, host tokens, webhooks, Egress, SignalR, SQLite
- `web` — schedule-only dashboard, static host room, open join page, LiveKit call, chat, snapshot crop
- `infra/livekit` — Compose + YAML
- `infra/ec2` — security group, env sample, systemd units, runbook
