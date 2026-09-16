"use client";

import { FormEvent, Suspense, useEffect, useRef, useState } from "react";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { HttpTransportType, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { toast } from "sonner";
import { MeetingSession } from "@/components/meeting-session";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api, ApiError, getToken, hubUrl, setGuestToken } from "@/lib/api";
import type { ChatMessage, JoinTokenResponse, User, WaitingOfficer } from "@/lib/types";

export default function JoinPage() {
  return (
    <Suspense fallback={<div className="p-8 text-muted-foreground">Loading join link…</div>}>
      <JoinBody />
    </Suspense>
  );
}

function JoinBody() {
  const params = useParams<{ slug: string }>();
  const search = useSearchParams();
  const router = useRouter();
  const slug = params.slug ?? "";
  const bank = search.get("bank") ?? "";
  const groupId = search.get("groupId") ?? "";
  const memberId = search.get("memberId") ?? "";
  const [name, setName] = useState("Field officer");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [creditOfficerName, setCreditOfficerName] = useState("the credit officer");
  const [waiting, setWaiting] = useState<WaitingOfficer | null>(null);
  const [draft, setDraft] = useState("");
  const [session, setSession] = useState<JoinTokenResponse | null>(null);
  const [guest, setGuest] = useState<User | null>(null);
  const connectingRef = useRef(false);

  useEffect(() => {
    connectingRef.current = false;
    setWaiting(null);
    setSession(null);
    setGuest(null);
    setGuestToken(null);
    setError(null);
  }, [slug, bank, groupId, memberId]);

  async function join() {
    setBusy(true);
    setError(null);
    try {
      const result = await api.join({ slug, bank, groupId, memberId, displayName: name });
      setGuestToken(result.guestToken);
      setCreditOfficerName(result.creditOfficerName);
      setWaiting(result.waiting);
      setGuest({
        id: result.waiting.id,
        name,
        email: "",
        role: "FieldGuest",
      });
    } catch (err) {
      const message = err instanceof ApiError ? err.message : err instanceof Error ? err.message : "Join failed.";
      setError(message);
      toast.error(message);
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    if (!waiting || session) return;
    const token = getToken();
    if (!token) return;
    let cancelled = false;
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl(), {
        accessTokenFactory: () => getToken() ?? token,
        transport:
          HttpTransportType.WebSockets |
          HttpTransportType.ServerSentEvents |
          HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("lobbyChat", (_waitingId: string, message: ChatMessage) => {
      setWaiting((current) =>
        current ? { ...current, chat: [...current.chat, message] } : current,
      );
    });
    connection.on("admitted", (joinSession: JoinTokenResponse) => {
      connectingRef.current = true;
      setGuestToken(joinSession.guestToken);
      setSession(joinSession);
    });

    connection.start().catch((err: Error) => {
      if (cancelled) return;
      if ((err?.message ?? "").includes("stopped during negotiation")) return;
    });

    const poll = window.setInterval(() => {
      api
        .waiting(waiting.id)
        .then(async (latest) => {
          setWaiting((current) =>
            current ? { ...latest, chat: latest.chat.length ? latest.chat : current.chat } : latest,
          );
          if (latest.status === "Admitted" && !cancelled && !connectingRef.current) {
            connectingRef.current = true;
            const joinSession = await api.waitingConnect(waiting.id);
            setGuestToken(joinSession.guestToken);
            setSession(joinSession);
          }
        })
        .catch(() => undefined);
    }, 3000);

    return () => {
      cancelled = true;
      window.clearInterval(poll);
      void connection.stop();
    };
  }, [waiting?.id, session]);

  async function sendChat(e: FormEvent) {
    e.preventDefault();
    if (!waiting) return;
    const body = draft.trim();
    if (!body) return;
    setDraft("");
    try {
      const saved = await api.waitingChat(waiting.id, body);
      setWaiting((current) =>
        current ? { ...current, chat: [...current.chat, saved] } : current,
      );
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Chat could not be sent.");
    }
  }

  async function leaveWait() {
    if (waiting) {
      try {
        await api.waitingLeave(waiting.id);
      } catch {
        /* ignore */
      }
    }
    setGuestToken(null);
    setWaiting(null);
    router.push(
      `/join/${slug}?bank=${encodeURIComponent(bank)}&groupId=${encodeURIComponent(groupId)}&memberId=${encodeURIComponent(memberId)}`,
    );
  }

  if (session && guest) {
    return (
      <div className="flex h-dvh flex-col">
        <MeetingSession
          token={session.token}
          serverUrl={session.liveKitUrl}
          meeting={session.meeting}
          user={guest}
          onLeave={() => {
            setGuestToken(null);
            setSession(null);
            setWaiting(null);
            router.push(
              `/join/${slug}?bank=${encodeURIComponent(bank)}&groupId=${encodeURIComponent(groupId)}&memberId=${encodeURIComponent(memberId)}`,
            );
          }}
        />
      </div>
    );
  }

  if (waiting) {
    return (
      <main className="mx-auto grid max-w-3xl gap-4 px-4 py-10 md:grid-cols-[1fr_280px]">
        <Card>
          <CardHeader>
            <CardTitle>Waiting for {creditOfficerName}</CardTitle>
            <CardDescription>
              You are not in the call yet. Chat here until the credit officer admits you, then speak
              on the call.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex h-[28rem] flex-col">
            <div className="min-h-0 flex-1 space-y-3 overflow-y-auto text-sm">
              {waiting.chat.length === 0 ? (
                <p className="text-muted-foreground">No messages yet.</p>
              ) : (
                waiting.chat.map((m) => (
                  <div key={m.id}>
                    <div className="text-muted-foreground text-xs">
                      {m.senderName} · {m.senderRole === "FieldGuest" ? "You" : "Credit"}
                    </div>
                    <div>{m.body}</div>
                  </div>
                ))
              )}
            </div>
            <form onSubmit={sendChat} className="mt-3 flex gap-2">
              <Input
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                placeholder="Message the credit officer"
              />
              <Button type="submit">Send</Button>
            </form>
          </CardContent>
        </Card>
        <Card className="h-fit">
          <CardHeader>
            <CardTitle className="text-base">Your visit tags</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <p>Bank: {waiting.bank || bank || "—"}</p>
            <p>Group: {waiting.groupId || groupId || "—"}</p>
            <p>Member: {waiting.memberId || memberId || "—"}</p>
            <Button variant="outline" className="mt-2 w-full" onClick={leaveWait}>
              Leave queue
            </Button>
          </CardContent>
        </Card>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-lg px-4 py-10">
      <Card>
        <CardHeader>
          <CardTitle>Join visit</CardTitle>
          <CardDescription>
            You will wait until the credit officer admits you. Bank, group, and member IDs are stored
            as-is. They are not validated here.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <dl className="grid grid-cols-3 gap-3 text-sm">
            <div>
              <dt className="text-muted-foreground">Bank</dt>
              <dd className="font-medium">{bank || "—"}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Group ID</dt>
              <dd className="font-medium">{groupId || "—"}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Member ID</dt>
              <dd className="font-medium">{memberId || "—"}</dd>
            </div>
          </dl>
          <div className="space-y-1.5">
            <Label htmlFor="displayName">Your name</Label>
            <Input id="displayName" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          {error ? <p className="text-destructive text-sm">{error}</p> : null}
          <Button className="w-full" disabled={busy} onClick={join}>
            {busy ? "Joining queue…" : "Wait for the credit officer"}
          </Button>
        </CardContent>
      </Card>
    </main>
  );
}
