"use client";

import { useEffect, useRef, useState } from "react";
import { api } from "@/lib/api";
import { geoErrorLabel, geotagSummary, type FieldGeotag } from "@/lib/geotag";

type Status = "idle" | "locating" | "ready" | "error";

function codeFromPositionError(err: GeolocationPositionError | null) {
  if (!err) return "POSITION_UNAVAILABLE";
  if (err.code === err.PERMISSION_DENIED) return "PERMISSION_DENIED";
  if (err.code === err.TIMEOUT) return "TIMEOUT";
  return "POSITION_UNAVAILABLE";
}

function movedMeters(from: FieldGeotag | null, lat: number, lng: number) {
  if (from?.latitude == null || from?.longitude == null) return Number.POSITIVE_INFINITY;
  const toRad = (deg: number) => (deg * Math.PI) / 180;
  const dLat = toRad(lat - from.latitude);
  const dLng = toRad(lng - from.longitude);
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRad(from.latitude)) * Math.cos(toRad(lat)) * Math.sin(dLng / 2) ** 2;
  return 6371000 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

export function useFieldOfficerGeotag(waitingId: string | null) {
  const [status, setStatus] = useState<Status>("idle");
  const [geotag, setGeotag] = useState<FieldGeotag | null>(null);
  const lastSent = useRef<{ at: number; geotag: FieldGeotag | null }>({ at: 0, geotag: null });

  useEffect(() => {
    if (!waitingId) return;

    let cancelled = false;
    let watchId: number | null = null;

    async function postFix(next: FieldGeotag, force = false) {
      const now = Date.now();
      const elapsed = now - lastSent.current.at;
      const moved = movedMeters(lastSent.current.geotag, next.latitude ?? 0, next.longitude ?? 0);
      if (!force && elapsed < 15_000 && moved < 20) return;
      lastSent.current = { at: now, geotag: next };
      try {
        const saved = await api.reportGeotag(waitingId!, {
          latitude: next.latitude,
          longitude: next.longitude,
          accuracyMeters: next.accuracyMeters,
          capturedAt: next.geoCapturedAt,
          error: next.geoError,
        });
        if (!cancelled) {
          setGeotag(saved);
          setStatus(saved.latitude != null && saved.longitude != null ? "ready" : "error");
        }
      } catch {
        /* keep trying on the next watch tick */
      }
    }

    async function postError(code: string) {
      setStatus("error");
      const next: FieldGeotag = {
        latitude: null,
        longitude: null,
        accuracyMeters: null,
        geoCapturedAt: new Date().toISOString(),
        geoError: code,
      };
      setGeotag((current) => (current?.latitude != null && current?.longitude != null ? current : next));
      try {
        await api.reportGeotag(waitingId!, {
          latitude: null,
          longitude: null,
          accuracyMeters: null,
          capturedAt: next.geoCapturedAt,
          error: code,
        });
      } catch {
        /* ignore */
      }
    }

    if (typeof window !== "undefined" && !window.isSecureContext) {
      void postError("INSECURE");
      return;
    }
    if (!navigator.geolocation) {
      void postError("UNSUPPORTED");
      return;
    }

    setStatus("locating");
    const options: PositionOptions = {
      enableHighAccuracy: true,
      timeout: 20_000,
      maximumAge: 10_000,
    };

    navigator.geolocation.getCurrentPosition(
      (position) => {
        if (cancelled) return;
        const next: FieldGeotag = {
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracyMeters: position.coords.accuracy,
          geoCapturedAt: new Date(position.timestamp).toISOString(),
          geoError: null,
        };
        setGeotag(next);
        setStatus("ready");
        void postFix(next, true);
      },
      (err) => {
        if (!cancelled) void postError(codeFromPositionError(err));
      },
      options,
    );

    watchId = navigator.geolocation.watchPosition(
      (position) => {
        if (cancelled) return;
        const next: FieldGeotag = {
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracyMeters: position.coords.accuracy,
          geoCapturedAt: new Date(position.timestamp).toISOString(),
          geoError: null,
        };
        setGeotag(next);
        setStatus("ready");
        void postFix(next);
      },
      (err) => {
        const hasFix =
          lastSent.current.geotag?.latitude != null && lastSent.current.geotag?.longitude != null;
        if (!cancelled && !hasFix) {
          void postError(codeFromPositionError(err));
        }
      },
      options,
    );

    return () => {
      cancelled = true;
      if (watchId != null) navigator.geolocation.clearWatch(watchId);
    };
  }, [waitingId]);

  const label =
    status === "locating"
      ? "Locating this phone…"
      : status === "ready"
        ? geotagSummary(geotag)
        : status === "error"
          ? geoErrorLabel(geotag?.geoError)
          : "GPS starts after you join the queue";

  return { status, geotag, label };
}
