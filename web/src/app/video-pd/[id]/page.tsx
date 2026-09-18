"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { AppHeader } from "@/components/app-header";
import { useAuth } from "@/components/auth-provider";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";
import type { MeetingDetail } from "@/lib/types";
import { STATUS_LABEL } from "@/lib/types";

export default function VideoPdDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { user, loading } = useAuth();
  const [pd, setPd] = useState<MeetingDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!loading && !user) router.replace("/");
  }, [loading, user, router]);

  useEffect(() => {
    if (!user || !id) return;
    api.meeting(id).then(setPd).catch((err) => setError(err.message));
  }, [user, id]);

  if (loading || !user) return <div className="min-h-full bg-[#eef1f6] p-8 text-[#5a6a84]">Loading Video PD…</div>;

  return (
    <div className="min-h-full bg-[#eef1f6]">
      <AppHeader />
      <main className="mx-auto max-w-4xl space-y-4 px-4 py-6">
        <Link href="/video-pd" className="text-sm text-[#29416f] underline">Back to Video PD</Link>
        {error ? <p className="text-destructive">{error}</p> : null}
        {pd ? (
          <>
            <div>
              <h1 className="mt-2 font-heading text-2xl font-semibold text-[#10264e]">
                {pd.memberName || pd.memberId || "Video PD"}
              </h1>
              <p className="text-sm text-[#5a6a84]">
                {pd.bank || "—"} · {pd.branch || "—"} · {pd.groupId || "—"} · {pd.memberId || "—"}
              </p>
              <Badge className="mt-2">{STATUS_LABEL[pd.status] ?? pd.status}</Badge>
            </div>
            <Card className="border border-[#d7deea] bg-white">
              <CardHeader>
                <CardTitle className="text-[#10264e]">Video PD record</CardTitle>
                <CardDescription className="text-[#5a6a84]">Recordings are tagged Bank → Branch → Group → Member.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <p>Credit officer: {pd.creditOfficer.name}</p>
                <p>Scheduled: {new Date(pd.scheduledAt).toLocaleString()}</p>
                <p>Duration: {pd.durationSeconds != null ? `${pd.durationSeconds} seconds` : "Not completed"}</p>
                <p className="break-all">Host URL: {pd.hostUrl}</p>
                <p className="break-all">Field URL: {pd.fieldUrl}</p>
              </CardContent>
            </Card>
            <Card className="border border-[#d7deea] bg-white">
              <CardHeader><CardTitle className="text-[#10264e]">Recording segments</CardTitle></CardHeader>
              <CardContent>
                {pd.recordings.length === 0 ? (
                  <p className="text-sm text-[#5a6a84]">No recording segments.</p>
                ) : (
                  <div className="space-y-3">
                    {pd.recordings.map((segment) => (
                      <div key={segment.id} className="rounded-lg border p-3 text-sm">
                        <div className="font-medium">Segment {segment.sequence} · {segment.status}</div>
                        <div className="text-[#5a6a84]">
                          {segment.bank} · {segment.branch} · {segment.groupId} · {segment.memberId}
                        </div>
                        <div className="text-xs text-[#5a6a84]">
                          Duration {segment.durationSeconds ?? 0}s
                        </div>
                        {segment.filePath ? <div className="mt-1 break-all text-xs">{segment.filePath}</div> : null}
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          </>
        ) : !error ? <p className="text-[#5a6a84]">Loading record…</p> : null}
      </main>
    </div>
  );
}
