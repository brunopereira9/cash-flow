"""Validate the executable backend and local-operation obligations for fluxo-caixa."""
from pathlib import Path
import argparse
import sys
import json
import re

ROOT = Path(__file__).resolve().parents[1]
PROCESSES = ("Core", "Summary")
LAYERS = ("Domain", "Application", "Infrastructure", "Api")
FORBIDDEN = {
    "Microsoft.EntityFrameworkCore": ("Domain", "Application"),
    "Microsoft.AspNetCore": ("Domain", "Application"),
    "RabbitMQ": ("Domain", "Application"),
    "Keycloak": ("Domain", "Application"),
}


def source_files(path: Path):
    return [p for p in path.rglob("*") if p.is_file() and p.suffix in {".cs", ".csproj"}]


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="ignore") if path.exists() else ""


def files(path: Path, suffixes=(".cs", ".csproj")):
    return [p for p in path.rglob("*") if p.is_file() and p.suffix in suffixes]


def service_block(compose: str, service: str) -> str:
    match = re.search(rf"(?ms)^  {re.escape(service)}:\n(?:(?!^  \S).)*", compose)
    return match.group(0) if match else ""


def check_structure(violations: list[str]) -> None:
    for process in PROCESSES:
        src = ROOT / "backend" / process / "src"
        missing = [layer for layer in LAYERS if not (src / layer).is_dir()]
        if missing:
            violations.append(f"{process}: missing required src folders: {', '.join(missing)}")
        for marker, forbidden_layers in FORBIDDEN.items():
            for layer in forbidden_layers:
                layer_path = src / layer
                for file in files(layer_path) if layer_path.exists() else []:
                    text = read(file)
                    if marker in text:
                        violations.append(f"{file.relative_to(ROOT)}: {marker} leaks into {layer}; move adapter detail to Infrastructure/Api")

        for layer in ("Domain", "Application"):
            for file in files(src / layer) if (src / layer).exists() else []:
                text = read(file)
                if re.search(r"Infrastructure|Api", text) and file.suffix == ".cs":
                    violations.append(f"{file.relative_to(ROOT)}: core layers cannot depend on Infrastructure/Api")


def check_persistence(violations: list[str]) -> None:
    for process, src in (("Core", core_src), ("Summary", summary_src)):
        migration_dirs = [src / "Infrastructure" / "Migrations"]
        if not migration_dirs[0].is_dir() or not list(migration_dirs[0].glob("*.cs")):
            violations.append(f"{process}: no versioned EF Core Migrations directory found")
        program = next(src.glob("Api/Program.cs"), None)
        runtime_text = read(program) if program else ""
        if "EnsureCreated" in "\n".join(read(p) for p in files(src, (".cs",))):
            violations.append(f"{process}: EnsureCreated is forbidden; use the explicit --migrate command")
        if "--migrate" not in runtime_text or "MigrateAsync" not in runtime_text:
            violations.append(f"{process}: explicit --migrate migration entrypoint is missing")
    compose = read(ROOT / "infra" / "compose" / "compose.yaml")
    for service in ("core-migrations", "summary-migrations"):
        if service not in compose or "--migrate" not in service_block(compose, service):
            violations.append(f"compose/{service}: explicit migration job is missing")


def check_observability(violations: list[str]) -> None:
    for process in PROCESSES:
        src = ROOT / "backend" / process / "src"
        source = "\n".join(read(p) for p in files(src, (".cs",)))
        for marker in ("AddOpenTelemetry", "AddOtlpExporter", "AddOpenTelemetry(logging", "SamplingRatio"):
            if marker not in source:
                violations.append(f"{process}: missing configurable OpenTelemetry marker {marker}")
    collector = read(ROOT / "infra" / "observability" / "otel-collector-config.yaml")
    for pipeline in ("traces:", "metrics:", "logs:"):
        if pipeline not in collector:
            violations.append(f"collector: missing {pipeline} pipeline")
    compose = read(ROOT / "infra" / "compose" / "compose.yaml")
    if compose.count('Telemetry__SamplingRatio: "1.0"') < 2:
        violations.append("compose: Core and Summary must use 100% sampling for local load/test runs")


def check_identity_boundary(violations: list[str]) -> None:
    compose = read(ROOT / "infra" / "compose" / "compose.yaml")
    for marker in ("Keycloak__AdminClientId: cashflow-api", "Keycloak__AdminClientId: cashflow-summary", "Keycloak__UserAdminClientId: cashflow-admin"):
        if marker not in compose:
            violations.append(f"compose: missing distinct service credential marker {marker}")
    source = "\n".join(read(p) for process in PROCESSES for p in files(ROOT / "backend" / process / "src", (".cs",)))
    if "keycloak_db" in source.lower():
        violations.append("backend: Keycloak internal database access is forbidden")
    if "UserAdminClientId" not in read(ROOT / "backend" / "Core" / "src" / "Infrastructure" / "KeycloakAdminClient.cs"):
        violations.append("Core: user administration must use its separate service credential")


