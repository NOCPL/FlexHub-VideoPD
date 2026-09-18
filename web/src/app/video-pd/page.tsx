"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Video } from "lucide-react";
import { AppHeader } from "@/components/app-header";
import { useAuth } from "@/components/auth-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { api } from "@/lib/api";
import type { MeetingListItem } from "@/lib/types";
import { STATUS_LABEL } from "@/lib/types";

export default function VideoPdPage() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const [records, setRecords] = useState<MeetingListItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!loading && !user) router.replace("/");
  }, [loading, user, router]);

  useEffect(() => {
    if (!user) return;
    api.meetings().then(setRecords).catch((err) => setError(err.message));
  }, [user]);

  if (loading || !user) return <div className="min-h-full bg-[#eef1f6] p-8 text-[#5a6a84]">Loading Video PD…</div>;

  return (
    <div className="min-h-full bg-[#eef1f6]">
      <AppHeader />
      <main className="mx-auto max-w-7xl px-4 py-6">
        <h1 className="font-heading text-2xl font-semibold text-[#10264e]">Video PD</h1>
        <p className="mb-5 text-sm text-[#5a6a84]">Scheduled, active and completed personal discussions.</p>
        {error ? <p className="text-destructive">{error}</p> : null}
        {records === null ? (
          <p className="text-[#5a6a84]">Loading records…</p>
        ) : records.length === 0 ? (
          <Card className="border border-[#d7deea] bg-white"><CardContent className="py-10 text-center text-[#5a6a84]">No Video PD records yet.</CardContent></Card>
        ) : (
          <div className="grid gap-3">
            {records.map((pd) => (
              <Card key={pd.id} className="border border-[#d7deea] bg-white">
                <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center">
                  <div className="flex size-11 items-center justify-center rounded-xl bg-[#fff1e4] text-[#f7481c]">
                    <Video className="size-5" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-semibold text-[#10264e]">{pd.memberName || pd.memberId || "Video PD"}</span>
                      <Badge variant="secondary">{STATUS_LABEL[pd.status] ?? pd.status}</Badge>
                    </div>
                    <div className="mt-1 text-sm text-[#5a6a84]">
                      {pd.bank || "—"} · {pd.branch || "—"} · {pd.groupId || "—"} · {pd.memberId || "—"}
                    </div>
                    <div className="text-xs text-[#5a6a84]">
                      {new Date(pd.scheduledAt).toLocaleString()} · {pd.creditOfficerName} · {pd.recordingSegments} segment(s)
                    </div>
                  </div>
                  <Button variant="outline" size="sm" asChild>
                    <Link href={`/video-pd/${pd.id}`}>View details</Link>
                  </Button>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}
