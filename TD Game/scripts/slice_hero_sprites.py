from __future__ import annotations

import json
import shutil
from dataclasses import dataclass
from pathlib import Path

from PIL import Image


PROJECT_DIR = Path(__file__).resolve().parents[1]
INPUT_DIR = PROJECT_DIR / "heros"
OUTPUT_DIR = PROJECT_DIR / "web-preview" / "assets" / "heroes"
DATA_FILE = PROJECT_DIR / "web-preview" / "assets" / "heroes" / "hero-animations-data.js"

HERO_SLUGS = {
    "光盾騎士": ("light-shield-knight", "光盾騎士"),
    "冰晶法姬": ("ice-crystal-mage", "冰晶法姬"),
    "機械少女": ("mecha-girl", "機械少女"),
    "火焰劍士": ("flame-swordsman", "火焰劍士"),
    "闇影刺客": ("shadow-assassin", "闇影刺客"),
    "雷電機甲": ("thunder-mech", "雷電機甲"),
    "風羽遊俠": ("wind-ranger", "風羽遊俠"),
}

BASE_ACTIONS = [
    ("idle", "待機", 5),
    ("walk", "行走", 8),
    ("run", "跑步", 10),
    ("jump", "跳躍", 10),
    ("attack", "普攻", 12),
    ("hurt", "受擊", 8),
    ("knockdown", "倒地", 8),
]


@dataclass(frozen=True)
class Cell:
    row: int
    col: int
    image: Image.Image
    content_pixels: int


def group_runs(values: list[int], max_gap: int = 1) -> list[tuple[int, int]]:
    groups: list[tuple[int, int]] = []
    start = previous = None
    for value in values:
        if start is None:
            start = previous = value
            continue
        if value <= previous + max_gap:
            previous = value
            continue
        groups.append((start, previous))
        start = previous = value
    if start is not None and previous is not None:
        groups.append((start, previous))
    return groups


