/** Average minutes each waiting token spends ahead of you. */
export const MINUTES_PER_TOKEN = 5;

export function waitMinutesForToken(tokenNumber: number | null | undefined) {
  if (tokenNumber == null || tokenNumber < 1) return null;
  return Math.max(0, (tokenNumber - 1) * MINUTES_PER_TOKEN);
}

export function tokenLabel(tokenNumber: number | null | undefined) {
  return tokenNumber == null ? "—" : String(tokenNumber);
}

export function waitEstimateLabel(minutes: number | null | undefined) {
  if (minutes == null) return "—";
  if (minutes <= 0) return "You’re next";
  return `About ${minutes} min`;
}
