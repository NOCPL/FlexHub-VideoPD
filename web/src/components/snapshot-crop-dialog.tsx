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
import { geotagSummary, type FieldGeotag } from "@/lib/geotag";

type Props = {
  meetingId: string;
  fieldOfficerId?: string | null;
  geotag?: FieldGeotag | null;
  imageSrc: string | null;
  onClose: () => void;
  onSaved: () => void;
};

export function SnapshotCropDialog({
  meetingId,
  fieldOfficerId,
  geotag,
  imageSrc,
  onClose,
  onSaved,
}: Props) {
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
      let stamp: FieldGeotag | null | undefined = geotag;
      if (fieldOfficerId) {
        try {
          const room = await api.waiting(fieldOfficerId);
          stamp = room.waiting;
        } catch {
          /* use the last geotag from the lobby if refresh fails */
        }
      }
      const blob = await getCroppedPng(imageSrc, area, stamp);
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
            The field officer’s phone GPS and time are printed on the saved image.
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
        <p className="text-sm text-[#29416f]">{geotagSummary(geotag)}</p>
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
