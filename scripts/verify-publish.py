#!/usr/bin/env python3
"""Reject incomplete, WPF, or self-contained Shion release payloads."""

import argparse
import json
from pathlib import Path


def verify(publish_dir: Path, runtime_config: Path, rid: str) -> int:
    config = json.loads(runtime_config.read_text(encoding="utf-8"))["runtimeOptions"]
    frameworks = config.get("frameworks", [])
    if "framework" in config:
        frameworks = [*frameworks, config["framework"]]
    if config.get("includedFrameworks"):
        raise ValueError("The runtime configuration contains a bundled .NET runtime.")
    if len(frameworks) != 1 or frameworks[0].get("name") != "Microsoft.NETCore.App":
        raise ValueError("Shion must depend only on Microsoft.NETCore.App, without WPF/Desktop Runtime.")
    if not frameworks[0].get("version", "").startswith("10."):
        raise ValueError("The release must target the .NET 10 runtime.")

    executable = publish_dir / ("PCL.Shion.exe" if rid.startswith("win-") else "PCL.Shion")
    if not executable.is_file() or executable.stat().st_size == 0:
        raise ValueError(f"Missing or empty release executable: {executable}")

    # Packaging moves the Windows executable alone. Reject leftovers instead of
    # silently dropping a native library or content file required at startup.
    files = sorted(path for path in publish_dir.rglob("*") if path.is_file())
    extras = [str(path.relative_to(publish_dir)) for path in files if path != executable]
    if extras:
        raise ValueError(f"The single-file payload has unexpected sidecar files: {', '.join(extras)}")
    return executable.stat().st_size


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish-dir", required=True, type=Path)
    parser.add_argument("--runtime-config", required=True, type=Path,
                        help="The build output runtimeconfig.json before single-file bundling")
    parser.add_argument("--rid", required=True,
                        choices=["win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"])
    args = parser.parse_args()
    try:
        size = verify(args.publish_dir, args.runtime_config, args.rid)
    except (OSError, ValueError, KeyError, TypeError) as error:
        parser.exit(1, f"Publish verification failed: {error}\n")
    print(f"Verified {args.rid}: framework-dependent Avalonia, {size:,} bytes ({size / 1024 / 1024:.2f} MiB).")


if __name__ == "__main__":
    main()
