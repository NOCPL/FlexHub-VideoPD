export type CallSignal =
  | { type: "flipCamera" }
  | { type: "meetingEnded" };

const encoder = new TextEncoder();
const decoder = new TextDecoder();

export function encodeCallSignal(signal: CallSignal) {
  return encoder.encode(JSON.stringify(signal));
}

export function decodeCallSignal(payload: Uint8Array): CallSignal | null {
  try {
    const parsed = JSON.parse(decoder.decode(payload)) as CallSignal;
    if (parsed?.type === "flipCamera" || parsed?.type === "meetingEnded") return parsed;
    return null;
  } catch {
    return null;
  }
}
