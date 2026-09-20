"""Validate the intentionally small frontend architecture."""

from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "front" / "src"
REQUIRED = ("app", "features", "components", "lib")
PLANNED_FEATURES = ("auth", "summary", "users", "audit", "ledger")
FORBIDDEN = ("domain", "application", "infrastructure", "ports", "adapters")
REQUIRED_FILES = (
    ("app", "App.tsx"),
    ("features/auth", "oidc.ts"),
    ("lib", "http.ts"),
)


def main() -> int:
    errors: list[str] = []

    if not SRC.is_dir():
        errors.append(f"frontend source directory not found: {SRC}")
    else:
        for name in REQUIRED:
            if not (SRC / name).is_dir():
                errors.append(f"missing frontend directory: src/{name}")
        features = SRC / "features"
        for name in PLANNED_FEATURES:
            if not (features / name).is_dir():
                errors.append(f"missing planned frontend feature: src/features/{name}")
        for directory, filename in REQUIRED_FILES:
            if not (SRC / directory / filename).is_file():
                errors.append(f"missing frontend entrypoint: src/{directory}/{filename}")
        for name in FORBIDDEN:
            candidate = SRC / name
            if candidate.is_file() or (candidate.is_dir() and any(candidate.iterdir())):
                errors.append(f"unnecessary architectural layer found: src/{name}")

        legacy_auth = SRC / "auth"
        if legacy_auth.is_dir() and any(legacy_auth.iterdir()):
            errors.append("OIDC integration must live under src/features/auth, not src/auth")

    if errors:
        print("FRONTEND ARCHITECTURE VALIDATION: FAIL")
        for error in errors:
            print(f"- {error}")
        return 1

    print("FRONTEND ARCHITECTURE VALIDATION: PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
