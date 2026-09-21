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

export function formatUtc(value: string | Date | null | undefined) {
  const date = value ? new Date(value) : new Date();
  if (Number.isNaN(date.getTime())) return "time unknown";
  return date.toISOString().replace("T", " ").replace(/\.\d{3}Z$/, " UTC");
}

export function geotagOverlayLines(geotag: FieldGeotag | null | undefined) {
  if (hasCoordinates(geotag)) {
    const accuracy =
      geotag.accuracyMeters != null ? `±${Math.round(geotag.accuracyMeters)} m` : "accuracy unknown";
    return [
      `${geotag.latitude!.toFixed(6)}, ${geotag.longitude!.toFixed(6)}`,
      `${accuracy} · ${formatUtc(geotag.geoCapturedAt)} · Field officer GPS`,
    ];
  }
  return [geoErrorLabel(geotag?.geoError), formatUtc(geotag?.geoCapturedAt ?? new Date().toISOString())];
}

export function geotagSummary(geotag: FieldGeotag | null | undefined) {
  if (hasCoordinates(geotag)) {
    const accuracy =
      geotag.accuracyMeters != null ? ` ±${Math.round(geotag.accuracyMeters)} m` : "";
    return `${geotag.latitude!.toFixed(5)}, ${geotag.longitude!.toFixed(5)}${accuracy} · ${formatUtc(geotag.geoCapturedAt)}`;
  }
  return geoErrorLabel(geotag?.geoError);
}

export function mapsUrl(geotag: FieldGeotag | null | undefined) {
  if (!hasCoordinates(geotag)) return null;
  return `https://www.google.com/maps?q=${geotag.latitude},${geotag.longitude}`;
}