def load_realm(violations: list[str]):
    path = ROOT / "infra" / "keycloak" / "cashflow-realm.json"
    try:
        return json.loads(read(path))
    except (json.JSONDecodeError, OSError):
        violations.append("Keycloak seed: cashflow-realm.json is missing or invalid JSON")
        return {}


def check_identity_seed(violations: list[str]) -> None:
    realm = load_realm(violations)
    users = {user.get("username"): user for user in realm.get("users", [])}
    for role in ("admin", "operator", "auditor"):
        user = users.get(f"demo-{role}")
        if not user or user.get("enabled") is not True or user.get("requiredActions") != []:
            violations.append(f"Keycloak seed: demo-{role} must be enabled and require no setup action")
        if user and any(credential.get("temporary") is not False for credential in user.get("credentials", [])):
            violations.append(f"Keycloak seed: demo-{role} must use a non-temporary local-only credential")
    docs = read(ROOT / "infra" / "README.md").lower()
    if "somente para desenvolvimento" not in docs and "only development" not in docs:
        violations.append("infra/README.md: local demo credential risk is not documented")


def check_compose_health(violations: list[str]) -> None:
    compose = read(ROOT / "infra" / "compose" / "compose.yaml")
    for service in ("postgres", "rabbitmq", "keycloak", "core", "summary", "frontend", "collector"):
        block = service_block(compose, service)
        if "healthcheck:" not in block:
            violations.append(f"compose/{service}: missing healthcheck")
    for service in ("core", "summary"):
        if "/readyz" not in service_block(compose, service):
            violations.append(f"compose/{service}: healthcheck must use readiness endpoint /readyz")
    for dependency in ("postgres: { condition: service_healthy }", "rabbitmq: { condition: service_healthy }", "keycloak: { condition: service_healthy }", "collector: { condition: service_healthy }"):
        if dependency not in compose:
            violations.append(f"compose: missing healthy dependency gate {dependency}")
    if "service_completed_successfully" not in compose:
        violations.append("compose: APIs must wait for explicit migration jobs")


def check_event_safety(violations: list[str]) -> None:
    contract = read(ROOT / "contracts" / "events" / "ledger-entry.v1.json")
    if '"LedgerEntryEvent.v1"' not in contract or '"LedgerEntryCreated.v1"' not in contract:
        violations.append("contracts/events: versioned ledger event contract is missing")
    if re.search(r"password|secret|access_token|authorization", contract, re.IGNORECASE):
        violations.append("contracts/events: sensitive credential fields are forbidden")
    for process in PROCESSES:
        for file in files(ROOT / "backend" / process / "src", (".cs",)):
            for line in read(file).splitlines():
                logged = line.split("logger.Log", 1)[1] if "logger.Log" in line else ""
                messages = " ".join(re.findall(r'"([^"\\]*(?:\\.[^"\\]*)*)"', logged))
                if messages and re.search(r"amount|description|payload|token|secret|password", messages, re.IGNORECASE):
                    violations.append(f"{file.relative_to(ROOT)}: telemetry line may expose sensitive/business data")
    docs = read(ROOT / "contracts" / "events" / "README.md").lower()
    if "telemet" not in docs or "segur" not in docs:
        violations.append("contracts/events/README.md: safe telemetry policy is missing")


def check_docs(violations: list[str]) -> None:
    docs = read(ROOT / "infra" / "README.md").lower()
    for term in ("docker compose -f infra/compose/compose.yaml up --build -d --wait", "--migrate", "cashflow-realm.json", "down -v", "18081", "/readyz", "token"):
        if term.lower() not in docs:
            violations.append(f"infra/README.md: missing operational command/port {term}")


core_src = ROOT / "backend" / "Core" / "src"
summary_src = ROOT / "backend" / "Summary" / "src"


def main() -> int:
    parser = argparse.ArgumentParser()
    for flag in ("structure", "persistence", "observability", "identity-boundary", "identity-seed", "compose-health", "event-safety", "docs"):
        parser.add_argument(f"--{flag}", action="store_true")
    args = parser.parse_args()
    selected = [name.replace("_", "-") for name, enabled in vars(args).items() if enabled]
    if not selected:
        selected = ["structure", "persistence", "observability", "identity-boundary", "identity-seed", "compose-health", "event-safety", "docs"]
    checks = {
        "structure": check_structure,
        "persistence": check_persistence,
        "observability": check_observability,
        "identity-boundary": check_identity_boundary,
        "identity-seed": check_identity_seed,
        "compose-health": check_compose_health,
        "event-safety": check_event_safety,
        "docs": check_docs,
    }
    violations: list[str] = []
    for name in selected:
        checks[name](violations)
    if violations:
        print("ARCHITECTURE VALIDATION: FAIL")
        print("\n".join(f"- {item}" for item in violations))
        return 1
    print("ARCHITECTURE VALIDATION: PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
