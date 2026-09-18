import type { Metadata } from "next";
import { Inter, Sora } from "next/font/google";
import { ThemeProvider } from "next-themes";
import { Toaster } from "@/components/ui/sonner";
import { AuthProvider } from "@/components/auth-provider";
import { NotificationProvider } from "@/components/notification-center";
import { NotificationListener } from "@/components/notification-listener";
import "./globals.css";

const sans = Inter({
  variable: "--font-sans",
  subsets: ["latin"],
});

const heading = Sora({
  variable: "--font-heading-family",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Flexhub Video PD",
  description: "Open meeting URLs for Angular credit officers and Kotlin field officers.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className={`${sans.variable} ${heading.variable} h-full`} suppressHydrationWarning>
      <body className="flex min-h-full flex-col bg-background text-foreground antialiased">
        <ThemeProvider attribute="class" defaultTheme="light" forcedTheme="light">
          <AuthProvider>
            <NotificationProvider>
              <NotificationListener />
              {children}
              <Toaster position="top-right" offset={72} richColors />
            </NotificationProvider>
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
