# Deploy LiveKit + VisitMeet on one AWS EC2 node

This is a runbook, not a provisioner. You need an Ubuntu 22.04/24.04 instance, a domain, and DNS.

## Instance

- Type: **c6i.2xlarge** or **c7i.2xlarge** (8 vCPU, 16 GB) for ~100 concurrent users in 3-person rooms (1 credit officer + 2 field officers).
- Network: assign an **Elastic IP**. Open the ports in [security-group.md](./security-group.md).
- Disk: 50+ GB **gp3** for recordings. Do not fill the root volume with MP4s.
- Do **not** use burstable `t3` for production media.

Recording is the limiter: each room-composite job needs about 4 vCPU. Two overlapping recordings can live on this box. Five or more should use a second Egress instance (`c6i.4xlarge`).

## DNS

Point both names at the Elastic IP:

- `livekit.example.com` — SFU + TURN
- `meet.example.com` — the web app + API (Caddy reverse proxy)

## Install LiveKit (official generator)

On your laptop:

```bash
docker pull livekit/generate
docker run --rm -it -v "$PWD:/output" livekit/generate
```

Enable **Egress** when prompted. Paste `cloud-init.*.yaml` into the EC2 **User data** field, or copy `init_script.sh` to the instance and run `sudo ./init_script.sh`.

Config lands in `/opt/livekit`. Start/stop:

```bash
sudo systemctl start livekit-docker
sudo docker compose -f /opt/livekit/docker-compose.yaml logs -f
```

If cloud-init ran before networking (common on EC2):

```bash
sudo cloud-init clean --logs
sudo reboot now
```

## Host networking

LiveKit must bind the UDP media range on the instance’s public IP. Production compose uses `network_mode: host`. Do not put RTP behind a classic load balancer.

## App + API on the same node

1. Install .NET 8 runtime and Node 22.
2. Publish the API to `/opt/visitmeet/api` and the Next.js build to `/opt/visitmeet/web`.
3. Copy [env.sample](./env.sample) to `/opt/visitmeet/.env`.
4. systemd unit for the API:

```ini
[Unit]
Description=VisitMeet API
After=network.target livekit-docker.service

[Service]
WorkingDirectory=/opt/visitmeet/api
EnvironmentFile=/opt/visitmeet/.env
ExecStart=/usr/bin/dotnet /opt/visitmeet/api/VisitMeet.Api.dll
Restart=always

[Install]
WantedBy=multi-user.target
```

5. Reverse-proxy `meet.example.com` → Next.js (or static export + API) and `/api` + `/hubs` → `127.0.0.1:5088`.
6. Point `LIVEKIT_URL` in the web build at `wss://livekit.example.com`.

## S3 recordings (optional)

When AWS keys exist, set `RECORDINGS_S3_BUCKET` and the API will ask Egress to upload MP4s with path:

`recordings/{bank}/{groupId}/{memberId}/{meetingId}/seg-{n}.mp4`

Until then, files land on `RECORDINGS_PATH`.

## Load test before go-live

```bash
lk load-test --url wss://livekit.example.com --api-key visitmeet --api-secret '...' \
  --room-count 33 --publishers 3 --subscribers 0 --video-publishers 3 --duration 60s
```

Simulate 33 rooms of 3 (your 100-user peak). Watch CPU, UDP errors, and NIC throughput.
