#!/usr/bin/env python3
"""Validate a <char>.vhforges.json (BodyForge proportions) file.

Checks structure per schema/vhforges.schema.json and, when a bone catalogue is
available, warns about unknown bone names and auto-resolves aliases (so the
editor can normalize names).

Usage:
  validate_proportions.py [--catalogue data/bone_catalogue.v1.json] FILE...
Exit: 0 valid (OK), 1 invalid, 2 usage error.
"""
import argparse
import json
import math
import sys


def _is_finite_number(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool) and math.isfinite(v)


def validate_doc(doc, path, catalogue):
    errors = []
    warnings = []
    if not isinstance(doc, dict):
        return ["document must be a JSON object"], warnings

    if doc.get("schemaVersion") != 1:
        errors.append("schemaVersion missing or != 1")
    if "bones" not in doc or not isinstance(doc["bones"], dict):
        errors.append("bones must be an object")
        return errors, warnings

    if "character" in doc:
        if not isinstance(doc["character"], str) or not doc["character"].strip():
            errors.append("character must be a non-empty string")

    if "unknown" not in doc and any(k not in {"schemaVersion", "character", "bones"}
                                   for k in doc):
        warnings.append("top-level keys beyond schema ignored")

    # Catalogue alias map for name normalization
    lookup = {}
    if catalogue:
        for bone, info in catalogue.get("bones", {}).items():
            lookup[bone] = bone
            for alias in info.get("aliases", []):
                lookup[alias] = bone
        known = set(lookup)

    for name, spec in doc["bones"].items():
        if not isinstance(name, str) or not (1 <= len(name) <= 64):
            errors.append(f"bone key must be a 1..64 length string: {name!r}")
            continue
        if not isinstance(spec, dict):
            errors.append(f"bones['{name}'] must be an object")
            continue
        if "scale" not in spec:
            errors.append(f"bones['{name}'] missing scale")
            continue
        scale = spec["scale"]
        if not isinstance(scale, list) or len(scale) != 3:
            errors.append(f"bones['{name}'].scale must be a 3-number array")
        elif not all(_is_finite_number(x) for x in scale):
            errors.append(f"bones['{name}'].scale must contain finite numbers")
        elif not all(0.01 <= x <= 100 for x in scale):
            errors.append(f"bones['{name}'].scale values must be in [0.01, 100]")
        for extra in set(spec) - {"scale", "enabled"}:
            warnings.append(f"bones['{name}'] has unknown field {extra!r}")
        if "enabled" in spec and not isinstance(spec["enabled"], bool):
            errors.append(f"bones['{name}'].enabled must be a boolean")

        if catalogue:
            if name in known:
                pass
            elif name in lookup:
                warnings.append(f"bone {name!r} is an alias for {lookup[name]!r}")
            else:
                # allow exact-cased model names not in catalogue
                warnings.append(f"bone {name!r} not found in catalogue")

    return errors, warnings


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("files", nargs="+", help="vhforges JSON files to validate")
    ap.add_argument("--catalogue", default=None,
                    help="path to bone_catalogue*.json (optional)")
    ap.add_argument("--no-warnings", action="store_true")
    args = ap.parse_args()

    catalogue = None
    if args.catalogue:
        try:
            with open(args.catalogue, encoding="utf-8") as f:
                catalogue = json.load(f)
        except OSError as e:
            print(f"{sys.argv[0]}: catalogue: {e}", file=sys.stderr)
            return 2

    rc = 0
    for path in args.files:
        try:
            with open(path, encoding="utf-8") as f:
                doc = json.load(f)
        except (OSError, json.JSONDecodeError) as e:
            print(f"{path}: ERROR: {e}")
            rc = 1
            continue
        errors, warnings = validate_doc(doc, path, catalogue)
        status = "OK" if not errors else "INVALID"
        print(f"{path}: {status}")
        for w in warnings:
            if not args.no_warnings:
                print(f"  warn: {w}")
        for e in errors:
            print(f"  error: {e}")
        if errors:
            rc = 1
    return rc


if __name__ == "__main__":
    sys.exit(main())