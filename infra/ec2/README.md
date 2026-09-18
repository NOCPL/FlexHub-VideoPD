# Deploy LiveKit + Flexhub Video PD on one AWS EC2 node

This is a runbook, not a provisioner. You need an Ubuntu 22.04/24.04 instance, a domain, and DNS.

Get the code onto a **private GitHub repo** first (see the root [README.md](../../README.md) — zip/download or `git clone` of **your** GitHub remote, then `git push origin main`). This file is the **server** half: clone, env, three processes, systemd, ALB.

Production names in use:

- `videopd.nocpl.in` — Next.js + API, **ALB + ACM**
- `livekit.nocpl.in` — SFU signaling, **on the instance**
- `turn.nocpl.in` — TURN, **on the instance** (UDP/TURN must not sit behind a classic ALB)

## Instance

- Type: **c6i.2xlarge** or **c7i.2xlarge** (8 vCPU, 16 GB) for ~100 concurrent users in 3-person rooms (1 credit officer + 2 field officers).
- Network: assign an **Elastic IP**. Open the ports in [security-group.md](./security-group.md).
- Disk: 50+ GB **gp3** for recordings. Do not fill the root volume with MP4s.
- Do **not** use burstable `t3` for production media.

Recording is the limiter: each room-composite job needs about 4 vCPU. Two overlapping recordings can live on this box. Five or more should use a second Egress instance (`c6i.4xlarge`).

## DNS

- `videopd.nocpl.in` — ALB (HTTPS via ACM) → instance HTTP (Next on `127.0.0.1:43123`; `/api` and `/hubs` to `127.0.0.1:5088`).
- `livekit.nocpl.in` and `turn.nocpl.in` — A records on the instance Elastic IP. Terminate TLS on Caddy (or LiveKit’s generator) on the box. Do **not** put RTP/TURN behind the ALB.

Point older `meet.example.com` / `livekit.example.com` names the same way if you still use them.

## Clone and pull (on the instance)

```bash
sudo apt-get update
sudo apt-get install -y git
sudo mkdir -p /opt/videopd
sudo chown "$USER":"$USER" /opt/videopd
cd /opt/videopd
git clone git@github.com:YOUR_ORG/Flexhub-VideoPD.git .
```

Later:

```bash
cd /opt/videopd
git pull origin main
```

SSH keys or a deploy token must be able to read that **private** GitHub repo.

## Install runtimes

```bash
# .NET 8
wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.bashrc
export PATH="$HOME/.dotnet:$PATH"

# Node 22
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash -
sudo apt-get install -y nodejs

# LiveKit server binary (local-style) — or use the official generator below
# https://docs.livekit.io/home/self-hosting/deployment/
```

## Install LiveKit (official generator)

On your laptop:

```bash
docker pull livekit/generate
docker run --rm -it -v "$PWD:/output" livekit/generate
```

Enable **Egress** when prompted. Use domain `livekit.nocpl.in` (and TURN `turn.nocpl.in`). Paste `cloud-init.*.yaml` into the EC2 **User data** field, or copy `init_script.sh` to the instance and run `sudo ./init_script.sh`.

Config lands in `/opt/livekit`. Keys in that generated YAML (and in `infra/livekit/livekit.local.yaml` for local: `visitmeet` / `visitmeet_dev_secret_change_me_32b`) must match `LiveKit__ApiKey` and `LiveKit__ApiSecret` in `/opt/videopd/.env`.

Start/stop:

```bash
sudo systemctl start livekit-docker
sudo docker compose -f /opt/livekit/docker-compose.yaml logs -f
```

If cloud-init ran before networking (common on EC2):

```bash
sudo cloud-init clean --logs
sudo reboot now
```

### Local-style SFU on the box (validation)

From the cloned repo, keys come from `infra/livekit/livekit.local.yaml`:

```bash
# 1. SFU — http://127.0.0.1:7880
livekit-server --config /opt/videopd/infra/livekit/livekit.local.yaml
```

Set `rtc.use_external_ip: true` in production config so WebRTC uses the public IP.

## Host networking

LiveKit must bind the UDP media range on the instance’s public IP. Production compose uses `network_mode: host`. Do not put RTP behind a classic load balancer.

## Environment (quote SQLite; use `__`)

```bash
sudo mkdir -p /opt/videopd/data /opt/videopd/recordings /opt/videopd/uploads
cp /opt/videopd/infra/ec2/env.sample /opt/videopd/.env
# edit secrets, LiveKit keys, PublicWebUrl=https://videopd.nocpl.in
```

ASP.NET Core nested keys use a **double underscore**. Quote the SQLite connection string — the space in `Data Source=` breaks bash if unquoted:

