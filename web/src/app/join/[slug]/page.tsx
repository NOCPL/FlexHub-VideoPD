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
import { useFieldOfficerGeotag } from "@/hooks/use-field-officer-geotag";
import type { JoinTokenResponse, LobbyMessage, User, WaitingOfficer } from "@/lib/types";

export default function JoinPage() {
  return (
    <Suspense fallback={<div className="min-h-full bg-[#eef1f6] p-8 text-[#5a6a84]">Loading join link…</div>}>
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
  const branch = search.get("branch") ?? "";
  const groupId = search.get("groupId") ?? "";
  const memberId = search.get("memberId") ?? "";
  const [name, setName] = useState("Field officer");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [creditOfficerName, setCreditOfficerName] = useState("the credit officer");
  const [waiting, setWaiting] = useState<WaitingOfficer | null>(null);
  const [messages, setMessages] = useState<LobbyMessage[]>([]);
  const [chatTarget, setChatTarget] = useState<"all" | "private">("all");
  const [denied, setDenied] = useState(false);
  const [draft, setDraft] = useState("");
  const [session, setSession] = useState<JoinTokenResponse | null>(null);
  const [guest, setGuest] = useState<User | null>(null);
  const [callEnded, setCallEnded] = useState(false);
  const connectingRef = useRef(false);
  const inCallRef = useRef(false);
  const endedRef = useRef(false);
  const { label: gpsLabel, status: gpsStatus } = useFieldOfficerGeotag(waiting?.id ?? null);

  useEffect(() => {
    connectingRef.current = false;
    setWaiting(null);
    setSession(null);
    setGuest(null);
    setMessages([]);
    setDenied(false);
    setCallEnded(false);
    setGuestToken(null);
    setError(null);
    inCallRef.current = false;
    endedRef.current = false;
  }, [slug, bank, branch, groupId, memberId]);

  async function join() {
    setBusy(true);
    setError(null);
    try {
      const result = await api.join({ slug, bank, branch, groupId, memberId, displayName: name });
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
    if (!waiting) return;
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

    connection.on("lobbyChat", (message: LobbyMessage) => {
      setMessages((current) => current.some((m) => m.id === message.id) ? current : [...current, message]);
    });
    connection.on("admitted", (joinSession: JoinTokenResponse) => {
      if (!isLiveKitSession(joinSession)) return;
      connectingRef.current = true;
      inCallRef.current = true;
      setGuestToken(joinSession.guestToken);
      setSession(joinSession);
    });
    connection.on("denied", () => {
      setDenied(true);
      setGuestToken(null);
      setSession(null);
    });
    connection.on("meetingEnded", () => {
      endedRef.current = true;
      inCallRef.current = false;
      setCallEnded(true);
      setSession(null);
      setGuestToken(null);
    });

    const connect = window.setTimeout(() => {
      if (cancelled) return;
      connection.start().catch((err: Error) => {
        if (cancelled) return;
        if ((err?.message ?? "").includes("stopped during negotiation")) return;
      });
    }, 50);

    const poll = window.setInterval(() => {
      api
        .waiting(waiting.id)
        .then(async (latest) => {
          setWaiting(latest.waiting);
          setMessages(latest.chat ?? []);
          if (latest.waiting.status === "Denied") {
            setDenied(true);
            setGuestToken(null);
            setSession(null);
          }
          if (latest.waiting.status === "Left" && inCallRef.current) {
            endedRef.current = true;
            setCallEnded(true);
            setSession(null);
            setGuestToken(null);
            inCallRef.current = false;
          }
          if (latest.waiting.status === "Admitted" && !cancelled && !connectingRef.current) {
            connectingRef.current = true;
            inCallRef.current = true;
            const joinSession = await api.waitingConnect(waiting.id);
            setGuestToken(joinSession.guestToken);
            setSession(joinSession);
          }
        })
        .catch(() => undefined);
    }, 3000);

    return () => {
      cancelled = true;
      window.clearTimeout(connect);
      window.clearInterval(poll);
      void connection.stop();
    };
  }, [waiting?.id]);

  async function sendChat(e: FormEvent) {
    e.preventDefault();
    if (!waiting) return;
    const body = draft.trim();
    if (!body) return;
    setDraft("");
    try {
      const saved = await api.waitingChat(
        waiting.id,
        body,
        chatTarget === "all" ? null : waiting.id,
      );
      setMessages((current) => current.some((m) => m.id === saved.id) ? current : [...current, saved]);
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
      `/join/${slug}?bank=${encodeURIComponent(bank)}&branch=${encodeURIComponent(branch)}&groupId=${encodeURIComponent(groupId)}&memberId=${encodeURIComponent(memberId)}`,
    );
  }

  if (callEnded) {
    return (
      <main className="flex min-h-full items-center justify-center bg-[#eef1f6] p-4">
        <Card className="max-w-md border border-[#d7deea] bg-white text-center">
          <CardHeader>
            <CardTitle className="text-[#10264e]">This Video PD has ended</CardTitle>
            <CardDescription className="text-[#5a6a84]">
              The credit officer ended the call. You can close this page.
            </CardDescription>
          </CardHeader>
        </Card>
      </main>
    );
  }

  if (session && guest && session.meeting && session.token) {
    return (
      <div className="flex h-dvh flex-col">
        <MeetingSession
          token={session.token}
          serverUrl={session.liveKitUrl}
          meeting={session.meeting}
          user={guest}
          gpsLabel={gpsLabel}
          onLeave={() => {
            setGuestToken(null);
            setSession(null);
            inCallRef.current = false;
            if (endedRef.current) return;
            setWaiting(null);
            router.push(
              `/join/${slug}?bank=${encodeURIComponent(bank)}&branch=${encodeURIComponent(branch)}&groupId=${encodeURIComponent(groupId)}&memberId=${encodeURIComponent(memberId)}`,
            );
          }}
        />
      </div>
    );
  }

  if (denied) {
    return (
      <main className="flex min-h-full items-center justify-center bg-[#eef1f6] p-4">
        <Card className="max-w-md border border-[#d7deea] bg-white text-center">
          <CardHeader>
            <CardTitle className="text-[#10264e]">The credit officer declined this Video PD</CardTitle>
            <CardDescription className="text-[#5a6a84]">
              You were not admitted to the call. Ask your team for a new join link if you still need to complete Video PD.
            </CardDescription>
          </CardHeader>
        </Card>
      </main>
    );
  }

  if (waiting) {
    const visibleMessages = (messages ?? []).filter((message) =>
      chatTarget === "all"
        ? message.recipientWaitingOfficerId === null
        : message.recipientWaitingOfficerId === waiting.id);
    return (
      <main className="min-h-full bg-[#eef1f6]">
        <div className="mx-auto grid max-w-3xl gap-4 px-4 py-10 md:grid-cols-[1fr_280px]">
        <Card className="border border-[#d7deea] bg-white">
          <CardHeader>
            <CardTitle className="text-[#10264e]">Waiting for {creditOfficerName}</CardTitle>
            <CardDescription className="text-[#5a6a84]">
              You are not in the call yet. Chat here until the credit officer admits you, then speak
              on the call.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex h-[28rem] flex-col">
            <div className="min-h-0 flex-1 space-y-3 overflow-y-auto text-sm">
              <div className="mb-3 flex gap-2">
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  className={chatTarget === "all" ? "border-transparent bg-[#10264e] text-white hover:bg-[#29416f] hover:text-white" : ""}
                  onClick={() => setChatTarget("all")}
                >
                  Everyone
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  className={chatTarget === "private" ? "border-transparent bg-[#10264e] text-white hover:bg-[#29416f] hover:text-white" : ""}
                  onClick={() => setChatTarget("private")}
                >
                  Credit officer
                </Button>
              </div>
              {visibleMessages.length === 0 ? (
                <p className="text-[#5a6a84]">No messages yet.</p>
              ) : (
                visibleMessages.map((m) => (
                  <div key={m.id}>
                    <div className="text-xs text-[#5a6a84]">
                      {m.senderName} · {m.senderRole === "FieldGuest" ? "You" : "Credit"}
                    </div>
                    <div className="text-[#10264e]">{m.body}</div>
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
              <Button type="submit" className="bg-[#f7481c] text-white hover:bg-[#d63a11]">Send</Button>
            </form>
          </CardContent>
        </Card>
        <Card className="h-fit border border-[#d7deea] bg-white">
          <CardHeader>
            <CardTitle className="text-base text-[#10264e]">Your Video PD tags</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm text-[#29416f]">
            <p>Bank: {waiting.bank || bank || "—"}</p>
            <p>Branch: {waiting.branch || branch || "—"}</p>
            <p>Group: {waiting.groupId || groupId || "—"}</p>
            <p>Member: {waiting.memberId || memberId || "—"}</p>
            <p className={gpsStatus === "error" ? "text-destructive" : ""}>
              GPS: {gpsStatus === "locating" ? "Allow location on this phone…" : gpsLabel}
            </p>
            <Button variant="outline" className="mt-2 w-full" onClick={leaveWait}>
              Leave queue
            </Button>
          </CardContent>
        </Card>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-full bg-[#eef1f6]">
      <div className="mx-auto max-w-lg px-4 py-10">
      <Card className="border border-[#d7deea] bg-white">
        <CardHeader>
          <CardTitle className="text-[#10264e]">Join Video PD</CardTitle>
          <CardDescription className="text-[#5a6a84]">
            You will wait until the credit officer admits you. This phone’s GPS and time are attached to
            captured stills. Bank, branch, group, and member IDs are stored as-is. They are not validated here.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <dl className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
            <div>
              <dt className="text-[#5a6a84]">Bank</dt>
              <dd className="font-medium text-[#10264e]">{bank || "—"}</dd>
            </div>
            <div>
              <dt className="text-[#5a6a84]">Branch</dt>
              <dd className="font-medium text-[#10264e]">{branch || "—"}</dd>
            </div>
            <div>
              <dt className="text-[#5a6a84]">Group ID</dt>
              <dd className="font-medium text-[#10264e]">{groupId || "—"}</dd>
            </div>
            <div>
              <dt className="text-[#5a6a84]">Member ID</dt>
              <dd className="font-medium text-[#10264e]">{memberId || "—"}</dd>
            </div>
          </dl>
          <div className="space-y-1.5">
            <Label htmlFor="displayName">Your name</Label>
            <Input id="displayName" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          {error ? <p className="text-destructive text-sm">{error}</p> : null}
          <Button className="w-full bg-[#f7481c] text-white hover:bg-[#d63a11]" disabled={busy} onClick={join}>
            {busy ? "Joining queue…" : "Wait for the credit officer"}
          </Button>
        </CardContent>
      </Card>
      </div>
    </main>
  );
}

function isLiveKitSession(value: JoinTokenResponse | string | null | undefined): value is JoinTokenResponse {
  return Boolean(
    value &&
      typeof value === "object" &&
      typeof value.token === "string" &&
      value.token.length > 0 &&
      value.meeting,
  );
}
