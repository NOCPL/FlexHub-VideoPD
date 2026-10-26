import { useEffect, useRef } from "react";

/** Keeps a chat transcript scrolled to the newest message. */
export function useStickToBottom(stickKey: string) {
  const endRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const id = window.requestAnimationFrame(() => {
      const container = endRef.current?.parentElement;
      if (container) container.scrollTop = container.scrollHeight;
    });
    return () => window.cancelAnimationFrame(id);
  }, [stickKey]);
  return endRef;
}
