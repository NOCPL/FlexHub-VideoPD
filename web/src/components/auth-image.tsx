"use client";

import { useEffect, useState } from "react";
import { API_URL, getToken } from "@/lib/api";

type Props = {
  src: string;
  alt: string;
  className?: string;
};

export function AuthImage({ src, alt, className }: Props) {
  const [blobUrl, setBlobUrl] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    let created: string | null = null;
    const token = getToken();
    fetch(`${API_URL}${src}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    })
      .then((res) => {
        if (!res.ok) throw new Error("Image could not be loaded.");
        return res.blob();
      })
      .then((blob) => {
        if (cancelled) return;
        created = URL.createObjectURL(blob);
        setBlobUrl(created);
      })
      .catch(() => {
        if (!cancelled) setFailed(true);
      });
    return () => {
      cancelled = true;
      if (created) URL.revokeObjectURL(created);
    };
  }, [src]);

  if (failed) {
    return <div className="flex h-full items-center justify-center text-sm text-[#5a6a84]">Still could not be loaded.</div>;
  }
  if (!blobUrl) {
    return <div className="flex h-full items-center justify-center text-sm text-[#5a6a84]">Loading still…</div>;
  }
  return <img src={blobUrl} alt={alt} className={className} />;
}
