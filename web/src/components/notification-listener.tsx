"use client";

import { HttpTransportType, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useEffect } from "react";
import { toast } from "sonner";
import { getAuthToken, hubUrl } from "@/lib/api";
import type { NotificationPayload } from "@/lib/types";
import { useAuth } from "@/components/auth-provider";

export function NotificationListener() {
  const { user } = useAuth();

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
  }, [user]);

  return null;
}
