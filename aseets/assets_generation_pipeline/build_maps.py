from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

CLASS_IDS = {
    "default": 0,
    "river": 1,
    "mountain": 2,
    "snow": 3,
    "buildings_can_destroy": 4,
    "buildings_no_destroy": 5,
}
PALETTE = np.array([[92, 112, 74], [42, 122, 190], [111, 91, 70],
                    [232, 239, 245], [188, 142, 91], [102, 74, 58]], dtype=np.uint8)
ROOT = Path(__file__).resolve().parent


def parse_args():
    parser = argparse.ArgumentParser(description="Build six-class Godot 4 TileMapLayer scenes.")
    parser.add_argument("--input", type=Path, default=ROOT / "source/maps")
    parser.add_argument("--output", type=Path, default=ROOT / "generated_maps")
    parser.add_argument("--masks", type=Path, help="Reviewed L-mode ID masks named like source stems.")
    parser.add_argument("--size", type=int, default=1024, help="Longest raster side, 256..4096.")
    parser.add_argument("--grid-size", type=int, default=128, help="Longest scene grid side, 8..256.")
    parser.add_argument("--cell-size", type=int, default=32, help="Godot world pixels per cell, 8..128.")
    parser.add_argument("--building-policy", choices=["can_destroy", "no_destroy"], default="can_destroy")
    parser.add_argument("--overwrite", action="store_true")
    return parser.parse_args()


def safe_stem(path):
    stem = re.sub(r"[^A-Za-z0-9_-]+", "_", path.stem).strip("_")
    if not stem:
        raise ValueError(f"Filename needs an ASCII identifier: {path.name}")
    return stem


def target_size(shape, longest_side):
    height, width = shape[:2]
    scale = longest_side / max(width, height)
    return max(1, round(width * scale)), max(1, round(height * scale))


