#!/usr/bin/env python3
"""Convert a checkerboard-backed Codex pet atlas into a transparent atlas."""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image


CELL_W = 192
CELL_H = 208
COLS = 8
ROWS = 9
FRAME_COUNTS = [6, 8, 8, 4, 5, 8, 6, 6, 6]


def likely_checker_pixel(rgb: np.ndarray) -> np.ndarray:
    high = rgb.min(axis=2) >= 210
    neutral = (rgb.max(axis=2) - rgb.min(axis=2)) <= 14
    return high & neutral


def exterior_background_mask(candidate_bg: np.ndarray) -> np.ndarray:
    height, width = candidate_bg.shape
    seen = np.zeros((height, width), dtype=bool)
    queue: deque[tuple[int, int]] = deque()

    def add(x: int, y: int) -> None:
        if candidate_bg[y, x] and not seen[y, x]:
            seen[y, x] = True
            queue.append((x, y))

    for x in range(width):
        add(x, 0)
        add(x, height - 1)
    for y in range(height):
        add(0, y)
        add(width - 1, y)

    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < width and 0 <= ny < height:
                add(nx, ny)

    return seen


def small_edge_fragments(alpha: np.ndarray, max_area: int = 1800) -> np.ndarray:
    height, width = alpha.shape
    foreground = alpha > 0
    seen = np.zeros((height, width), dtype=bool)
    remove = np.zeros((height, width), dtype=bool)

    for start_y in range(height):
        for start_x in range(width):
            if not foreground[start_y, start_x] or seen[start_y, start_x]:
                continue

            queue: deque[tuple[int, int]] = deque([(start_x, start_y)])
            seen[start_y, start_x] = True
            pixels: list[tuple[int, int]] = []
            touches_edge = False

            while queue:
                x, y = queue.popleft()
                pixels.append((x, y))
                if x == 0 or y == 0 or x == width - 1 or y == height - 1:
                    touches_edge = True
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if (
                        0 <= nx < width
                        and 0 <= ny < height
                        and foreground[ny, nx]
                        and not seen[ny, nx]
                    ):
                        seen[ny, nx] = True
                        queue.append((nx, ny))

            if touches_edge and len(pixels) <= max_area:
                for x, y in pixels:
                    remove[y, x] = True

    return remove


def clean_used_cell(cell: Image.Image) -> Image.Image:
    rgba = np.array(cell.convert("RGBA"), dtype=np.uint8)
    candidate_bg = likely_checker_pixel(rgba[:, :, :3])
    transparent = exterior_background_mask(candidate_bg)

    rgba[transparent, 3] = 0
    rgba[transparent, :3] = 0
    fragments = small_edge_fragments(rgba[:, :, 3])
    rgba[fragments, 3] = 0
    rgba[fragments, :3] = 0
    return Image.fromarray(rgba, "RGBA")


def clean_atlas(source: Path, output: Path) -> None:
    atlas = Image.open(source).convert("RGBA")
    if atlas.size != (CELL_W * COLS, CELL_H * ROWS):
        raise ValueError(f"Expected 1536x1872 atlas, got {atlas.size[0]}x{atlas.size[1]}")

    cleaned = Image.new("RGBA", atlas.size, (0, 0, 0, 0))
    for row, frame_count in enumerate(FRAME_COUNTS):
        for col in range(frame_count):
            box = (col * CELL_W, row * CELL_H, (col + 1) * CELL_W, (row + 1) * CELL_H)
            cleaned.paste(clean_used_cell(atlas.crop(box)), box)

    output.parent.mkdir(parents=True, exist_ok=True)
    if output.suffix.lower() == ".webp":
        cleaned.save(output, lossless=True, exact=True)
    else:
        cleaned.save(output)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    clean_atlas(args.source, args.output)


if __name__ == "__main__":
    main()
