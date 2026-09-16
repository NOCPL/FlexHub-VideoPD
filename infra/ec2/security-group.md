# EC2 security group (single-node LiveKit)

Open these on the instance that runs `livekit-server` (and Caddy):

| Port | Protocol | Why |
| --- | --- | --- |
| 80 | TCP | Let's Encrypt / HTTP redirect |
| 443 | TCP | HTTPS signaling + TURN/TLS |
| 7881 | TCP | WebRTC over TCP fallback |
| 3478 | UDP | TURN/UDP |
| 50000–60000 | UDP | WebRTC media |

SSH (22) only from your IP. Do not expose Redis (6379) or the LiveKit HTTP port (7880) to the public internet — terminate TLS on Caddy and proxy `wss://livekit.example.com` to `127.0.0.1:7880`.

Instance type: `c6i.2xlarge` or `c7i.2xlarge` for ~100 concurrent users in 3-person rooms. Add a second `c6i.4xlarge` for Egress if more than ~2 room-composite recordings overlap.
