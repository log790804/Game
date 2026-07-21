from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image


PROJECT_DIR = Path(__file__).resolve().parents[1]
WEB_PREVIEW_DIR = PROJECT_DIR / "web-preview"
MANIFEST_FILE = WEB_PREVIEW_DIR / "assets" / "heroes" / "manifest.json"
DEFAULT_OUTPUT_DIR = WEB_PREVIEW_DIR / "exports" / "hero-gifs"
IDLE_UNDERLAY_RANGES = {
    "flame-swordsman": {
        "skill-02": range(4, 11),
    },
}
FORWARD_SLOT_RANGES = {
    "flame-swordsman": {
        "skill-02": range(4, 11),
    },
}


def load_manifest() -> dict:
    if not MANIFEST_FILE.exists():
        raise SystemExit(f"Manifest not found: {MANIFEST_FILE}")
    return json.loads(MANIFEST_FILE.read_text(encoding="utf-8"))


def place_on_canvas(frame: Image.Image, width: int, height: int) -> Image.Image:
    frame = frame.convert("RGBA")
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    x = (width - frame.width) // 2
    y = height - frame.height
    canvas.alpha_composite(frame, (x, y))
    return canvas


def place_in_slot(frame: Image.Image, slot_index: int, slot_width: int, width: int, height: int) -> Image.Image:
    frame = frame.convert("RGBA")
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    x = slot_index * slot_width + (slot_width - frame.width) // 2
    y = height - frame.height
    canvas.alpha_composite(frame, (x, y))
    return canvas


def normalize_frame(path: Path, width: int, height: int) -> Image.Image:
    frame = Image.open(path).convert("RGBA")
    return place_on_canvas(frame, width, height)


def matte_frame(frame: Image.Image, background: tuple[int, int, int] | None) -> Image.Image:
    if background is None:
        return frame
    matte = Image.new("RGBA", frame.size, (*background, 255))
    matte.alpha_composite(frame)
    return matte


def save_gif(
    frames: list[Image.Image],
    output_path: Path,
    fps: int,
    play_once: bool,
    background: tuple[int, int, int] | None,
    slow_factor: float,
) -> None:
    duration = max(20, round((1000 / max(1, fps)) * max(0.1, slow_factor)))
    gif_frames = [matte_frame(frame, background) for frame in frames]
    output_path.parent.mkdir(parents=True, exist_ok=True)
    gif_frames[0].save(
        output_path,
        save_all=True,
        append_images=gif_frames[1:],
        duration=duration,
        loop=1 if play_once else 0,
        disposal=2,
        optimize=False,
    )


def parse_hex_color(value: str | None) -> tuple[int, int, int] | None:
    if not value:
        return None
    color = value.strip().lstrip("#")
    if len(color) != 6:
        raise argparse.ArgumentTypeError("background must be a 6-digit hex color, such as #101820")
    return tuple(int(color[index : index + 2], 16) for index in (0, 2, 4))


def build_action_frames(hero: dict, action: dict, idle_action: dict | None, return_idle: bool) -> list[Image.Image]:
    uses_idle = action["id"].startswith("skill-") and idle_action is not None
    width = action["frameWidth"]
    height = action["frameHeight"]
    forward_range = FORWARD_SLOT_RANGES.get(hero["id"], {}).get(action["id"], range(0))
    slot_width = max(width, idle_action["frameWidth"] if idle_action else width)
    if uses_idle:
        width = max(width, idle_action["frameWidth"])
        height = max(height, idle_action["frameHeight"])
    if forward_range:
        width = slot_width * 2

    idle_frames = []
    if idle_action is not None:
        idle_frames = [
            place_in_slot(Image.open(WEB_PREVIEW_DIR / frame_path), 0, slot_width, width, height)
            if forward_range
            else normalize_frame(WEB_PREVIEW_DIR / frame_path, width, height)
            for frame_path in idle_action["frames"]
        ]

    underlay_range = IDLE_UNDERLAY_RANGES.get(hero["id"], {}).get(action["id"], range(0))
    output_frames: list[Image.Image] = []

    for index, frame_path in enumerate(action["frames"], start=1):
        raw_action_frame = Image.open(WEB_PREVIEW_DIR / frame_path)
        action_slot = 1 if index in forward_range else 0
        action_frame = (
            place_in_slot(raw_action_frame, action_slot, slot_width, width, height)
            if forward_range
            else normalize_frame(WEB_PREVIEW_DIR / frame_path, width, height)
        )
        if index in underlay_range and idle_frames:
            composed = idle_frames[0].copy()
            composed.alpha_composite(action_frame)
            output_frames.append(composed)
        else:
            output_frames.append(action_frame)

    if return_idle and action["id"].startswith("skill-") and idle_frames:
        output_frames.extend(frame.copy() for frame in idle_frames)

    return output_frames


def export_gifs(
    hero_filter: str | None,
    action_filter: str | None,
    output_dir: Path,
    background: tuple[int, int, int] | None,
    play_once: bool,
    slow_factor: float,
    return_idle: bool,
) -> None:
    manifest = load_manifest()
    exported = 0

    for hero in manifest["heroes"]:
        if hero_filter and hero["id"] != hero_filter:
            continue
        idle_action = next((action for action in hero["actions"] if action["id"] == "idle"), None)
        for action in hero["actions"]:
            if action_filter and action["id"] != action_filter:
                continue

            frames = build_action_frames(hero, action, idle_action, return_idle)
            if not frames:
                continue

            output_path = output_dir / hero["id"] / f"{hero['id']}-{action['id']}.gif"
            save_gif(frames, output_path, action["fps"], play_once, background, slow_factor)
            exported += 1

    print(f"Exported {exported} GIF files.")
    print(f"Output: {output_dir}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Export hero animation frames to GIF files for PPT or docs.")
    parser.add_argument("--hero", help="Only export one hero id, such as ice-crystal-mage.")
    parser.add_argument("--action", help="Only export one action id, such as skill-01.")
    parser.add_argument("--out", default=str(DEFAULT_OUTPUT_DIR), help="Output directory.")
    parser.add_argument("--play-once", action="store_true", help="Play each GIF once instead of looping forever.")
    parser.add_argument("--return-idle", action="store_true", help="Append one idle cycle after each skill animation.")
    parser.add_argument("--slow-factor", type=float, default=1.0, help="Multiply frame duration. Example: 1.5 is slower.")
    parser.add_argument(
        "--background",
        type=parse_hex_color,
        help="Optional matte background color. Omit for transparent GIF, or use #101820 for dark PPT slides.",
    )
    args = parser.parse_args()

    export_gifs(
        args.hero,
        args.action,
        Path(args.out),
        args.background,
        args.play_once,
        args.slow_factor,
        args.return_idle,
    )


if __name__ == "__main__":
    main()
