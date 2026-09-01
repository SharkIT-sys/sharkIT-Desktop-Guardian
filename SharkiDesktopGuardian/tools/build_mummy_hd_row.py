#!/usr/bin/env python3
"""Convierte una tira de Mummy de alta definición con fondo verde en una fila del atlas."""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image


CELL_WIDTH, CELL_HEIGHT = 192, 208
MAX_WIDTH, MAX_HEIGHT = 184, 190


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--atlas", required=True, type=Path)
    parser.add_argument("--row", required=True, type=int)
    parser.add_argument("--frames", required=True, type=int)
    parser.add_argument("--source-row", type=int)
    parser.add_argument("--source-total-rows", type=int)
    parser.add_argument("--source-columns", type=int)
    parser.add_argument("--allow-upscale", action="store_true")
    return parser.parse_args()


def cutout(slot: Image.Image) -> Image.Image:
    rgba_source = np.asarray(slot.convert("RGBA"), dtype=np.uint8)
    rgb = rgba_source[:, :, :3].copy()
    source_alpha = rgba_source[:, :, 3]
    green_strength = rgb[:, :, 1].astype(np.int16) - np.maximum(rgb[:, :, 0], rgb[:, :, 2]).astype(np.int16)
    neon_red = (rgb[:, :, 0] > 220) & (rgb[:, :, 1] < 90) & (rgb[:, :, 2] < 90)
    neon_yellow = (rgb[:, :, 0] > 230) & (rgb[:, :, 1] > 220) & (rgb[:, :, 2] < 60)
    candidate = (source_alpha < 128) | (green_strength > 10) | (np.max(rgb, axis=2) < 70) | neon_red | neon_yellow
    background = np.zeros(candidate.shape, dtype=bool)
    queue: deque[tuple[int, int]] = deque()
    height, width = candidate.shape
    for x in range(width):
        queue.extend(((0, x), (height - 1, x)))
    for y in range(height):
        queue.extend(((y, 0), (y, width - 1)))
    while queue:
        y, x = queue.popleft()
        if y < 0 or y >= height or x < 0 or x >= width or background[y, x] or not candidate[y, x]:
            continue
        background[y, x] = True
        queue.extend(((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)))
    alpha = ((~background) & (source_alpha > 0)).astype(np.uint8) * 255
    spill = green_strength > 0
    rgb[spill, 1] = np.minimum(rgb[spill, 1], np.maximum(rgb[spill, 0], rgb[spill, 2]))
    rgba = np.dstack((rgb, alpha))
    rgba[alpha == 0, :3] = 0
    image = Image.fromarray(rgba, "RGBA")
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Una celda generada quedó vacía.")
    return image.crop(bbox)


def main() -> int:
    args = parse_args()
    if args.row < 0 or args.row >= 15 or args.frames < 1 or args.frames > 8:
        raise SystemExit("Fila o número de fotogramas inválido.")
    source = Image.open(args.source).convert("RGB")
    if args.source_row is not None or args.source_total_rows is not None:
        if args.source_row is None or args.source_total_rows is None or not 0 <= args.source_row < args.source_total_rows:
            raise SystemExit("La fila de origen y el número total de filas deben ser válidos.")
        top = round(args.source_row * source.height / args.source_total_rows)
        bottom = round((args.source_row + 1) * source.height / args.source_total_rows)
        source = source.crop((0, top, source.width, bottom))

    source_columns = args.source_columns or args.frames
    if source_columns < args.frames:
        raise SystemExit("La cuadrícula de origen no tiene suficientes columnas para los fotogramas.")
    atlas = Image.open(args.atlas).convert("RGBA")
    if atlas.size != (1536, 3120):
        raise SystemExit(f"El atlas debe medir 1536x3120; mide {atlas.size}.")

    poses = [
        cutout(source.crop((
            round(index * source.width / source_columns),
            0,
            round((index + 1) * source.width / source_columns),
            source.height)))
        for index in range(args.frames)
    ]
    scale = min(MAX_WIDTH / max(pose.width for pose in poses), MAX_HEIGHT / max(pose.height for pose in poses))
    if not args.allow_upscale:
        scale = min(scale, 1.0)

    for frame in range(8):
        atlas.paste((0, 0, 0, 0), (frame * CELL_WIDTH, args.row * CELL_HEIGHT, (frame + 1) * CELL_WIDTH, (args.row + 1) * CELL_HEIGHT))
    for frame, pose in enumerate(poses):
        size = (round(pose.width * scale), round(pose.height * scale))
        pose = pose.resize(size, Image.Resampling.LANCZOS)
        atlas.alpha_composite(pose, (frame * CELL_WIDTH + (CELL_WIDTH - pose.width) // 2, args.row * CELL_HEIGHT + 199 - pose.height))
    atlas.save(args.atlas, optimize=True)
    print(f"Fila {args.row} actualizada desde {args.source.name}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