def fill_missing_regular_lines(lines: list[int]) -> list[int]:
    if len(lines) < 3:
        return lines

    gaps = [right - left for left, right in zip(lines, lines[1:]) if right > left]
    if not gaps:
        return lines

    sorted_gaps = sorted(gaps)
    typical_gap = sorted_gaps[len(sorted_gaps) // 2]
    if typical_gap <= 0:
        return lines

    filled = [lines[0]]
    for left, right in zip(lines, lines[1:]):
        gap = right - left
        if gap > typical_gap * 1.55:
            missing_count = round(gap / typical_gap) - 1
            if missing_count > 0:
                step = gap / (missing_count + 1)
                for index in range(1, missing_count + 1):
                    filled.append(round(left + step * index))
        filled.append(right)

    return filled


def is_green_grid(pixel: tuple[int, int, int]) -> bool:
    red, green, blue = pixel
    return green >= 70 and green - max(red, blue) >= 45 and red < 80 and blue < 90


def is_gray_grid(pixel: tuple[int, int, int]) -> bool:
    red, green, blue = pixel
    high = max(red, green, blue)
    low = min(red, green, blue)
    return high - low <= 12 and 45 <= high <= 145


def detect_grid_lines(image: Image.Image) -> tuple[list[int], list[int], bool]:
    width, height = image.size
    pixels = image.load()

    def detect(predicate, threshold: float) -> tuple[list[int], list[int]]:
        x_candidates: list[int] = []
        y_candidates: list[int] = []
        for x in range(width):
            count = sum(1 for y in range(height) if predicate(pixels[x, y]))
            if count / height > threshold:
                x_candidates.append(x)
        for y in range(height):
            count = sum(1 for x in range(width) if predicate(pixels[x, y]))
            if count / width > threshold:
                y_candidates.append(y)
        x_lines = [round((start + end) / 2) for start, end in group_runs(x_candidates)]
        y_lines = [round((start + end) / 2) for start, end in group_runs(y_candidates)]
        return x_lines, y_lines

    x_lines, y_lines = detect(is_green_grid, 0.2)
    if len(x_lines) >= 3 and len(y_lines) >= 3:
        return fill_missing_regular_lines(x_lines), y_lines, x_lines[0] > 40

    x_lines, y_lines = detect(is_gray_grid, 0.45)
    if len(x_lines) >= 3 and len(y_lines) >= 3:
        return fill_missing_regular_lines(x_lines), y_lines, False

    raise RuntimeError("Could not detect sprite grid lines.")


def transparent_cell(source: Image.Image) -> tuple[Image.Image, int]:
    rgba = source.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    content_pixels = 0

    for y in range(height):
        for x in range(width):
            red, green, blue, _ = pixels[x, y]
            high = max(red, green, blue)
            low = min(red, green, blue)
            near_border = x < 3 or y < 3 or x >= width - 3 or y >= height - 3
            is_background = high <= 14 or (high <= 22 and high - low <= 10)
            is_grid_edge = near_border and (
                is_green_grid((red, green, blue)) or is_gray_grid((red, green, blue))
            )
            if is_grid_edge or is_background:
                pixels[x, y] = (red, green, blue, 0)
            else:
                pixels[x, y] = (red, green, blue, 255)
                content_pixels += 1

    return rgba, content_pixels


def has_visible_content(image: Image.Image) -> bool:
    pixels = image.get_flattened_data() if hasattr(image, "get_flattened_data") else image.getdata()
    for red, green, blue, alpha in pixels:
        if alpha > 0 and max(red, green, blue) > 28:
            return True
    return False


def collect_cells(image: Image.Image, x_lines: list[int], y_lines: list[int]) -> list[list[Cell]]:
    rows: list[list[Cell]] = []
    for row_index, (top, bottom) in enumerate(zip(y_lines, y_lines[1:])):
        row: list[Cell] = []
        for col_index, (left, right) in enumerate(zip(x_lines, x_lines[1:])):
            if right - left < 12 or bottom - top < 12:
                continue
            raw = image.crop((left + 1, top + 1, right, bottom))
            frame, content_pixels = transparent_cell(raw)
            if content_pixels > 35 and has_visible_content(frame):
                row.append(Cell(row_index, col_index, frame, content_pixels))
        rows.append(row)
    return rows


def action_name_for(index: int) -> tuple[str, str, int]:
    if index < len(BASE_ACTIONS):
        return BASE_ACTIONS[index]
    skill_number = index - len(BASE_ACTIONS) + 1
    return (f"skill-{skill_number:02d}", f"技能 {skill_number:02d}", 12)


def build_sheet(cells: list[Cell]) -> tuple[Image.Image, int, int]:
    frame_width = max(cell.image.width for cell in cells)
    frame_height = max(cell.image.height for cell in cells)
    sheet = Image.new("RGBA", (frame_width * len(cells), frame_height), (0, 0, 0, 0))
    for index, cell in enumerate(cells):
        x = index * frame_width + (frame_width - cell.image.width) // 2
        y = frame_height - cell.image.height
        sheet.alpha_composite(cell.image, (x, y))
    return sheet, frame_width, frame_height


def write_frame_strip(cells: list[Cell], out_dir: Path, slug: str, action: str) -> list[str]:
    frame_paths: list[str] = []
    for index, cell in enumerate(cells, start=1):
        frame_name = f"{slug}-{action}-{index:03d}.png"
        frame_path = out_dir / frame_name
        cell.image.save(frame_path)
        frame_paths.append(frame_path.relative_to(PROJECT_DIR / "web-preview").as_posix())
    return frame_paths


def build_actions(rows: list[list[Cell]], has_label_column: bool) -> list[tuple[str, str, int, list[Cell]]]:
    non_empty_rows = [row for row in rows if row]
    actions: list[tuple[str, str, int, list[Cell]]] = []
    row_index = 0
    action_index = 0

    while row_index < len(non_empty_rows):
        if has_label_column and action_index == 11 and row_index + 1 < len(non_empty_rows):
            first = non_empty_rows[row_index]
            second = non_empty_rows[row_index + 1]
            action, label, fps = ("skill-05", "技能 05", 12)
            actions.append((action, label, fps, first + second))
            row_index += 2
            action_index += 1
            continue

        action, label, fps = action_name_for(action_index)
        actions.append((action, label, fps, non_empty_rows[row_index]))
        row_index += 1
        action_index += 1

    return actions


def process_hero(file_path: Path) -> dict:
    source_name = file_path.stem
    slug, display_name = HERO_SLUGS.get(source_name, (source_name, source_name))
    image = Image.open(file_path).convert("RGB")
    x_lines, y_lines, has_label_column = detect_grid_lines(image)
    rows = collect_cells(image, x_lines, y_lines)
    actions = build_actions(rows, has_label_column)

    hero_dir = OUTPUT_DIR / slug
    hero_dir.mkdir(parents=True, exist_ok=True)

    hero_manifest = {
        "id": slug,
        "name": display_name,
        "source": file_path.relative_to(PROJECT_DIR).as_posix(),
        "grid": {
            "columns": len(x_lines) - 1,
            "rows": len(y_lines) - 1,
            "hasLabelColumn": has_label_column,
        },
        "actions": [],
    }

    for action, label, fps, cells in actions:
        action_dir = hero_dir / action
        if action_dir.exists():
            shutil.rmtree(action_dir)
        action_dir.mkdir(parents=True, exist_ok=True)
        frames = write_frame_strip(cells, action_dir, slug, action)
        sheet, frame_width, frame_height = build_sheet(cells)
        sheet_path = hero_dir / f"{slug}-{action}-sheet.png"
        sheet.save(sheet_path)

        hero_manifest["actions"].append(
            {
                "id": action,
                "label": label,
                "fps": fps,
                "loop": action in {"idle", "walk", "run"},
                "frameCount": len(cells),
                "frameWidth": frame_width,
                "frameHeight": frame_height,
                "sheet": sheet_path.relative_to(PROJECT_DIR / "web-preview").as_posix(),
                "frames": frames,
            }
        )

    return hero_manifest


def write_data_file(manifest: dict) -> None:
    json_text = json.dumps(manifest, ensure_ascii=False, indent=2)
    DATA_FILE.write_text(
        "window.TD_HERO_ANIMATIONS = "
        + json_text
        + ";\n",
        encoding="utf-8",
    )


def main() -> None:
    if not INPUT_DIR.exists():
        raise SystemExit(f"Input directory not found: {INPUT_DIR}")
    if OUTPUT_DIR.exists():
        resolved_output = OUTPUT_DIR.resolve()
        resolved_project = PROJECT_DIR.resolve()
        if resolved_project not in resolved_output.parents:
            raise SystemExit(f"Refusing to clean output outside project: {OUTPUT_DIR}")
        shutil.rmtree(OUTPUT_DIR)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    heroes = [process_hero(path) for path in sorted(INPUT_DIR.glob("*.png"))]
    manifest = {
        "version": 1,
        "generatedBy": "scripts/slice_hero_sprites.py",
        "heroes": heroes,
    }
    (OUTPUT_DIR / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    write_data_file(manifest)

    total_actions = sum(len(hero["actions"]) for hero in heroes)
    total_frames = sum(
        action["frameCount"]
        for hero in heroes
        for action in hero["actions"]
    )
    print(f"Processed {len(heroes)} heroes, {total_actions} actions, {total_frames} frames.")
    print(f"Output: {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
