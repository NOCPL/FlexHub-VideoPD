"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { AppHeader } from "@/components/app-header";
import { useAuth } from "@/components/auth-provider";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { API_URL, api, getToken } from "@/lib/api";
import type { MeetingDetail } from "@/lib/types";
import { STATUS_LABEL } from "@/lib/types";

export default function VisitDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { user, loading } = useAuth();
  const [meeting, setMeeting] = useState<MeetingDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [images, setImages] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!loading && !user) router.replace("/");
  }, [loading, user, router]);

  useEffect(() => {
    if (!user || !params.id) return;
    api
      .meeting(params.id)
      .then(setMeeting)
      .catch((err) => setError(err instanceof Error ? err.message : "Visit not found."));
  }, [user, params.id]);

  useEffect(() => {
    if (!meeting) return;
    const token = getToken();
    let cancelled = false;
    const urls: string[] = [];
    Promise.all(
      meeting.snapshots.map(async (snap) => {
        const res = await fetch(`${API_URL}${snap.url}`, {
          headers: token ? { Authorization: `Bearer ${token}` } : undefined,
        });
        if (!res.ok) return [snap.id, ""] as const;
        const blob = await res.blob();
        const url = URL.createObjectURL(blob);
        urls.push(url);
        return [snap.id, url] as const;
      }),
    ).then((entries) => {
      if (!cancelled) setImages(Object.fromEntries(entries));
    });
    return () => {
      cancelled = true;
      urls.forEach((u) => URL.revokeObjectURL(u));
    };
  }, [meeting]);

  if (loading || !user) return <div className="p-8 text-muted-foreground">Loading visit…</div>;

  return (
    <div className="min-h-full bg-background">
      <AppHeader />
      <main className="mx-auto max-w-3xl space-y-4 px-4 py-6">
        {error ? <p className="text-destructive">{error}</p> : null}
        {!meeting && !error ? <p className="text-muted-foreground">Loading visit details…</p> : null}
        {meeting ? (
          <>
            <div>
              <a href="/dashboard" className="text-sm underline">
                Back to dashboard
              </a>
              <h1 className="mt-2 text-2xl font-semibold">{meeting.memberName || meeting.memberId || "Visit"}</h1>
              <p className="text-muted-foreground text-sm">
                {meeting.bank} · {meeting.groupId} · {meeting.memberId}
              </p>
              <Badge className="mt-2">{STATUS_LABEL[meeting.status] ?? meeting.status}</Badge>
            </div>
            <Card>
              <CardHeader>
                <CardTitle>Visit record</CardTitle>
                <CardDescription>
                  Recording segments are tagged with bank, group ID, and member ID from the field-officer link.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <p>Credit officer: {meeting.creditOfficer.name}</p>
                <p>Host URL: {meeting.hostUrl}</p>
                <p>Field URL: {meeting.fieldUrl}</p>
                <p>Scheduled: {new Date(meeting.scheduledAt).toLocaleString()}</p>
                <p>Duration: {meeting.durationSeconds != null ? `${meeting.durationSeconds} seconds` : "not ended"}</p>
                {meeting.recordings.length === 0 ? (
                  <p>Recording segments: none</p>
                ) : (
                  <ul className="space-y-1">
                    {meeting.recordings.map((r) => (
                      <li key={r.id}>
                        Seg {r.sequence}: {r.status}
                        {r.joinedAt ? ` · joined ${new Date(r.joinedAt).toLocaleTimeString()}` : ""}
                        {r.leftAt ? ` · left ${new Date(r.leftAt).toLocaleTimeString()}` : ""}
                        {r.durationSeconds != null ? ` · ${r.durationSeconds}s` : ""}
                        {r.filePath ? ` · ${r.filePath}` : ""}
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle>Cropped stills</CardTitle>
              </CardHeader>
              <CardContent>
                {meeting.snapshots.length === 0 ? (
                  <p className="text-muted-foreground text-sm">No stills were captured on this visit.</p>
                ) : (
                  <div className="grid gap-3 sm:grid-cols-2">
                    {meeting.snapshots.map((s) => (
                      <figure key={s.id} className="overflow-hidden rounded-lg border">
                        {images[s.id] ? (
                          // eslint-disable-next-line @next/next/no-img-element
                          <img src={images[s.id]} alt="Cropped visit still" className="w-full" />
                        ) : (
                          <div className="bg-muted h-40" />
                        )}
                        <figcaption className="text-muted-foreground p-2 text-xs">
                          {s.bank} / {s.groupId} / {s.memberId} · {new Date(s.createdAt).toLocaleString()}
                        </figcaption>
                      </figure>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle>Chat</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                {meeting.chat.length === 0 ? (
                  <p className="text-muted-foreground">No messages were stored for this visit.</p>
                ) : (
                  meeting.chat.map((m) => (
                    <p key={m.id}>
                      <span className="font-medium">{m.senderName}:</span> {m.body}
                    </p>
                  ))
                )}
              </CardContent>
            </Card>
          </>
        ) : null}
      </main>
    </div>
  );
}
