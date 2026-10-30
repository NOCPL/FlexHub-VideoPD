"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { AppHeader } from "@/components/app-header";
import { AssessmentNav } from "@/components/assessment-nav";
import { useAuth } from "@/components/auth-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { api } from "@/lib/api";
import type { TemplateListItem } from "@/lib/types";

export default function TemplatesPage() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const [templates, setTemplates] = useState<TemplateListItem[]>([]);

  useEffect(() => {
    if (!loading && (!user || user.role !== "Admin")) router.replace("/assessment");
  }, [loading, user, router]);

  useEffect(() => {
    if (user?.role !== "Admin") return;
    api.templates().then(setTemplates).catch((err) => toast.error(err.message));
  }, [user]);

  async function clone(id: string, name: string) {
    try {
      const copy = await api.cloneTemplate(id, { name: `${name} copy` });
      toast.success(`Version ${copy.version} created`);
      router.push(`/assessment/templates/${copy.id}`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not clone.");
    }
  }

  if (loading || !user) return <div className="min-h-full bg-[#f0f2f5] p-8 text-[#5a6a84]">Loading templates…</div>;

  return (
    <div className="min-h-full bg-[#f0f2f5]">
      <AppHeader />
      <main className="mx-auto max-w-7xl px-4 py-6">
        <AssessmentNav />
        <h1 className="mb-1 font-heading text-2xl font-semibold text-[#10264e]">CAM templates</h1>
        <p className="mb-4 text-sm text-[#5a6a84]">
          Output templates list the fields a Credit Assessment Memo must answer. Clone a template to start a new version. A memo keeps the version it was built on.
        </p>
        <div className="space-y-3">
          {templates.length === 0 ? (
            <Card className="border border-[#d7deea] bg-white">
              <CardContent className="py-8 text-sm text-[#5a6a84]">No output templates yet.</CardContent>
            </Card>
          ) : null}
          {templates.map((template) => (
            <Card key={template.id} className="border border-[#d7deea] bg-white">
              <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center">
                <div className="min-w-0 flex-1">
                  <div className="font-semibold text-[#10264e]">{template.name}</div>
                  <div className="text-sm text-[#5a6a84]">
                    {template.memoType} · {template.code} · v{template.version} · {template.sectionCount} sections · {template.fieldCount} fields
                  </div>
                </div>
                <Badge>{template.status}</Badge>
                <Button variant="outline" size="sm" asChild><Link href={`/assessment/templates/${template.id}`}>Edit</Link></Button>
                <Button variant="outline" size="sm" onClick={() => void clone(template.id, template.name)}>Clone / version</Button>
              </CardContent>
            </Card>
          ))}
        </div>
      </main>
    </div>
  );
}
