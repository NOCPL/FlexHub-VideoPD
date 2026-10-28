"use client";

import { FormEvent, useEffect, useState } from "react";
import { useStickToBottom } from "@/hooks/use-stick-to-bottom";
import { useParams, useRouter } from "next/navigation";
import { HttpTransportType, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { Clock, MessageSquare, UserRoundX, UsersRound, VideoOff } from "lucide-react";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { useAuth } from "@/components/auth-provider";
import { useNotifications } from "@/components/notification-center";
import { MeetingSession } from "@/components/meeting-session";
import { MetadataWorkspace, PhotosWorkspace, PlanWorkspace } from "@/components/engagement-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { api, ApiError, getAuthToken, hubUrl, setGuestToken } from "@/lib/api";
import { geotagSummary, hasCoordinates } from "@/lib/geotag";
import { tokenLabel, waitEstimateLabel } from "@/lib/queue";
import type { ApplicationListItem, JoinTokenResponse, LobbyMessage, MeetingDetail, SessionDetail, WaitingOfficer } from "@/lib/types";

export default function HostPage() {
  const params = useParams<{ slug: string }>();
  const router = useRouter();
  const { user, loading } = useAuth();
  const { addChatNotice, syncWaiting } = useNotifications();
  const slug = params.slug ?? "";
  const [waiting, setWaiting] = useState<WaitingOfficer[]>([]);
  const [active, setActive] = useState<WaitingOfficer | null>(null);
  const [chatTarget, setChatTarget] = useState<string>("all");
  const [messages, setMessages] = useState<LobbyMessage[]>([]);
  const [unread, setUnread] = useState<Record<string, number>>({});
  const [session, setSession] = useState<JoinTokenResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [admitting, setAdmitting] = useState<string | null>(null);
  const [draft, setDraft] = useState("");
  const [visit, setVisit] = useState<MeetingDetail | null>(null);
  const [sessionPlan, setSessionPlan] = useState<SessionDetail | null>(null);
  const [sessionError, setSessionError] = useState<string | null>(null);
  const [apps, setApps] = useState<ApplicationListItem[]>([]);
  const [tab, setTab] = useState<"waiting" | "chat" | "plan" | "photos" | "meta">("waiting");

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
        setWaiting(lobby.waiting ?? []);
        setActive(lobby.active ?? null);
        setMessages(lobby.chat ?? []);
        setVisit(lobby.meeting ?? null);
        if (lobby.active) {
          api.hostToken(slug).then((token) => {
            if (!cancelled) setSession(token);
          }).catch(() => undefined);
        }
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : "Host link not found.");
      });
    return () => {
      cancelled = true;
    };
  }, [user, slug]);

  useEffect(() => {
    if (!user) return;
    const meetingId = visit?.id;
    const groupId = active?.groupId || visit?.groupId;
    const memberId = active?.memberId || visit?.memberId;
    if (!meetingId && !groupId) return;
    let cancelled = false;
    api
      .ensureSession({
        meetingId,
        bank: active?.bank || visit?.bank,
        branch: active?.branch || visit?.branch,
        groupId,
        memberId,
      })
      .then((plan) => {
        if (cancelled) return;
        setSessionPlan(plan);
        setSessionError(null);
        setTab((current) => (current === "waiting" && waiting.length > 0 ? current : "plan"));
      })
      .catch(async (err) => {
        if (cancelled) return;
        setSessionPlan(null);
        setSessionError(err instanceof Error ? err.message : "No application linked to this visit.");
        try {
          setApps(await api.applications());
        } catch {
          /* ignore */
        }
      });
    return () => {
      cancelled = true;
    };
  }, [user, visit?.id, active?.id, active?.groupId, active?.memberId]);

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
        list.some((w) => w.id === officer.id) ? list : [...list, officer],
      );
    });
    connection.on("waitingLeft", (id: string) => {
      setWaiting((list) => list.filter((w) => w.id !== id));
      setUnread((current) => {
        const next = { ...current };
        delete next[id];
        return next;
      });
    });
    connection.on("lobbyChat", (message: LobbyMessage) => {
      setMessages((list) => list.some((m) => m.id === message.id) ? list : [...list, message]);
      if (message.senderRole === "FieldGuest") {
        const target = message.recipientWaitingOfficerId ?? "all";
        setUnread((current) => ({ ...current, [target]: (current[target] ?? 0) + 1 }));
        addChatNotice(`/host/${slug}`, message.senderName, message.body, message.id);
        if (message.recipientWaitingOfficerId) {
          setChatTarget(message.recipientWaitingOfficerId);
          toast(message.senderName || "Field officer", {
            description: message.body,
            duration: 10_000,
          });
        }
      }
    });
    connection.on("admitted", (waitingId: string) => {
      setWaiting((list) => list.filter((w) => w.id !== waitingId));
      setUnread((current) => {
        const next = { ...current };
        delete next[waitingId];
        return next;
      });
    });
    connection.on("lobbyChatCleared", () => {
      setMessages([]);
    });
    connection.on("activeEnded", (waitingId: string) => {
      setActive((current) => current?.id === waitingId ? null : current);
      setSession(null);
      void api
        .hostCurrent(slug)
        .then((lobby) => {
          setWaiting(lobby.waiting ?? []);
          setMessages(lobby.chat ?? []);
          if (lobby.active) setActive(lobby.active);
          else setActive(null);
        })
        .catch(() => undefined);
    });
    connection.on("activeConnected", (waitingId: string, connectedAt: string) => {
      setActive((current) =>
        current?.id === waitingId ? { ...current, status: "Connected", connectedAt } : current,
      );
    });
    connection.on("geotagUpdated", (officer: WaitingOfficer) => {
      setWaiting((list) => list.map((item) => (item.id === officer.id ? { ...item, ...officer } : item)));
      setActive((current) => (current?.id === officer.id ? { ...current, ...officer } : current));
    });
    connection.on("queueUpdated", (officers: WaitingOfficer[]) => {
      if (Array.isArray(officers)) setWaiting(officers);
    });
    connection.on("queueShifted", () => {
      void api
        .hostCurrent(slug)
        .then((lobby) => {
          setWaiting(lobby.waiting ?? []);
          setMessages(lobby.chat ?? []);
          if (lobby.active) setActive(lobby.active);
          else setActive(null);
        })
        .catch(() => undefined);
    });

    const connect = window.setTimeout(() => {
      if (cancelled) return;
      connection
        .start()
        .then(() => {
          if (!cancelled) return connection.invoke("WatchHost", slug);
        })
        .catch((err: Error) => {
          if (cancelled) return;
          if ((err?.message ?? "").includes("stopped during negotiation")) return;
        });
    }, 50);

    const poll = window.setInterval(() => {
      api
        .hostCurrent(slug)
        .then((lobby) => {
          setWaiting(lobby.waiting ?? []);
          setMessages(lobby.chat ?? []);
          if (lobby.active) setActive(lobby.active);
          else setActive(null);
        })
        .catch(() => undefined);
    }, 4000);

    return () => {
      cancelled = true;
      window.clearTimeout(connect);
      window.clearInterval(poll);
      void connection.stop();
    };
  }, [user, slug, addChatNotice]);

  useEffect(() => {
    if (!slug) return;
    syncWaiting(slug, waiting);
  }, [slug, waiting, syncWaiting]);

  async function admit(id: string) {
    setAdmitting(id);
    try {
      const result = await api.admit(slug, id);
      setWaiting((list) => {
        const next = list.filter((w) => w.id !== id);
        return next;
      });
      setActive(result.waiting);
      if (chatTarget === id) setChatTarget("all");
      setSession(result.host);
      setVisit(result.host.meeting);
      setTab("plan");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not admit the field officer.");
    } finally {
      setAdmitting(null);
    }
  }

  async function deny(id: string) {
    try {
      await api.denyWaiting(id);
      setWaiting((list) => list.filter((w) => w.id !== id));
      if (chatTarget === id) setChatTarget("all");
      toast.success("You declined this field officer.");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not remove the field officer.");
    }
  }

  async function sendChat(e: FormEvent) {
    e.preventDefault();
    const anchor = chatTarget === "all" ? waiting[0] : waiting.find((w) => w.id === chatTarget);
    if (!anchor) return;
    const body = draft.trim();
    if (!body) return;
    setDraft("");
    try {
      const saved = await api.waitingChat(
        anchor.id,
        body,
        chatTarget === "all" ? null : chatTarget,
      );
      setMessages((list) => list.some((m) => m.id === saved.id) ? list : [...list, saved]);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Chat could not be sent.");
    }
  }

  const visibleMessages = (messages ?? []).filter((message) =>
    chatTarget === "all"
      ? message.recipientWaitingOfficerId === null
      : message.recipientWaitingOfficerId === chatTarget);
  const chatEndRef = useStickToBottom(
    `${chatTarget}:${visibleMessages.at(-1)?.id ?? "empty"}:${visibleMessages.length}`,
  );

  if (loading || !user) {
    return <div className="min-h-full bg-[#f0f2f5] p-8 text-[#5a6a84]">Checking your session…</div>;
  }

  if (error) {
    return (
      <div className="min-h-full bg-[#f0f2f5]">
        <AppHeader />
        <main className="mx-auto max-w-lg px-4 py-10 text-sm text-destructive">{error}</main>
      </div>
    );
  }

  return (
    <div className="flex h-dvh flex-col bg-[#f0f2f5]">
      <AppHeader />
      <div className="flex min-h-0 flex-1 bg-[#f0f2f5] p-3">
        <main className="flex min-h-0 min-w-0 flex-1 flex-col">
          {active ? (
            <div className="mb-3 flex items-center gap-3 rounded-xl border bg-white px-4 py-2">
              <div className="min-w-0 flex-1">
                <div className="font-semibold text-[#10264e]">{active.displayName}</div>
                <div className="truncate text-sm text-[#5a6a84]">
                  {active.bank || "—"} · {active.branch || "—"} · {active.groupId || "—"} · {active.memberId || "—"}
                </div>
                <div className={`truncate text-xs ${hasCoordinates(active) ? "text-[#29416f]" : "text-[#005792]"}`}>
                  {geotagSummary(active)}
                </div>
              </div>
            </div>
          ) : null}
          {session ? (
            <MeetingSession
              token={session.token}
              serverUrl={session.liveKitUrl}
              meeting={session.meeting}
              user={user}
              fieldOfficer={active}
              onSnapshotSaved={() => {
                if (sessionPlan) void api.session(sessionPlan.id).then(setSessionPlan).catch(() => undefined);
              }}
              onLeave={() => {
                setSession(null);
                void api
                  .hostCurrent(slug)
                  .then((lobby) => {
                    setWaiting(lobby.waiting ?? []);
                    setMessages(lobby.chat ?? []);
                    setActive(lobby.active ?? null);
                    setVisit(lobby.meeting ?? null);
                  })
                  .catch(() => undefined);
              }}
            />
          ) : (
            <div className="flex flex-1 flex-col items-center justify-center rounded-2xl bg-[radial-gradient(120%_90%_at_50%_15%,#16315c_0%,#0b1b36_60%,#08142a_100%)] px-6 text-center text-white">
              <div className="mb-4 flex size-16 items-center justify-center rounded-xl bg-[#709ac5]/15 text-[#9fb4d4]"><VideoOff /></div>
              <p className="text-xl font-semibold">No one in the call yet</p>
              <p className="mt-2 max-w-sm text-sm text-[#9fb4d4]">Admit a field officer from the waiting room on the right. Their video fills this screen.</p>
              {waiting[0] ? (
                <Button
                  className="mt-5 bg-[#005792] hover:bg-[#004675]"
                  disabled={!hasCoordinates(waiting[0])}
                  onClick={() => admit(waiting[0].id)}
                >
                  {hasCoordinates(waiting[0]) ? "Admit next officer" : "Waiting for GPS"}
                </Button>
              ) : null}
            </div>
          )}
        </main>

        <aside className="ml-3 flex w-[min(440px,40vw)] min-w-80 shrink-0 flex-col overflow-hidden rounded-xl border bg-white">
          <div className="flex shrink-0 flex-wrap gap-1 border-b p-2">
            {([
              ["waiting", `Waiting${waiting.length ? ` ${waiting.length}` : ""}`],
              ["chat", "Chat"],
              ["plan", "Plan"],
              ["photos", "Photos"],
              ["meta", "Metadata"],
            ] as const).map(([id, label]) => (
              <button
                key={id}
                type="button"
                className={`rounded-full px-3 py-1 text-xs font-semibold ${tab === id ? "bg-[#005792] text-white" : "border border-[#d7deea] text-[#29416f]"}`}
                onClick={() => setTab(id)}
              >
                {label}
              </button>
            ))}
          </div>

          {tab === "waiting" ? (
            <div className="min-h-0 flex-1 overflow-y-auto">
              <div className="flex items-center gap-2 border-b px-3 py-3 text-[#29416f]">
                <UsersRound className="size-4" />
                <span className="font-semibold text-[#10264e]">Waiting room</span>
              </div>
              {waiting.length === 0 ? <p className="p-4 text-center text-sm text-[#5a6a84]">No one is waiting.</p> : waiting.map((officer) => (
                <div key={officer.id} className="border-b p-3 last:border-b-0">
                  <div className="flex items-start gap-2">
                    <span className="mt-0.5 inline-flex size-8 shrink-0 items-center justify-center rounded-full bg-[#e5eef4] text-sm font-bold text-[#005792]">
                      {tokenLabel(officer.tokenNumber)}
                    </span>
                    <div className="min-w-0 flex-1">
                      <div className="font-semibold text-[#10264e]">{officer.displayName}</div>
                      <div className="text-xs text-[#5a6a84]">{officer.bank || "—"} · {officer.branch || "—"} · {officer.groupId || "—"} · {officer.memberId || "—"}</div>
                    </div>
                  </div>
                  <div className={`mt-1 text-xs ${hasCoordinates(officer) ? "text-[#29416f]" : "text-[#005792]"}`}>
                    {hasCoordinates(officer)
                      ? geotagSummary(officer)
                      : "Waiting for GPS. Admit is blocked until they allow location."}
                  </div>
                  <div className="mt-1 flex items-center gap-1 text-xs text-[#5a6a84]"><Clock className="size-3" /> {waitEstimateLabel(officer.estimatedWaitMinutes)} · in queue <WaitingTimer startedAt={officer.createdAt} /></div>
                  <div className="mt-2 flex gap-2">
                    <Button className="flex-1 bg-[#005792] hover:bg-[#004675]" size="sm" disabled={Boolean(active) || admitting === officer.id || !hasCoordinates(officer)} onClick={() => admit(officer.id)}>{admitting === officer.id ? "Admitting…" : hasCoordinates(officer) ? "Admit" : "Waiting for GPS"}</Button>
                    <Button variant="outline" size="icon-sm" title="Private message" onClick={() => { setChatTarget(officer.id); setUnread((u) => ({ ...u, [officer.id]: 0 })); setTab("chat"); }}><MessageSquare /></Button>
                    <Button variant="outline" size="icon-sm" title="Decline" className="text-destructive" onClick={() => deny(officer.id)}><UserRoundX /></Button>
                  </div>
                </div>
              ))}
            </div>
          ) : null}

          {tab === "chat" ? (
            <section className="flex min-h-0 flex-1 flex-col overflow-hidden">
              <div className="border-b px-3 py-3">
                <div className="font-semibold text-[#10264e]">Chat</div>
                <div className="mt-2 flex gap-1 overflow-x-auto">
                  <button className={`rounded-full px-3 py-1 text-xs font-semibold ${chatTarget === "all" ? "bg-[#005792] text-white" : "border"}`} onClick={() => { setChatTarget("all"); setUnread((u) => ({ ...u, all: 0 })); }}>Everyone</button>
                  {waiting.map((officer) => <button key={officer.id} className={`rounded-full px-3 py-1 text-xs font-semibold ${chatTarget === officer.id ? "bg-[#005792] text-white" : "border"}`} onClick={() => { setChatTarget(officer.id); setUnread((u) => ({ ...u, [officer.id]: 0 })); }}>{officer.displayName.split(" ")[0]}</button>)}
                </div>
              </div>
              <div className="min-h-0 flex-1 space-y-2 overflow-y-auto bg-[#fafbfd] p-3">
                {visibleMessages.length === 0 ? <p className="text-center text-xs text-[#5a6a84]">No messages in this conversation.</p> : visibleMessages.map((message) => (
                  <div key={message.id} className={`flex ${message.senderRole !== "FieldGuest" ? "justify-end" : ""}`}>
                    <div className={`max-w-[86%] rounded-xl px-3 py-2 text-sm ${message.senderRole !== "FieldGuest" ? "bg-[#005792] text-white" : "border bg-white text-[#10264e]"}`}>
                      <div className="text-[11px] font-semibold opacity-70">{message.senderRole !== "FieldGuest" ? "You" : message.senderName}</div>
                      <div>{message.body}</div>
                      <div className="mt-1 text-right text-[10px] opacity-60">{new Date(message.sentAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</div>
                    </div>
                  </div>
                ))}
                <div ref={chatEndRef} />
              </div>
              <form onSubmit={sendChat} className="flex gap-2 border-t p-2">
                <Input value={draft} onChange={(e) => setDraft(e.target.value)} placeholder={`Message ${chatTarget === "all" ? "everyone" : waiting.find((w) => w.id === chatTarget)?.displayName ?? "officer"}…`} disabled={waiting.length === 0} />
                <Button type="submit" className="bg-[#005792] hover:bg-[#004675]" disabled={waiting.length === 0}>Send</Button>
              </form>
            </section>
          ) : null}

          {tab === "plan" ? (
            sessionPlan ? (
              <PlanWorkspace compact session={sessionPlan} onChange={setSessionPlan} />
            ) : (
              <div className="space-y-3 p-4 text-sm text-[#5a6a84]">
                <p>{sessionError ?? "Link an application to open the Business LAP plan."}</p>
                {apps.map((app) => (
                  <Button
                    key={app.id}
                    variant="outline"
                    className="w-full justify-start"
                    onClick={() => {
                      void api.ensureSession({ applicationId: app.id, meetingId: visit?.id }).then((plan) => {
                        setSessionPlan(plan);
                        setSessionError(null);
                      }).catch((err) => toast.error(err instanceof Error ? err.message : "Could not open session."));
                    }}
                  >
                    {app.code} · {app.displayName}
                  </Button>
                ))}
              </div>
            )
          ) : null}

          {tab === "photos" ? <PhotosWorkspace photos={sessionPlan?.photos ?? visit?.snapshots ?? []} /> : null}
          {tab === "meta" ? (
            sessionPlan ? <MetadataWorkspace session={sessionPlan} /> : <p className="p-4 text-sm text-[#5a6a84]">Open a Plan session to see metadata.</p>
          ) : null}
        </aside>
      </div>
    </div>
  );
}

function WaitingTimer({ startedAt }: { startedAt: string }) {
  const now = useNow();
  return <span>{formatDuration(Math.max(0, Math.floor((now - new Date(startedAt).getTime()) / 1000)))}</span>;
}

function useNow() {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const tick = () => setNow(Date.now());
    tick();
    const timer = window.setInterval(tick, 1000);
    const onVisible = () => {
      if (document.visibilityState === "visible") tick();
    };
    document.addEventListener("visibilitychange", onVisible);
    window.addEventListener("focus", tick);
    return () => {
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisible);
      window.removeEventListener("focus", tick);
    };
  }, []);
  return now;
}

function formatDuration(totalSeconds: number) {
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return [hours, minutes, seconds].map((part) => String(part).padStart(2, "0")).join(":");
}
