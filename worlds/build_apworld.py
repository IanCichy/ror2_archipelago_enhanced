#!/usr/bin/env python3
# ============================================================
# build_apworld.py
# Requires: Python 3.6+
# Usage: python build_apworld.py --world-dir worlds/ror2 --output RiskOfRain2.apworld
# Or with defaults: python build_apworld.py
# ============================================================

import shutil
import zipfile
import argparse
from pathlib import Path

# Subdirectories to exclude from the archive
EXCLUDE_DIRS = {"docs", "test"}

def parse_args():
    parser = argparse.ArgumentParser(description="Build an .apworld file. "
    "This script should live in the worlds folder to run properly.")
    parser.add_argument(
        "--world-dir",
        type=Path,
        default=Path("ror2"),
        help="Path to the world folder (default: ror2)"
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("ror2.apworld"),
        help="Output .apworld file name (default: ror2.apworld)"
    )
    return parser.parse_args()

def clean_pycache(directory: Path):
    """Remove all __pycache__ folders recursively."""
    for pycache in directory.rglob("__pycache__"):
        print(f"Removing {pycache}...")
        shutil.rmtree(pycache)

def is_excluded(file: Path) -> bool:
    """Return True if the file lives inside an excluded subdirectory."""
    return any(part in EXCLUDE_DIRS for part in file.parts)

def build_apworld(world_dir: Path, output: Path):
    if not world_dir.exists():
        print(f"Error: {world_dir} not found. Are you running from the repo root?")
        raise SystemExit(1)

    if output.exists():
        print(f"Removing old {output}...")
        output.unlink()

    print("Cleaning __pycache__...")
    clean_pycache(world_dir)

    print(f"Building {output}...")
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as zf:
        for file in world_dir.rglob("*"):
            if file.is_file() and not is_excluded(file):
                arcname = file.relative_to(world_dir.parent)
                zf.write(file, arcname)
                print(f"  Adding {arcname}")
            elif file.is_file():
                print(f"  Skipping {file}")

    print(f"\nDone! {output} created.")

if __name__ == "__main__":
    args = parse_args()
    build_apworld(args.world_dir, args.output)