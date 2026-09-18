import type { NextConfig } from "next";

const api = process.env.VISITMEET_API_ORIGIN ?? "http://127.0.0.1:5088";

const nextConfig: NextConfig = {
  allowedDevOrigins: ["127.0.0.1", "localhost", "*.cursor.sh", "*.cursor.com"],
  async redirects() {
    return [
      { source: "/login", destination: "/", permanent: false },
      { source: "/schedule", destination: "/dashboard", permanent: false },
      { source: "/visits", destination: "/video-pd", permanent: false },
      { source: "/visits/:id", destination: "/video-pd/:id", permanent: false },
    ];
  },
  async rewrites() {
    return [
      { source: "/api/:path*", destination: `${api}/api/:path*` },
      { source: "/hubs/:path*", destination: `${api}/hubs/:path*` },
    ];
  },
};

export default nextConfig;
