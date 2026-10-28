"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { AppHeader } from "@/components/app-header";
import { AssessmentNav } from "@/components/assessment-nav";
import { useAuth } from "@/components/auth-provider";
import { MetadataWorkspace, PhotosWorkspace, PlanWorkspace } from "@/components/engagement-panel";
import { api } from "@/lib/api";
import type { SessionDetail } from "@/lib/types";

export default function SessionReviewPage() {
  const { id } = useParams<{ id: string }>();
  const { user, loading } = useAuth();
  const router = useRouter();
  const [session, setSession] = useState<SessionDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<"plan" | "photos" | "meta">("plan");

  useEffect(() => {
    if (!loading && !user) router.replace("/");
  }, [loading, user, router]);

  useEffect(() => {
    if (!user || !id) return;
    api.session(id).then(setSession).catch((err) => setError(err.message));
  }, [user, id]);

  if (loading || !user) return <div className="min-h-full bg-[#f0f2f5] p-8 text-[#5a6a84]">Loading session…</div>;

  return (
    <div className="min-h-full bg-[#f0f2f5]">
      <AppHeader />
      <main className="mx-auto max-w-5xl space-y-4 px-4 py-6">
        <AssessmentNav />
        {session ? (
          <Link href={`/assessment/applications/${session.applicationId}`} className="text-sm text-[#29416f] underline">
            Back to {session.applicationCode}
          </Link>
        ) : (
          <Link href="/assessment" className="text-sm text-[#29416f] underline">Back to applications</Link>
        )}
        {error ? <p className="text-destructive">{error}</p> : null}
        {session ? (
          <>
            <div className="flex flex-wrap gap-1">
              {(["plan", "photos", "meta"] as const).map((id) => (
                <button
                  key={id}
                  type="button"
                  className={`rounded-full px-3 py-1 text-xs font-semibold ${tab === id ? "bg-[#005792] text-white" : "border border-[#d7deea] bg-white text-[#29416f]"}`}
                  onClick={() => setTab(id)}
                >
                  {id === "plan" ? "Plan" : id === "photos" ? "Photos" : "Metadata"}
                </button>
              ))}
            </div>
            <div className="flex min-h-[70vh] flex-col overflow-hidden rounded-xl border border-[#d7deea] bg-white">
              {tab === "plan" ? <PlanWorkspace session={session} onChange={setSession} /> : null}
              {tab === "photos" ? <PhotosWorkspace photos={session.photos} /> : null}
              {tab === "meta" ? <MetadataWorkspace session={session} /> : null}
            </div>
          </>
        ) : !error ? <p className="text-[#5a6a84]">Loading record…</p> : null}
      </main>
    </div>
  );
}
