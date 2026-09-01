#!/usr/bin/env python3
"""Inserta seis poses de orden no válida de Mummy en la fila 5 del atlas de la aplicación."""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


CELL_WIDTH = 192
CELL_HEIGHT = 208
ROW_INDEX = 5
FRAME_COUNT = 6
BACKGROUND_THRESHOLD = 42


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--atlas", required=True, type=Path)
    return parser.parse_args()


def connected_background(rgb: np.ndarray) -> np.ndarray:
    candidate = np.max(rgb, axis=2) <= BACKGROUND_THRESHOLD
    height, width = candidate.shape
    marked = np.zeros(candidate.shape, dtype=bool)
    queue: deque[tuple[int, int]] = deque()

    for x in range(width):
        queue.extend(((0, x), (height - 1, x)))
    for y in range(height):
        queue.extend(((y, 0), (y, width - 1)))

    while queue:
        y, x = queue.popleft()
        if y < 0 or y >= height or x < 0 or x >= width or marked[y, x] or not candidate[y, x]:
            continue
        marked[y, x] = True
        queue.extend(((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)))
    return marked


def extract_cell(source: Image.Image, index: int) -> Image.Image:
    left = round(index * source.width / FRAME_COUNT)
    right = round((index + 1) * source.width / FRAME_COUNT)
    slot = source.crop((left, 0, right, source.height)).convert("RGB")
    rgb = np.asarray(slot, dtype=np.uint8)
    alpha = (~connected_background(rgb)).astype(np.uint8) * 255
    alpha = np.asarray(Image.fromarray(alpha, "L").filter(ImageFilter.MaxFilter(3)), dtype=np.uint8)
    rgba = np.dstack((rgb, alpha))
    rgba[alpha == 0, :3] = 0
    result = Image.fromarray(rgba, "RGBA")
    cell = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT), (0, 0, 0, 0))
    cell.alpha_composite(result, ((CELL_WIDTH - result.width) // 2, (CELL_HEIGHT - result.height) // 2))
    return cell


def main() -> int:
    args = parse_args()
    source = Image.open(args.source).convert("RGB")
    if source.size != (1024, 200):
        raise SystemExit(f"La tira debe medir 1024x200; mide {source.size}.")
    atlas = Image.open(args.atlas).convert("RGBA")
    if atlas.size != (CELL_WIDTH * 8, CELL_HEIGHT * 15):
        raise SystemExit(f"El atlas debe medir 1536x3120; mide {atlas.size}.")

    for frame in range(8):
        atlas.paste((0, 0, 0, 0), (frame * CELL_WIDTH, ROW_INDEX * CELL_HEIGHT, (frame + 1) * CELL_WIDTH, (ROW_INDEX + 1) * CELL_HEIGHT))
    for frame in range(FRAME_COUNT):
        atlas.alpha_composite(extract_cell(source, frame), (frame * CELL_WIDTH, ROW_INDEX * CELL_HEIGHT))

    atlas.save(args.atlas, optimize=True)
    print(f"Fila {ROW_INDEX} actualizada con {FRAME_COUNT} fotogramas.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
