"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Bell, CalendarPlus, ListVideo, Users, Video } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useAuth } from "@/components/auth-provider";
import { useNotifications } from "@/components/notification-center";

export function AppHeader() {
  const { user, logout } = useAuth();
  const { notices, unreadCount, markRead, markAllRead } = useNotifications();
  const router = useRouter();

  return (
    <header className="border-b border-border/80 bg-[#10264e] text-white">
      <div className="mx-auto flex min-h-16 max-w-7xl items-center gap-4 px-4">
        <Link href="/dashboard" className="flex items-center gap-2 font-semibold tracking-tight">
          <span className="flex size-9 items-center justify-center rounded-lg bg-gradient-to-br from-orange-400 to-[#f7481c]">
            <Video className="size-4" />
          </span>
          <span>
            <span className="block">Flexhub Video PD</span>
            <span className="block text-[11px] font-normal text-[#a9bdd9]">Personal discussion room</span>
          </span>
        </Link>
        {user ? (
          <nav className="ml-3 hidden items-center gap-1 md:flex">
            {user.role === "Admin" ? (
              <Button variant="ghost" size="sm" asChild className="text-white hover:bg-white/10 hover:text-white">
                <Link href="/officers"><Users /> Officers</Link>
              </Button>
            ) : null}
            <Button variant="ghost" size="sm" asChild className="text-white hover:bg-white/10 hover:text-white">
              <Link href="/dashboard"><CalendarPlus /> Schedule</Link>
            </Button>
            <Button variant="ghost" size="sm" asChild className="text-white hover:bg-white/10 hover:text-white">
              <Link href="/video-pd"><ListVideo /> Video PD</Link>
            </Button>
          </nav>
        ) : null}
        <div className="flex-1" />
        {user ? (
          <div className="flex items-center gap-3 text-sm">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  className="relative text-white hover:bg-white/10 hover:text-white"
                  title={`${unreadCount} unread notifications`}
                >
                  <Bell />
                  {unreadCount > 0 ? (
                    <span className="absolute -right-1 -top-1 min-w-4 rounded-full bg-[#f7481c] px-1 text-center text-[10px] leading-4 text-white">
                      {unreadCount}
                    </span>
                  ) : null}
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-80 p-0">
                <div className="flex items-center justify-between px-3 py-2">
                  <DropdownMenuLabel className="p-0 text-[#10264e]">Notifications</DropdownMenuLabel>
                  {unreadCount > 0 ? (
                    <button
                      type="button"
                      className="text-xs font-medium text-[#29416f] hover:underline"
                      onClick={() => markAllRead()}
                    >
                      Mark all read
                    </button>
                  ) : null}
                </div>
                <DropdownMenuSeparator className="m-0" />
                {notices.length === 0 ? (
                  <p className="px-3 py-6 text-center text-sm text-[#5a6a84]">No notifications yet.</p>
                ) : (
                  <div className="max-h-80 overflow-y-auto">
                    {notices.map((notice) => (
                      <DropdownMenuItem
                        key={notice.id}
                        className={`items-start gap-2 rounded-none px-3 py-2 ${notice.read ? "" : "bg-[#fff7f2]"}`}
                        onSelect={() => {
                          markRead(notice.id);
                          router.push(notice.href);
                        }}
                      >
                        <span className="mt-1 size-2 shrink-0 rounded-full bg-[#f7481c]" style={{ visibility: notice.read ? "hidden" : "visible" }} />
                        <span className="min-w-0">
                          <span className="block font-medium text-[#10264e]">{notice.title}</span>
                          {notice.description ? (
                            <span className="mt-0.5 block truncate text-xs text-[#5a6a84]">{notice.description}</span>
                          ) : null}
                        </span>
                      </DropdownMenuItem>
                    ))}
                  </div>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
            <div className="hidden text-right sm:block">
              <div className="font-medium">{user.name}</div>
              <div className="text-xs text-[#a9bdd9]">
                {user.role === "Admin" ? "Admin" : "Credit officer"}
              </div>
            </div>
            <Button className="border-white/20 bg-white/10 text-white hover:bg-white/20" variant="outline" size="sm" onClick={() => { logout(); window.location.href = "/"; }}>
              Sign out
            </Button>
          </div>
        ) : null}
      </div>
    </header>
  );
}
