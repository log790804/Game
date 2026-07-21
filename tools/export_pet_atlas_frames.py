#!/usr/bin/env python3
"""Export Codex pet atlas cells into per-state frame folders for QA previews."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image


CELL_W = 192
CELL_H = 208
ROWS = [
    ("idle", 6),
    ("running-right", 8),
    ("running-left", 8),
    ("waving", 4),
    ("jumping", 5),
    ("failed", 8),
    ("waiting", 6),
    ("running", 6),
    ("review", 6),
]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("atlas", type=Path)
    parser.add_argument("output_dir", type=Path)
    args = parser.parse_args()

    atlas = Image.open(args.atlas).convert("RGBA")
    args.output_dir.mkdir(parents=True, exist_ok=True)

    manifest_rows = []
    for row_index, (state, frame_count) in enumerate(ROWS):
        state_dir = args.output_dir / state
        state_dir.mkdir(parents=True, exist_ok=True)
        for frame_index in range(frame_count):
            box = (
                frame_index * CELL_W,
                row_index * CELL_H,
                (frame_index + 1) * CELL_W,
                (row_index + 1) * CELL_H,
            )
            atlas.crop(box).save(state_dir / f"{frame_index:02d}.png")
        manifest_rows.append(
            {
                "state": state,
                "row": row_index,
                "frame_count": frame_count,
                "method": "components",
            }
        )

    (args.output_dir / "frames-manifest.json").write_text(
        json.dumps({"rows": manifest_rows}, indent=2) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
