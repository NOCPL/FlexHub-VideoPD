export type FieldGeotag = {
  latitude: number | null;
  longitude: number | null;
  accuracyMeters: number | null;
  geoCapturedAt: string | null;
  geoError: string | null;
};

export function hasCoordinates(
  geotag: FieldGeotag | null | undefined,
): geotag is FieldGeotag & { latitude: number; longitude: number } {
  return geotag?.latitude != null && geotag?.longitude != null;
}

export function gpsRequiredMessage(code?: string | null) {
  switch (code) {
    case "PERMISSION_DENIED":
      return "Location is required. Tap Allow when this page asks. If you already blocked it, open this site’s settings on your phone, set Location to Allow, then tap Try again. The call cannot start without GPS.";
    case "POSITION_UNAVAILABLE":
      return "Turn on GPS on this phone, then tap Try again. The call cannot start without GPS.";
    case "TIMEOUT":
      return "Could not get a GPS fix. Keep location on, then tap Try again. The call cannot start without GPS.";
    case "UNSUPPORTED":
      return "This browser cannot share location. Open the join link in Chrome or Safari.";
    case "INSECURE":
      return "Open this join link over HTTPS so the phone can share GPS.";
    default:
      return "Allow location on this phone to continue. The call cannot start without GPS.";
  }
}

export function geoErrorLabel(code: string | null | undefined) {
  switch (code) {
    case "PERMISSION_DENIED":
      return "Location permission denied on the field officer’s phone";
    case "POSITION_UNAVAILABLE":
      return "GPS unavailable on the field officer’s phone";
    case "TIMEOUT":
      return "GPS timed out on the field officer’s phone";
    case "UNSUPPORTED":
      return "This phone browser cannot share location";
    case "INSECURE":
      return "Open the join link over HTTPS to share GPS";
    default:
      return code ? code.replace(/_/g, " ").toLowerCase() : "GPS not reported by field officer";
  }
}

export function formatKolkata(value: string | Date | null | undefined) {
  const date = value ? new Date(value) : new Date();
  if (Number.isNaN(date.getTime())) return "time unknown";
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: "Asia/Kolkata",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  }).formatToParts(date);
  const get = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value ?? "";
  return `${get("year")}-${get("month")}-${get("day")} ${get("hour")}:${get("minute")}:${get("second")} IST`;
}

export function geotagOverlayLines(geotag: FieldGeotag | null | undefined) {
  if (hasCoordinates(geotag)) {
    const accuracy =
      geotag.accuracyMeters != null ? `±${Math.round(geotag.accuracyMeters)} m` : "accuracy unknown";
    return [
      `${geotag.latitude!.toFixed(6)}, ${geotag.longitude!.toFixed(6)}`,
      `${accuracy} · ${formatKolkata(geotag.geoCapturedAt)}`,
    ];
  }
  return [geoErrorLabel(geotag?.geoError), formatKolkata(geotag?.geoCapturedAt ?? new Date().toISOString())];
}

export function geotagSummary(geotag: FieldGeotag | null | undefined) {
  if (hasCoordinates(geotag)) {
    const accuracy =
      geotag.accuracyMeters != null ? ` ±${Math.round(geotag.accuracyMeters)} m` : "";
    return `${geotag.latitude!.toFixed(5)}, ${geotag.longitude!.toFixed(5)}${accuracy} · ${formatKolkata(geotag.geoCapturedAt)}`;
  }
  return geoErrorLabel(geotag?.geoError);
}

export function mapsUrl(geotag: FieldGeotag | null | undefined) {
  if (!hasCoordinates(geotag)) return null;
  return `https://www.google.com/maps?q=${geotag.latitude},${geotag.longitude}`;
}
