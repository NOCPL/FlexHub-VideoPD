"use client";

import { useCallback, useState } from "react";
import Cropper, { type Area } from "react-easy-crop";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { api } from "@/lib/api";
import { getCroppedPng } from "@/lib/crop";

type Props = {
  meetingId: string;
  imageSrc: string | null;
  onClose: () => void;
  onSaved: () => void;
};

export function SnapshotCropDialog({ meetingId, imageSrc, onClose, onSaved }: Props) {
  const [crop, setCrop] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [area, setArea] = useState<Area | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onCropComplete = useCallback((_: Area, pixels: Area) => {
    setArea(pixels);
  }, []);

  async function save() {
    if (!imageSrc || !area) return;
    setSaving(true);
    setError(null);
    try {
      const blob = await getCroppedPng(imageSrc, area);
      await api.uploadSnapshot(meetingId, blob, area);
      onSaved();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not save the cropped still.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={Boolean(imageSrc)} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>Crop evidence still</DialogTitle>
          <DialogDescription>
            Frame the member or document from the live video. The cropped image is stored against this
            Video PD&apos;s bank, branch, group, and member IDs.
          </DialogDescription>
        </DialogHeader>
        <div className="relative h-80 overflow-hidden rounded-lg bg-black">
          {imageSrc ? (
            <Cropper
              image={imageSrc}
              crop={crop}
              zoom={zoom}
              aspect={3 / 4}
              onCropChange={setCrop}
              onZoomChange={setZoom}
              onCropComplete={onCropComplete}
            />
          ) : null}
        </div>
        <label className="flex items-center gap-3 text-sm">
          Zoom
          <input
            type="range"
            min={1}
            max={3}
            step={0.05}
            value={zoom}
            onChange={(e) => setZoom(Number(e.target.value))}
            className="flex-1"
          />
        </label>
        {error ? <p className="text-destructive text-sm">{error}</p> : null}
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving || !area}>
            {saving ? "Saving…" : "Save cropped still"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
