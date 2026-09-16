export type Role = "CreditOfficer" | "Admin" | "FieldGuest";

export type User = {
  id: string;
  name: string;
  email: string;
  role: Role;
  hostSlug?: string | null;
  hostUrl?: string | null;
};

export type AuthResponse = {
  token: string;
  user: User;
};

export type MeetingListItem = {
  id: string;
  code: string;
  bank: string;
  groupId: string;
  memberId: string;
  memberName: string;
  scheduledAt: string;
  status: string;
  creditOfficerName: string;
  creditOfficerId: string;
  hostSlug: string | null;
  hostUrl: string;
  fieldUrl: string;
  durationSeconds: number | null;
  recordingStatus: string | null;
  recordingSegments: number;
};

export type Recording = {
  id: string;
  status: string;
  sequence: number;
  bank: string;
  groupId: string;
  memberId: string;
  filePath: string | null;
  durationSeconds: number | null;
  error: string | null;
  createdAt: string;
  joinedAt: string | null;
  leftAt: string | null;
};

export type Snapshot = {
  id: string;
  bank: string;
  groupId: string;
  memberId: string;
  url: string;
  cropX: number | null;
  cropY: number | null;
  cropWidth: number | null;
  cropHeight: number | null;
  createdAt: string;
};

export type ChatMessage = {
  id: string;
  senderUserId: string;
  senderName: string;
  senderRole: string;
  body: string;
  sentAt: string;
};

export type MeetingDetail = {
  id: string;
  code: string;
  bank: string;
  groupId: string;
  memberId: string;
  memberName: string;
  scheduledAt: string;
  status: string;
  liveKitRoomName: string;
  startedAt: string | null;
  endedAt: string | null;
  durationSeconds: number | null;
  hostUrl: string;
  fieldUrl: string;
  creditOfficer: User;
  recordings: Recording[];
  snapshots: Snapshot[];
  chat: ChatMessage[];
};

export type JoinTokenResponse = {
  token: string;
  guestToken: string;
  liveKitUrl: string;
  identity: string;
  meeting: MeetingDetail;
};

export type WaitingOfficer = {
  id: string;
  displayName: string;
  bank: string;
  groupId: string;
  memberId: string;
  status: string;
  createdAt: string;
  admittedAt: string | null;
  chat: ChatMessage[];
};

export type WaitResponse = {
  guestToken: string;
  waiting: WaitingOfficer;
  creditOfficerName: string;
  hostSlug: string;
};

export type HostLobby = {
  officer: User;
  meeting: MeetingDetail | null;
  waiting: WaitingOfficer[];
};

export type AdmitResponse = {
  field: JoinTokenResponse;
  host: JoinTokenResponse;
  waiting: WaitingOfficer;
};

export type NotificationPayload = {
  type: string;
  meetingId: string;
  code: string;
  message: string;
  bank?: string | null;
  groupId?: string | null;
  memberId?: string | null;
};

export const STATUS_LABEL: Record<string, string> = {
  Scheduled: "Scheduled",
  WaitingForFieldOfficer: "Waiting room",
  InProgress: "In progress",
  Completed: "Completed",
  Cancelled: "Cancelled",
};
