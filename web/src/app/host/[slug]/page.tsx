"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { HttpTransportType, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { useAuth } from "@/components/auth-provider";
import { MeetingSession } from "@/components/meeting-session";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { api, ApiError, getAuthToken, hubUrl, setGuestToken } from "@/lib/api";
import type { ChatMessage, JoinTokenResponse, WaitingOfficer } from "@/lib/types";

export default function HostPage() {
  const params = useParams<{ slug: string }>();
  const router = useRouter();
  const { user, loading } = useAuth();
  const slug = params.slug ?? "";
  const [waiting, setWaiting] = useState<WaitingOfficer[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [session, setSession] = useState<JoinTokenResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [admitting, setAdmitting] = useState<string | null>(null);
  const [draft, setDraft] = useState("");

  useEffect(() => {
    if (!loading && !user) {
      router.replace(`/?next=${encodeURIComponent(`/host/${slug}`)}`);
    }
  }, [loading, user, router, slug]);

  useEffect(() => {
    if (!user || !slug) return;
    let cancelled = false;
    setGuestToken(null);
    api
      .hostCurrent(slug)
      .then((lobby) => {
        if (cancelled) return;
        setWaiting(lobby.waiting);
        setSelectedId((current) => current ?? lobby.waiting[0]?.id ?? null);
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : "Host link not found.");
      });
    return () => {
      cancelled = true;
    };
  }, [user, slug]);

  useEffect(() => {
    if (!user || !slug) return;
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

    connection.on("waitingArrived", (officer: WaitingOfficer) => {
      setWaiting((list) =>
        list.some((w) => w.id === officer.id) ? list : [...list, { ...officer, chat: officer.chat ?? [] }],
      );
      setSelectedId((current) => current ?? officer.id);
      toast(`${officer.displayName} is waiting`, {
        description: `${officer.bank} · ${officer.groupId} · ${officer.memberId}`,
      });
    });
    connection.on("waitingLeft", (id: string) => {
      setWaiting((list) => list.filter((w) => w.id !== id));
      setSelectedId((current) => (current === id ? null : current));
    });
    connection.on("lobbyChat", (waitingId: string, message: ChatMessage) => {
      setWaiting((list) =>
        list.map((w) => {
          if (w.id !== waitingId || w.chat.some((m) => m.id === message.id)) return w;
          return { ...w, chat: [...w.chat, message] };
        }),
      );
    });
    connection.on("admitted", (waitingId: string) => {
      setWaiting((list) => list.filter((w) => w.id !== waitingId));
      setSelectedId((current) => (current === waitingId ? null : current));
    });

    connection
      .start()
      .then(() => connection.invoke("WatchHost", slug))
      .catch((err: Error) => {
        if (cancelled) return;
        if ((err?.message ?? "").includes("stopped during negotiation")) return;
      });

    const poll = window.setInterval(() => {
      api
        .hostWaiting(slug)
        .then((list) => {
          setWaiting((prev) =>
            list.map((item) => {
              const existing = prev.find((w) => w.id === item.id);
              return existing ? { ...item, chat: existing.chat } : { ...item, chat: item.chat ?? [] };
            }),
          );
        })
        .catch(() => undefined);
    }, 4000);

    return () => {
      cancelled = true;
      window.clearInterval(poll);
      void connection.stop();
    };
  }, [user, slug]);

  useEffect(() => {
    if (!selectedId) return;
    api
      .waiting(selectedId)
      .then((full) => {
        setWaiting((list) => list.map((w) => (w.id === full.id ? { ...w, chat: full.chat } : w)));
      })
      .catch(() => undefined);
  }, [selectedId]);

  const selected = useMemo(
    () => waiting.find((w) => w.id === selectedId) ?? null,
    [waiting, selectedId],
  );

  async function admit(id: string) {
    setAdmitting(id);
    try {
      const result = await api.admit(slug, id);
      setWaiting((list) => {
        const next = list.filter((w) => w.id !== id);
        setSelectedId((current) => (current === id ? next[0]?.id ?? null : current));
        return next;
      });
      setSession(result.host);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not admit the field officer.");
    } finally {
      setAdmitting(null);
    }
  }

  async function sendChat(e: FormEvent) {
    e.preventDefault();
    if (!selected) return;
    const body = draft.trim();
    if (!body) return;
    setDraft("");
    try {
      const saved = await api.waitingChat(selected.id, body);
      setWaiting((list) =>
        list.map((w) => {
          if (w.id !== selected.id || w.chat.some((m) => m.id === saved.id)) return w;
          return { ...w, chat: [...w.chat, saved] };
        }),
      );
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Chat could not be sent.");
    }
  }

  if (loading || !user) {
    return <div className="p-8 text-muted-foreground">Checking your session…</div>;
  }

  if (error) {
    return (
      <div className="min-h-full bg-background">
        <AppHeader />
        <main className="mx-auto max-w-lg px-4 py-10 text-sm text-destructive">{error}</main>
      </div>
    );
  }

  const chatPanel = (
    <div className="flex min-h-0 min-w-0 flex-1 flex-col border-t">
      {selected ? (
        <>
          <div className="border-b px-3 py-2">
            <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Chat</div>
            <div className="mt-0.5 truncate text-sm font-medium">{selected.displayName}</div>
            <p className="text-muted-foreground truncate text-xs">
              {selected.bank || "—"} · {selected.groupId || "—"} · {selected.memberId || "—"}
            </p>
            <Button
              className="mt-2 w-full"
              size="sm"
              disabled={admitting === selected.id}
              onClick={() => admit(selected.id)}
            >
              {admitting === selected.id ? "Admitting…" : "Admit to call"}
            </Button>
          </div>
          <div className="min-h-0 flex-1 space-y-2 overflow-y-auto p-3 text-sm">
            {selected.chat.length === 0 ? (
              <p className="text-muted-foreground text-xs">
                Chat with this waiting officer. After you admit them they speak on the call.
              </p>
            ) : (
              selected.chat.map((m) => (
                <div key={m.id}>
                  <div className="text-muted-foreground text-xs">
                    {m.senderName} · {m.senderRole === "FieldGuest" ? "Field" : "Credit"}
                  </div>
                  <div>{m.body}</div>
                </div>
              ))
            )}
          </div>
          <form onSubmit={sendChat} className="flex gap-2 border-t p-2">
            <Input
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              placeholder="Message"
              className="h-8"
            />
            <Button type="submit" size="sm">
              Send
            </Button>
          </form>
        </>
      ) : (
        <div className="text-muted-foreground flex flex-1 items-center justify-center p-3 text-center text-xs">
          Select a waiting field officer to chat.
        </div>
      )}
    </div>
  );

  return (
    <div className="flex h-dvh flex-col bg-background">
      <AppHeader />
      <div className="flex min-h-0 flex-1">
        <aside className="flex w-56 shrink-0 flex-col border-r bg-card sm:w-64">
          <div className="border-b px-3 py-2">
            <div className="text-sm font-medium">Waiting</div>
            <p className="text-muted-foreground text-xs">Lobby and chat</p>
          </div>
          <div className="max-h-40 shrink-0 overflow-y-auto p-2 sm:max-h-48">
            {waiting.length === 0 ? (
              <p className="text-muted-foreground p-2 text-xs">
                No one is waiting. Field officers appear here when they open their join link.
              </p>
            ) : (
              waiting.map((officer) => (
                <button
                  key={officer.id}
                  type="button"
                  className={`mb-1 w-full rounded-md px-2 py-1.5 text-left text-sm ${
                    selectedId === officer.id ? "bg-primary/10" : "hover:bg-muted"
                  }`}
                  onClick={() => setSelectedId(officer.id)}
                >
                  <div className="truncate font-medium">{officer.displayName}</div>
                  <div className="text-muted-foreground truncate text-xs">
                    {officer.bank || "—"} · {officer.groupId || "—"} · {officer.memberId || "—"}
                  </div>
                </button>
              ))
            )}
          </div>
          {chatPanel}
        </aside>

        <main className="flex min-h-0 min-w-0 flex-1 flex-col bg-[#0f1614]">
          {session ? (
            <MeetingSession
              token={session.token}
              serverUrl={session.liveKitUrl}
              meeting={session.meeting}
              user={user}
              onLeave={() => setSession(null)}
            />
          ) : (
            <div className="flex flex-1 flex-col items-center justify-center gap-2 px-6 text-center text-white">
              <p className="text-lg font-medium">Video</p>
              <p className="max-w-sm text-sm text-white/70">
                Waiting officers and chat stay in the narrow panel. Admit someone to fill this area
                with the call.
              </p>
              <Button variant="secondary" onClick={() => router.push("/dashboard")}>
                Dashboard
              </Button>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