def classify(source, building_policy="can_destroy"):
    hsv = cv2.cvtColor(source, cv2.COLOR_RGB2HSV)
    gray = cv2.cvtColor(source, cv2.COLOR_RGB2GRAY)
    labels = np.zeros(source.shape[:2], dtype=np.uint8)
    blue = ((hsv[:, :, 0] >= 82) & (hsv[:, :, 0] <= 135)
            & (hsv[:, :, 1] >= 35) & (hsv[:, :, 2] >= 45))
    blue = cv2.morphologyEx(blue.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
    labels[blue > 0] = CLASS_IDS["river"]
    ink_density = cv2.boxFilter((gray <= 135).astype(np.float32), -1, (7, 7))
    labels[(ink_density >= 0.20) & (labels == 0)] = CLASS_IDS[f"buildings_{building_policy}"]
    return labels


def read_mask(path, source_size, output_size):
    with Image.open(path) as image:
        image.load()
        if image.mode != "L" or image.size != source_size:
            raise ValueError(f"{path}: expected L-mode mask with source size {source_size}")
        if np.asarray(image).max() > 5:
            raise ValueError(f"{path}: only integer IDs 0..5 are allowed")
        return np.asarray(image.resize(output_size, Image.Resampling.NEAREST)).copy()


def make_grid(labels, longest_side):
    width, height = target_size(labels.shape, longest_side)
    grid = np.zeros((height, width), dtype=np.uint8)
    for row in range(height):
        top = row * labels.shape[0] // height
        bottom = max(top + 1, (row + 1) * labels.shape[0] // height)
        for column in range(width):
            left = column * labels.shape[1] // width
            right = max(left + 1, (column + 1) * labels.shape[1] // width)
            block = labels[top:bottom, left:right]
            grid[row, column] = np.bincount(block.ravel(), minlength=6).argmax()
    return grid


def statistics(labels):
    counts = np.bincount(labels.ravel(), minlength=6)
    return {name: {"id": class_id, "pixels": int(counts[class_id]),
                   "coverage": float(counts[class_id] / labels.size)}
            for name, class_id in CLASS_IDS.items()}


def write_json(path, payload):
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_scene(path, grid, cell_size):
    cells = ", ".join(map(str, grid.ravel().tolist()))
    path.write_text(
        '[gd_scene load_steps=2 format=3]\n\n'
        '[ext_resource type="Script" path="../semantic_map.gd" id="1"]\n\n'
        '[node name="SemanticMap" type="Node2D"]\n'
        'script = ExtResource("1")\n'
        f'grid_width = {grid.shape[1]}\ngrid_height = {grid.shape[0]}\n'
        f'cell_size = {cell_size}\ncells = PackedInt32Array({cells})\n', encoding="utf-8")


def build_one(source_path, args):
    stem = safe_stem(source_path)
    map_dir = args.output / stem
    if map_dir.exists() and not args.overwrite:
        raise ValueError(f"Output exists: {map_dir}; use --overwrite to rebuild")
    with Image.open(source_path) as image:
        image.load()
        original_size = image.size
        output_size = target_size((image.height, image.width), args.size)
        source = np.asarray(image.convert("RGB").resize(output_size, Image.Resampling.BOX))
    mask_path = args.masks / f"{source_path.stem}.png" if args.masks else None
    if mask_path is not None:
        if not mask_path.is_file():
            raise ValueError(f"Missing reviewed mask: {mask_path}")
        labels = read_mask(mask_path, original_size, output_size)
        mode = "provided_mask"
        warnings = ["Provided mask used; spatial and historical correctness still require review."]
    else:
        labels = classify(source, args.building_policy)
        mode = "heuristic_draft"
        warnings = [
            "Draft only: blue ink can be mistaken for water; text and roads for buildings.",
            "Snow and mountains are NOT inferred from white paper or contour-line texture; supply a reviewed mask.",
            f"All detected buildings use explicit gameplay policy: {args.building_policy}.",
            "No historical accuracy, playability, bridges or route connectivity is guaranteed.",
        ]
    grid = make_grid(labels, args.grid_size)
    if np.all(grid == 0):
        warnings.append("Scene grid is entirely default terrain; provide a reviewed mask before gameplay use.")
    if np.mean((grid == 4) | (grid == 5)) > 0.5:
        warnings.append("Over half the scene is classified as buildings; likely ink/contour false positives, review required.")
    map_dir.mkdir(parents=True, exist_ok=True)
    Image.fromarray(labels).save(map_dir / "semantic_mask.png")
    Image.fromarray(PALETTE[labels]).save(map_dir / "preview.png")
    Image.fromarray(PALETTE[grid]).resize(
        (grid.shape[1] * 4, grid.shape[0] * 4), Image.Resampling.NEAREST).save(map_dir / "grid_preview.png")
    write_scene(map_dir / "map.tscn", grid, args.cell_size)
    payload = {
        "schema": "kingwar.semantic-map.v2", "source": source_path.name,
        "source_sha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
        "source_size": list(original_size), "semantic_mask": "semantic_mask.png", "preview": "preview.png",
        "scene": "map.tscn", "width": labels.shape[1], "height": labels.shape[0],
        "class_ids": CLASS_IDS, "class_pixels": statistics(labels),
        "element_types": [
            {"id": 0, "type": "default"}, {"id": 1, "type": "river"},
            {"id": 2, "type": "mountain"}, {"id": 3, "type": "snow"},
            {"id": 4, "type": "buildings", "can_destroy": True},
            {"id": 5, "type": "buildings", "can_destroy": False},
        ],
        "grid": {"width": grid.shape[1], "height": grid.shape[0], "cell_size": args.cell_size,
                 "sampling": "majority_vote_lowest_id_on_tie", "class_cells": statistics(grid),
                 "world_size": [grid.shape[1] * args.cell_size, grid.shape[0] * args.cell_size]},
        "classification": {"mode": mode, "warnings": warnings,
                           "mask": str(mask_path) if mask_path else None},
    }
    write_json(map_dir / "map.json", payload)
    return payload


def main():
    args = parse_args()
    if not 256 <= args.size <= 4096 or not 8 <= args.grid_size <= 256 or not 8 <= args.cell_size <= 128:
        raise SystemExit("Expected --size 256..4096, --grid-size 8..256, --cell-size 8..128")
    if not args.input.exists():
        raise SystemExit(f"Missing input: {args.input}")
    extensions = {".png", ".jpg", ".jpeg", ".tif", ".tiff"}
    sources = [args.input] if args.input.is_file() else sorted(
        path for path in args.input.iterdir() if path.is_file() and path.suffix.lower() in extensions)
    if not sources or any(path.suffix.lower() not in extensions for path in sources):
        raise SystemExit("No supported source images")
    stems = [safe_stem(path).lower() for path in sources]
    if len(set(stems)) != len(stems):
        raise SystemExit("Colliding output names; rename input images")
    args.output.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / "runtime/semantic_map.gd", args.output / "semantic_map.gd")
    results, failures = [], []
    for index, source in enumerate(sources, 1):
        try:
            result = build_one(source, args)
            results.append(result)
            print(f"[{index:02d}/{len(sources):02d}] OK {source.name}")
        except Exception as error:
            failures.append({"source": source.name, "error": str(error)})
            print(f"FAIL {source.name}: {error}", file=sys.stderr)
    write_json(args.output / "build_report.json", {
        "source_count": len(sources), "success_count": len(results), "failure_count": len(failures),
        "requested_longest_side": args.size, "grid_longest_side": args.grid_size,
        "failures": failures, "maps": results,
    })
    print(f"Generated {len(results)}/{len(sources)} Godot scenes in {args.output}")
    return int(bool(failures))


if __name__ == "__main__":
    raise SystemExit(main())
