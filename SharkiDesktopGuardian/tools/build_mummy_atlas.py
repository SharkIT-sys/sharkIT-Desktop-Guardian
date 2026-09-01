#!/usr/bin/env python3
"""Convierte la hoja JPEG de Mummy al atlas transparente 8x15 de la app."""

from __future__ import annotations

import argparse
from collections import deque
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


CELL_WIDTH = 192
CELL_HEIGHT = 208
COLUMNS = 8
ROWS = 15
ATLAS_SIZE = (CELL_WIDTH * COLUMNS, CELL_HEIGHT * ROWS)
BACKGROUND_THRESHOLD = 28
COMMON_SCALE = 2.8
ROW_BOUNDARIES = [0, 68, 136, 205, 273, 341, 409, 478, 546, 614, 683, 752, 820, 889, 956, 1024]
ROW_SPECS = [
    ("idle", 7, 125, True),
    ("moving-right", 8, 125, True),
    ("moving-left", 8, 125, True),
    ("greeting", 4, 125, True),
    ("jumping", 5, 125, True),
    ("failed", 8, 125, True),
    ("waiting", 6, 160, True),
    ("running", 6, 125, True),
    ("review", 6, 125, True),
    ("look-upper-unused", 8, 125, False),
    ("look-lower-unused", 8, 125, False),
    ("high-load", 8, 125, True),
    ("low-disk", 6, 125, True),
    ("high-memory", 6, 125, True),
    ("thermal-alert", 5, 125, True),
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--qa-dir", required=True, type=Path)
    return parser.parse_args()


def connected_background(slot_rgb: np.ndarray) -> np.ndarray:
    """Marca solo el fondo oscuro conectado al perímetro de una celda."""
    candidate = np.max(slot_rgb, axis=2) <= BACKGROUND_THRESHOLD
    height, width = candidate.shape
    background = np.zeros(candidate.shape, dtype=bool)
    queue: deque[tuple[int, int]] = deque()

    for x in range(width):
        queue.append((0, x))
        queue.append((height - 1, x))
    for y in range(height):
        queue.append((y, 0))
        queue.append((y, width - 1))

    while queue:
        y, x = queue.popleft()
        if y < 0 or y >= height or x < 0 or x >= width or background[y, x] or not candidate[y, x]:
            continue
        background[y, x] = True
        queue.extend(((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)))

    return background


def make_transparent_source(source: Image.Image) -> Image.Image:
    rgb = np.asarray(source.convert("RGB"), dtype=np.uint8)
    alpha = np.zeros(rgb.shape[:2], dtype=np.uint8)

    for row, (_, _, _, included) in enumerate(ROW_SPECS):
        if not included:
            continue
        y0 = ROW_BOUNDARIES[row]
        y1 = ROW_BOUNDARIES[row + 1]
        for column in range(COLUMNS):
            x0 = column * 63
            x1 = (column + 1) * 63
            foreground = ~connected_background(rgb[y0:y1, x0:x1])
            slot = Image.fromarray(foreground.astype(np.uint8) * 255, "L")
            # Un píxel de dilatación conserva gafas, contornos y otros detalles oscuros
            # sin unir personajes de celdas adyacentes.
            slot = slot.filter(ImageFilter.MaxFilter(3))
            alpha[y0:y1, x0:x1] = np.asarray(slot, dtype=np.uint8)

    rgba = np.dstack((rgb, alpha))
    rgba[alpha == 0, :3] = 0
    return Image.fromarray(rgba, "RGBA")


def compose_atlas(image: Image.Image) -> Image.Image:
    """Escala todas las celdas con un factor común y añade margen de seguridad."""
    atlas = Image.new("RGBA", ATLAS_SIZE, (0, 0, 0, 0))
    for row, (_, _, _, included) in enumerate(ROW_SPECS):
        if not included:
            continue
        y0 = ROW_BOUNDARIES[row]
        y1 = ROW_BOUNDARIES[row + 1]
        for column in range(COLUMNS):
            x0 = column * 63
            x1 = (column + 1) * 63
            slot = image.crop((x0, y0, x1, y1)).convert("RGBa")
            size = (round(slot.width * COMMON_SCALE), round(slot.height * COMMON_SCALE))
            slot = slot.resize(size, Image.Resampling.LANCZOS).convert("RGBA")
            left = column * CELL_WIDTH + (CELL_WIDTH - slot.width) // 2
            top = row * CELL_HEIGHT + (CELL_HEIGHT - slot.height) // 2
            atlas.alpha_composite(slot, (left, top))
    return atlas


def alpha_bbox(cell: Image.Image) -> tuple[int, int, int, int] | None:
    alpha = cell.getchannel("A").point(lambda value: 255 if value > 8 else 0)
    return alpha.getbbox()


def checkerboard(size: tuple[int, int], tile: int = 12) -> Image.Image:
    image = Image.new("RGB", size, "#B8BEC7")
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            if (x // tile + y // tile) % 2:
                draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill="#E3E7EC")
    return image


def render_contact_sheet(atlas: Image.Image, output: Path) -> None:
    scale = 0.25
    preview = atlas.resize((round(atlas.width * scale), round(atlas.height * scale)), Image.Resampling.LANCZOS)
    margin_left = 112
    canvas = checkerboard((margin_left + preview.width + 16, preview.height + 32), 10)
    canvas.paste(preview, (margin_left, 16), preview)
    draw = ImageDraw.Draw(canvas)
    for row, (name, _, _, included) in enumerate(ROW_SPECS):
        y = 16 + round((row + 0.5) * CELL_HEIGHT * scale)
        suffix = " (reservada)" if not included else ""
        draw.text((8, y - 6), f"{row:02d} {name}{suffix}", fill="#111820")
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output)


