import type {
  AdmitResponse,
  AuthResponse,
  ChatMessage,
  HostLobby,
  JoinTokenResponse,
  LobbyMessage,
  MeetingDetail,
  MeetingListItem,
  Snapshot,
  User,
  WaitResponse,
  WaitingOfficer,
  WaitingRoom,
} from "@/lib/types";

export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

export function hubUrl() {
  if (API_URL) return `${API_URL}/hubs/notifications`;
  if (typeof window !== "undefined") {
    const host = window.location.hostname;
    if (host === "127.0.0.1" || host === "localhost") {
      return "http://127.0.0.1:5088/hubs/notifications";
    }
  }
  return "/hubs/notifications";
}

const TOKEN_KEY = "visitmeet.token";
const GUEST_KEY = "visitmeet.guest";

export function getAuthToken() {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TOKEN_KEY);
}

export function getToken() {
  if (typeof window === "undefined") return null;
  return sessionStorage.getItem(GUEST_KEY) || getAuthToken();
}

export function setToken(token: string | null) {
  if (typeof window === "undefined") return;
  if (token) localStorage.setItem(TOKEN_KEY, token);
  else localStorage.removeItem(TOKEN_KEY);
}

export function setGuestToken(token: string | null) {
  if (typeof window === "undefined") return;
  if (token) sessionStorage.setItem(GUEST_KEY, token);
  else sessionStorage.removeItem(GUEST_KEY);
}

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

async function request<T>(path: string, init: RequestInit = {}, anonymous = false): Promise<T> {
  const token = anonymous ? null : getToken();
  const headers = new Headers(init.headers);
  if (!(init.body instanceof FormData) && !headers.has("Content-Type") && init.body) {
    headers.set("Content-Type", "application/json");
  }
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const res = await fetch(`${API_URL}${path}`, { ...init, headers });
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  const data = text ? JSON.parse(text) : null;
  if (!res.ok) {
    throw new ApiError(res.status, data?.message ?? `Request failed (${res.status})`);
  }
  return data as T;
}

export const api = {
  login: (email: string, password: string) =>
    request<AuthResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    }, true),
  me: () => request<User>("/api/auth/me"),
  creditOfficers: () => request<User[]>("/api/users/credit-officers"),
  meetings: () => request<MeetingListItem[]>("/api/meetings"),
  meeting: (id: string) => request<MeetingDetail>(`/api/meetings/${id}`),
  schedule: (body: {
    bank: string;
    branch: string;
    groupId: string;
    memberId: string;
    memberName: string;
    scheduledAt: string;
    creditOfficerId?: string;
  }) =>
    request<MeetingDetail>("/api/meetings", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  hostCurrent: (slug: string) => request<HostLobby>(`/api/host/${slug}`),
  hostToken: (slug: string) =>
    request<JoinTokenResponse>(`/api/host/${slug}/token`, { method: "POST" }),
  hostWaiting: (slug: string) => request<WaitingOfficer[]>(`/api/host/${slug}/waiting`),
  admit: (slug: string, waitingId: string) =>
    request<AdmitResponse>(`/api/host/${slug}/waiting/${waitingId}/admit`, {
      method: "POST",
    }),
  join: (body: {
    slug: string;
    bank: string;
    branch: string;
    groupId: string;
    memberId: string;
    displayName?: string;
  }) =>
    request<WaitResponse>(
      "/api/join",
      { method: "POST", body: JSON.stringify(body) },
      true,
    ),
  waiting: (id: string) => request<WaitingRoom>(`/api/waiting/${id}`),
  waitingChat: (id: string, body: string, recipientWaitingOfficerId: string | null) =>
    request<LobbyMessage>(`/api/waiting/${id}/chat`, {
      method: "POST",
      body: JSON.stringify({ body, recipientWaitingOfficerId }),
    }),
  waitingLeave: (id: string) =>
    request<void>(`/api/waiting/${id}/leave`, { method: "POST" }),
  waitingConnect: (id: string) =>
    request<JoinTokenResponse>(`/api/waiting/${id}/connect`, { method: "POST" }),
  reportGeotag: (
    id: string,
    body: {
      latitude: number | null;
      longitude: number | null;
      accuracyMeters: number | null;
      capturedAt: string | null;
      error: string | null;
    },
  ) =>
    request<WaitingOfficer>(`/api/waiting/${id}/geotag`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  denyWaiting: (id: string) =>
    request<void>(`/api/waiting/${id}/deny`, { method: "POST" }),
  createCreditOfficer: (body: { name: string; email: string; temporaryPassword: string }) =>
    request<User>("/api/users/credit-officers", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  endMeeting: (id: string) =>
    request<MeetingDetail>(`/api/meetings/${id}/end`, { method: "POST" }),
  postChat: (id: string, body: string) =>
    request<ChatMessage>(`/api/meetings/${id}/chat`, {
      method: "POST",
      body: JSON.stringify({ body }),
    }),
  uploadSnapshot: (id: string, file: Blob, crop: { x: number; y: number; width: number; height: number }) => {
    const form = new FormData();
    form.append("file", file, "capture.png");
    form.append("cropX", String(Math.round(crop.x)));
    form.append("cropY", String(Math.round(crop.y)));
    form.append("cropWidth", String(Math.round(crop.width)));
    form.append("cropHeight", String(Math.round(crop.height)));
    return request<Snapshot>(`/api/meetings/${id}/snapshots`, { method: "POST", body: form });
  },
};
