#!/usr/bin/env python3
"""Write the shiba-inu pet manifest with ASCII-safe Unicode escapes."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


FW = 192
FH = 208
STATES = [
    ("idle", "Idle", "\u5f85\u6a5f", 0, 6, 140, True),
    ("running-right", "Run Right", "\u5411\u53f3\u79fb\u52d5", 1, 8, 80, True),
    ("running-left", "Run Left", "\u5411\u5de6\u79fb\u52d5", 2, 8, 80, True),
    ("waving", "Waving", "\u63ee\u624b", 3, 4, 140, True),
    ("jumping", "Jumping", "\u8df3\u8e8d", 4, 5, 110, False),
    ("failed", "Failed", "\u5931\u6557", 5, 8, 130, False),
    ("waiting", "Waiting", "\u7b49\u5f85", 6, 6, 150, True),
    ("running", "Running", "\u8655\u7406\u4e2d", 7, 6, 85, True),
    ("review", "Review", "\u5be9\u95b1", 8, 6, 150, True),
]


def frames(row: int, count: int) -> list[dict[str, int]]:
    return [
        {"index": index, "x": index * FW, "y": row * FH, "w": FW, "h": FH}
        for index in range(count)
    ]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    animations = {}
    row_mapping = []
    for key, label, label_zhtw, row, count, duration, loop in STATES:
        animations[key] = {
            "label": label,
            "labelZhTw": label_zhtw,
            "row": row,
            "startColumn": 0,
            "frameCount": count,
            "frameDurationMs": duration,
            "loop": loop,
            "frames": frames(row, count),
        }
        row_mapping.append(
            {
                "row": row,
                "key": key,
                "label": label,
                "labelZhTw": label_zhtw,
                "frameCount": count,
            }
        )

    pet = {
        "id": "shiba-inu",
        "name": "shiba_inu",
        "displayName": "\u67f4\u72ac",
        "description": "\u6a58\u767d\u8272\u50cf\u7d20\u98a8\u67f4\u72ac\uff0c\u6234\u8457\u7d05\u8272\u9805\u5708\uff0c\u8868\u60c5\u6d3b\u6f51\u53ef\u611b\u3002",
        "spritesheetPath": "spritesheet.webp",
        "spriteSheet": {
            "image": "spritesheet.webp",
            "width": 1536,
            "height": 1872,
            "columns": 8,
            "rows": 9,
            "frameWidth": FW,
            "frameHeight": FH,
        },
        "animations": animations,
        "rowMapping": row_mapping,
    }

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(pet, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
