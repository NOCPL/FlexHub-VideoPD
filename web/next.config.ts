import type { NextConfig } from "next";

const api = process.env.VISITMEET_API_ORIGIN ?? "http://127.0.0.1:5088";

const nextConfig: NextConfig = {
  allowedDevOrigins: ["127.0.0.1", "localhost"],
  async rewrites() {
    return [
      { source: "/api/:path*", destination: `${api}/api/:path*` },
      { source: "/hubs/:path*", destination: `${api}/hubs/:path*` },
    ];
  },
};

export default nextConfig;
