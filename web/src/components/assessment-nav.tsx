"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/components/auth-provider";

const LINKS = [
  { href: "/assessment", label: "Applications", match: (path: string) => path === "/assessment" || path.startsWith("/assessment/applications") },
  { href: "/assessment/guides", label: "Guides", match: (path: string) => path.startsWith("/assessment/guides") },
  { href: "/assessment/rules", label: "Deviation rules", match: (path: string) => path.startsWith("/assessment/rules") },
  { href: "/assessment/categories", label: "Loan categories", match: (path: string) => path.startsWith("/assessment/categories") },
];

export function AssessmentNav() {
  const path = usePathname();
  const { user } = useAuth();
  const links = user?.role === "Admin" ? LINKS : LINKS.filter((l) => l.href === "/assessment");
  return (
    <div className="mb-4 flex flex-wrap gap-2">
      {links.map((link) => {
        const active = link.match(path);
        return (
          <Link
            key={link.href}
            href={link.href}
            className={`rounded-full px-3 py-1 text-sm font-semibold ${active ? "bg-[#005792] text-white" : "border border-[#d7deea] bg-white text-[#29416f]"}`}
          >
            {link.label}
          </Link>
        );
      })}
    </div>
  );
}
