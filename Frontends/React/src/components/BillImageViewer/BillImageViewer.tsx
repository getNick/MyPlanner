import React, { useEffect, useRef, useState } from "react";
import { ZoomIn, ZoomOut } from "lucide-react";

interface BillImageViewerProps {
  src: string;
  alt: string;
}

export default function BillImageViewer({ src, alt }: BillImageViewerProps) {
  const [zoomed, setZoomed] = useState(false);
  const [hoverZoom, setHoverZoom] = useState(1.5);
  const [pointer, setPointer] = useState<{ x: number; y: number } | null>(null);
  const previewRef = useRef<HTMLDivElement>(null);
  const imageRef = useRef<HTMLImageElement>(null);

  useEffect(() => {
    setZoomed(false);
    setPointer(null);
    if (previewRef.current) {
      previewRef.current.scrollLeft = 0;
      previewRef.current.scrollTop = 0;
    }
  }, [src]);

  useEffect(() => {
    const preview = previewRef.current;
    if (!preview) return;
    const adjustHoverZoom = (event: WheelEvent) => {
      if (zoomed || !pointer) return;
      event.preventDefault();
      setHoverZoom(current => Math.min(3, Math.max(1, current + (event.deltaY < 0 ? 0.25 : -0.25))));
    };
    preview.addEventListener("wheel", adjustHoverZoom, { passive: false });
    return () => preview.removeEventListener("wheel", adjustHoverZoom);
  }, [pointer, zoomed]);

  const trackPointer = (event: React.PointerEvent<HTMLDivElement>) => {
    if (event.pointerType === "touch" || zoomed) return;
    const bounds = imageRef.current?.getBoundingClientRect();
    if (!bounds || !bounds.width || !bounds.height) return;
    setPointer({
      x: Math.min(1, Math.max(0, (event.clientX - bounds.left) / bounds.width)),
      y: Math.min(1, Math.max(0, (event.clientY - bounds.top) / bounds.height)),
    });
  };

  return (
    <div className="space-y-2">
      <button
        type="button"
        aria-label={zoomed ? "Zoom out" : "Zoom in"}
        aria-pressed={zoomed}
        onClick={() => { setZoomed(value => !value); setPointer(null); }}
        className="inline-flex items-center gap-1 border border-slate-300 bg-white px-2 py-1 text-xs text-slate-700 hover:bg-slate-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-600"
      >
        {zoomed ? <ZoomOut aria-hidden="true" className="h-3.5 w-3.5" /> : <ZoomIn aria-hidden="true" className="h-3.5 w-3.5" />}
        {zoomed ? "Zoom out" : "Zoom in"}
      </button>
      <div
        ref={previewRef}
        aria-label={zoomed ? "Zoomed Bill Image; scroll to inspect" : `Bill Image preview; hover magnification ${hoverZoom.toFixed(2)} times; scroll to adjust`}
        className={`relative mx-auto w-full overflow-auto rounded-sm border border-slate-200 bg-slate-50 p-1 ${zoomed ? "max-h-64 touch-pan-x touch-pan-y" : "flex max-h-64 items-center justify-center"}`}
        tabIndex={zoomed ? 0 : undefined}
        onPointerMove={trackPointer}
        onPointerLeave={() => setPointer(null)}
      >
        <img
          ref={imageRef}
          src={src}
          alt={alt}
          draggable={false}
          className={zoomed ? "block h-auto w-[200%] max-w-none object-contain" : "block max-h-60 max-w-full object-contain"}
        />
        {!zoomed && pointer && (
          <div
            aria-hidden="true"
            className="pointer-events-none absolute inset-0 border border-white/70"
            style={{
              backgroundImage: `url("${src}")`,
              backgroundRepeat: "no-repeat",
              backgroundSize: `${hoverZoom * 100}% auto`,
              backgroundPosition: `${pointer.x * 100}% ${pointer.y * 100}%`,
            }}
          />
        )}
      </div>
    </div>
  );
}
