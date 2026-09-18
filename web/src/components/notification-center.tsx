"use client";

import { createContext, useCallback, useContext, useMemo, useState } from "react";
import type { NotificationPayload, WaitingOfficer } from "@/lib/types";

export type NoticeKind = "waiting" | "chat" | "system";

export type Notice = {
  id: string;
  kind: NoticeKind;
  title: string;
  description?: string;
  href: string;
  read: boolean;
  at: number;
};

type NotificationState = {
  notices: Notice[];
  unreadCount: number;
  addNotice: (notice: Omit<Notice, "read" | "at"> & { read?: boolean; at?: number }) => void;
  addFromPayload: (payload: NotificationPayload) => void;
  addChatNotice: (href: string, senderName: string, body: string, messageId: string) => void;
  syncWaiting: (slug: string, officers: WaitingOfficer[]) => void;
  markRead: (id: string) => void;
  markAllRead: () => void;
};

const NotificationContext = createContext<NotificationState | null>(null);

export function NotificationProvider({ children }: { children: React.ReactNode }) {
  const [notices, setNotices] = useState<Notice[]>([]);

  const addNotice = useCallback((notice: Omit<Notice, "read" | "at"> & { read?: boolean; at?: number }) => {
    setNotices((prev) => {
      const rest = prev.filter((item) => item.id !== notice.id);
      return [
        {
          ...notice,
          read: notice.read ?? false,
          at: notice.at ?? Date.now(),
        },
        ...rest,
      ]
        .sort((a, b) => b.at - a.at)
        .slice(0, 40);
    });
  }, []);

  const addFromPayload = useCallback(
    (payload: NotificationPayload) => {
      const href =
        payload.type === "WaitingArrived" && payload.code
          ? `/host/${payload.code}`
          : payload.meetingId && payload.meetingId !== "00000000-0000-0000-0000-000000000000"
            ? `/video-pd/${payload.meetingId}`
            : "/dashboard";
      addNotice({
        id: `${payload.type}:${payload.code}:${payload.meetingId}:${payload.message}`,
        kind: payload.type === "WaitingArrived" ? "waiting" : "system",
        title: payload.message,
        description:
          payload.bank || payload.groupId
            ? [payload.bank, payload.branch, payload.groupId, payload.memberId].filter(Boolean).join(" · ")
            : undefined,
        href,
      });
    },
    [addNotice],
  );

  const addChatNotice = useCallback(
    (href: string, senderName: string, body: string, messageId: string) => {
      addNotice({
        id: `chat:${messageId}`,
        kind: "chat",
        title: `Chat from ${senderName}`,
        description: body,
        href,
      });
    },
    [addNotice],
  );

  const syncWaiting = useCallback((slug: string, officers: WaitingOfficer[]) => {
    setNotices((prev) => {
      const keep = prev.filter((item) => item.kind !== "waiting");
      const waitingItems: Notice[] = officers.map((officer) => {
        const id = `waiting:${officer.id}`;
        const existing = prev.find((item) => item.id === id);
        return {
          id,
          kind: "waiting",
          title: `${officer.displayName} is waiting`,
          description: [officer.bank, officer.branch, officer.groupId, officer.memberId]
            .filter(Boolean)
            .join(" · "),
          href: `/host/${slug}`,
          read: existing?.read ?? false,
          at: existing?.at ?? new Date(officer.createdAt).getTime(),
        };
      });
      return [...waitingItems, ...keep].sort((a, b) => b.at - a.at).slice(0, 40);
    });
  }, []);

  const markRead = useCallback((id: string) => {
    setNotices((prev) => prev.map((item) => (item.id === id ? { ...item, read: true } : item)));
  }, []);

  const markAllRead = useCallback(() => {
    setNotices((prev) => prev.map((item) => ({ ...item, read: true })));
  }, []);

  const value = useMemo<NotificationState>(
    () => ({
      notices,
      unreadCount: notices.filter((item) => !item.read).length,
      addNotice,
      addFromPayload,
      addChatNotice,
      syncWaiting,
      markRead,
      markAllRead,
    }),
    [notices, addNotice, addFromPayload, addChatNotice, syncWaiting, markRead, markAllRead],
  );

  return <NotificationContext.Provider value={value}>{children}</NotificationContext.Provider>;
}

export function useNotifications() {
  const ctx = useContext(NotificationContext);
  if (!ctx) throw new Error("useNotifications must be used within NotificationProvider");
  return ctx;
}
