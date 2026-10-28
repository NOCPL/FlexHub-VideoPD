export const SNAPSHOT_CATEGORIES = [
  "House Image 1",
  "House Image 2",
  "House Image 3",
  "KYC 1",
  "KYC 2",
  "KYC 3",
  "Business site",
  "Dairy",
  "Kirana",
  "Tailoring",
  "Barber",
  "Clothing store",
] as const;

export type SnapshotCategory = (typeof SNAPSHOT_CATEGORIES)[number];
