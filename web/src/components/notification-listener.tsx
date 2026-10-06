"use client";

import { HttpTransportType, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useEffect } from "react";
import { toast } from "sonner";
import { getAuthToken, hubUrl } from "@/lib/api";
import type { LobbyMessage, NotificationPayload } from "@/lib/types";
import { useAuth } from "@/components/auth-provider";
import { useNotifications } from "@/components/notification-center";

export function NotificationListener() {
  const { user } = useAuth();
  const { addFromPayload, addChatNotice } = useNotifications();

  useEffect(() => {
    if (!user) return;
    const token = getAuthToken();
    if (!token) return;

    let cancelled = false;
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl(), {
        accessTokenFactory: () => getAuthToken() ?? token,
        transport:
          HttpTransportType.WebSockets |
          HttpTransportType.ServerSentEvents |
          HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("notify", (payload: NotificationPayload) => {
      addFromPayload(payload);
      toast(payload.message, {
        description:
          payload.bank || payload.groupId
            ? `${payload.bank ?? ""} · ${payload.branch ?? ""} · ${payload.groupId ?? ""} · ${payload.memberId ?? ""}`
            : payload.type,
        action: {
          label: payload.type === "WaitingArrived" ? "Open lobby" : "Open",
          onClick: () => {
            window.location.href =
              payload.type === "WaitingArrived" && payload.code
                ? `/host/${payload.code}`
                : "/dashboard";
          },
        },
      });
    });
    connection.on("chatNotify", (message: LobbyMessage) => {
      if (message.senderRole !== "FieldGuest") return;
      if (!message.recipientWaitingOfficerId) return;
      if (message.senderUserId === user.id) return;
      const lobbyHref = user.hostSlug ? `/host/${user.hostSlug}` : "/dashboard";
      addChatNotice(lobbyHref, message.senderName, message.body, message.id);
      if (window.location.pathname.startsWith("/host/")) return;
      toast(message.senderName || "Field officer", {
        description: message.body,
        duration: 10_000,
        action: user.hostSlug
          ? {
              label: "Open lobby",
              onClick: () => {
                window.location.href = `/host/${user.hostSlug}`;
              },
            }
          : undefined,
      });
    });

    const connect = window.setTimeout(() => {
      if (cancelled) return;
      connection.start().catch((err: Error) => {
        if (cancelled) return;
        const message = err?.message ?? "";
        if (message.includes("stopped during negotiation")) return;
        toast.error("Live notifications unavailable. Refresh if the dashboard looks stale.");
      });
    }, 50);

    return () => {
      cancelled = true;
      window.clearTimeout(connect);
      void connection.stop();
    };
  }, [user, addFromPayload, addChatNotice]);

  return null;
}