```bash
export ConnectionStrings__Default="Data Source=/opt/videopd/data/visitmeet.db"
export Jwt__SigningKey='replace_with_a_long_random_signing_key'
export LiveKit__Url=wss://livekit.nocpl.in
export LiveKit__HttpUrl=http://127.0.0.1:7880
export LiveKit__ApiKey=visitmeet
export LiveKit__ApiSecret='your_livekit_secret'
export PublicWebUrl=https://videopd.nocpl.in
export Cors__Origins=https://videopd.nocpl.in
export ASPNETCORE_URLS=http://127.0.0.1:5088
```

`NEXT_PUBLIC_API_URL` stays **empty** when Next.js rewrites `/api` and `/hubs` to the API on the same host (`VISITMEET_API_ORIGIN=http://127.0.0.1:5088`). Set `NEXT_PUBLIC_API_URL` only if the API is on another host (then rebuild the web app).

**SQLite wipe:** `Program.cs` calls `EnsureDeletedAsync()` then recreates and seeds on every API start. That is OK for **validation** (demo logins always exist). **Production must not use EnsureDeleted** — remove that call before you keep real meetings. Postgres/RDS is preferred later; this runbook does not switch the database.

## Run the three processes

Validation (foreground). Production: use systemd units in this folder.

```bash
# 1. LiveKit SFU (port 7880) — already running if you used livekit-docker
#    or: livekit-server --config /opt/videopd/infra/livekit/livekit.local.yaml

# 2. ASP.NET API  http://127.0.0.1:5088
cd /opt/videopd/api/VisitMeet.Api
ASPNETCORE_URLS=http://127.0.0.1:5088 \
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__Default="Data Source=/opt/videopd/data/visitmeet.db" \
dotnet run

# health
curl http://127.0.0.1:5088/health

# 3. Next.js web  (local port 43123; ALB targets this for videopd.nocpl.in)
cd /opt/videopd/web
cp .env.example .env.local   # NEXT_PUBLIC_API_URL empty
npm install
npm run build
npx next start --hostname 127.0.0.1 --port 43123
```

For a published API instead of `dotnet run`:

```bash
cd /opt/videopd/api/VisitMeet.Api
dotnet publish -c Release -o /opt/videopd/api
```

## systemd

Units: [visitmeet-api.service](./visitmeet-api.service) and [visitmeet-web.service](./visitmeet-web.service). Copy them, then:

```bash
sudo cp /opt/videopd/infra/ec2/visitmeet-api.service /etc/systemd/system/
sudo cp /opt/videopd/infra/ec2/visitmeet-web.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now visitmeet-api visitmeet-web
sudo systemctl status visitmeet-api visitmeet-web
```

LiveKit’s generator already installs `livekit-docker.service`. Point ALB health checks at `/` on 43123 and/or `/health` on 5088 (only if the ALB target is the API; usually the ALB hits Next, which rewrites `/api`).

## ALB / Caddy

- **videopd.nocpl.in**: ACM certificate on the **ALB**. Forward HTTPS → instance `43123` (Next). Next rewrites `/api/*` and `/hubs/*` to `127.0.0.1:5088`. Do not put the API on a public ALB listener unless you also set CORS and `NEXT_PUBLIC_API_URL`.
- **livekit.nocpl.in** / **turn.nocpl.in**: stay on the instance. Caddy (or the LiveKit generator) terminates TLS and proxies `wss://livekit.nocpl.in` → `127.0.0.1:7880`. Open UDP 50000–60000, 3478, TCP 7881 on the instance security group — not on the ALB.

If you still terminate the web app with Caddy on the instance (no ALB):

```
videopd.nocpl.in {
  reverse_proxy 127.0.0.1:43123
}
```

Caddy would then need a listener the ALB does **not** also claim on 443.

## Demo logins (seeded SQLite)

| Role | Email | Password | Notes |
| --- | --- | --- | --- |
| Credit officer | `credit@visit.local` | `Credit@123` | Host slug `h8k2m9q4w1` |
| Admin | `admin@visit.local` | `Admin@123` | Schedule on behalf of a CO |

Field officers have no account: `/join/h8k2m9q4w1?bank=SBI&branch=Mumbai-Central&groupId=G-22&memberId=M-10482`

## S3 recordings (optional)

When AWS keys exist, set `RECORDINGS_S3_BUCKET` and the API will ask Egress to upload MP4s with path:

`recordings/{bank}/{branch}/{groupId}/{memberId}/{meetingId}/seg-{n}.mp4`

Until then, files land on `Storage__RecordingsPath`.

## Load test before go-live

```bash
lk load-test --url wss://livekit.nocpl.in --api-key visitmeet --api-secret '...' \
  --room-count 33 --publishers 3 --subscribers 0 --video-publishers 3 --duration 60s
```

Simulate 33 rooms of 3 (your 100-user peak). Watch CPU, UDP errors, and NIC throughput.