def render_previews(atlas: Image.Image, output_dir: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    for row, (name, frame_count, duration, included) in enumerate(ROW_SPECS):
        if not included:
            continue
        frames: list[Image.Image] = []
        for frame in range(frame_count):
            cell = atlas.crop(
                (
                    frame * CELL_WIDTH,
                    row * CELL_HEIGHT,
                    (frame + 1) * CELL_WIDTH,
                    (row + 1) * CELL_HEIGHT,
                )
            )
            background = checkerboard(cell.size, 12)
            background.paste(cell, (0, 0), cell)
            frames.append(background)
        frames[0].save(
            output_dir / f"{name}.gif",
            save_all=True,
            append_images=frames[1:],
            duration=duration,
            loop=0,
            disposal=2,
        )


def validate(atlas: Image.Image, source_path: Path) -> dict[str, object]:
    failures: list[str] = []
    frames: dict[str, list[dict[str, object]]] = {}

    if atlas.size != ATLAS_SIZE:
        failures.append(f"Dimensiones incorrectas: {atlas.size}; esperado {ATLAS_SIZE}.")
    if atlas.mode != "RGBA":
        failures.append(f"Modo incorrecto: {atlas.mode}; esperado RGBA.")

    for row, (name, frame_count, _, included) in enumerate(ROW_SPECS):
        if not included:
            for frame in range(COLUMNS):
                cell = atlas.crop(
                    (
                        frame * CELL_WIDTH,
                        row * CELL_HEIGHT,
                        (frame + 1) * CELL_WIDTH,
                        (row + 1) * CELL_HEIGHT,
                    )
                )
                if np.count_nonzero(np.asarray(cell.getchannel("A")) > 8):
                    failures.append(f"{name}[{frame}] debería estar vacío.")
            frames[name] = []
            continue
        frame_results: list[dict[str, object]] = []
        for frame in range(frame_count):
            cell = atlas.crop(
                (
                    frame * CELL_WIDTH,
                    row * CELL_HEIGHT,
                    (frame + 1) * CELL_WIDTH,
                    (row + 1) * CELL_HEIGHT,
                )
            )
            bbox = alpha_bbox(cell)
            opaque_pixels = int(np.count_nonzero(np.asarray(cell.getchannel("A")) > 8))
            touches_edge = bool(
                bbox
                and (bbox[0] <= 1 or bbox[1] <= 1 or bbox[2] >= CELL_WIDTH - 1 or bbox[3] >= CELL_HEIGHT - 1)
            )
            if bbox is None or opaque_pixels < 64:
                failures.append(f"{name}[{frame}] está vacío o incompleto.")
            if touches_edge:
                failures.append(f"{name}[{frame}] toca el borde de su celda.")
            frame_results.append(
                {
                    "frame": frame,
                    "bbox": list(bbox) if bbox else None,
                    "visiblePixels": opaque_pixels,
                    "touchesEdge": touches_edge,
                }
            )
        frames[name] = frame_results

        for frame in range(frame_count, COLUMNS):
            cell = atlas.crop(
                (
                    frame * CELL_WIDTH,
                    row * CELL_HEIGHT,
                    (frame + 1) * CELL_WIDTH,
                    (row + 1) * CELL_HEIGHT,
                )
            )
            if np.count_nonzero(np.asarray(cell.getchannel("A")) > 8):
                failures.append(f"{name}[{frame}] debería estar vacío.")

    return {
        "ok": not failures,
        "source": str(source_path.resolve()),
        "sourceSha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
        "atlasSize": list(atlas.size),
        "cellSize": [CELL_WIDTH, CELL_HEIGHT],
        "grid": [COLUMNS, ROWS],
        "backgroundThreshold": BACKGROUND_THRESHOLD,
        "commonScale": COMMON_SCALE,
        "maskMethod": "per-slot perimeter flood fill",
        "rowFrameCounts": [spec[1] for spec in ROW_SPECS],
        "includedRows": [spec[3] for spec in ROW_SPECS],
        "rowBoundaries": ROW_BOUNDARIES,
        "maskDilationPixelsAtSourceScale": 1,
        "frames": frames,
        "failures": failures,
    }


def main() -> int:
    args = parse_args()
    source = Image.open(args.source).convert("RGB")
    if source.size != (504, 1024):
        raise SystemExit(f"La fuente debe medir 504x1024; mide {source.width}x{source.height}.")

    atlas = compose_atlas(make_transparent_source(source))
    report = validate(atlas, args.source)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.qa_dir.mkdir(parents=True, exist_ok=True)
    atlas.save(args.output, optimize=True)
    render_contact_sheet(atlas, args.qa_dir / "contact-sheet.png")
    render_previews(atlas, args.qa_dir / "previews")
    (args.qa_dir / "validation.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    print(json.dumps({"ok": report["ok"], "output": str(args.output), "failures": report["failures"]}, ensure_ascii=False))
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
