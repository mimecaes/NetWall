import json
import os
import sys
from pathlib import Path

from loader import Loader
from semantic import check
from analyzer import analyze
from simulator import simulate, simulationData


ERROR_TYPES = {"error_sintactico", "error_semantico"}

DIAGNOSTIC_COLORS = {
    "error_sintactico": "31",
    "error_semantico": "31",
    "advertencia_alcanzabilidad": "33",
    "contradiccion": "35",
    "redundancia": "34",
}

DIM = "90"
LOCATION = "36"
SUCCESS = "32"


def useColor() -> bool:
    if os.environ.get("NO_COLOR"):
        return False
    if os.environ.get("FORCE_COLOR"):
        return True
    return sys.stdout.isatty()


class Painter:
    def __init__(self, enabled: bool):
        self.enabled = enabled

    def color(self, code: str, text: str) -> str:
        return f"\x1b[{code}m{text}\x1b[0m" if self.enabled else text

    def link(self, path: Path, line: int, text: str) -> str:
        if not self.enabled:
            return text
        return f"\x1b]8;;{path.resolve().as_uri()}#L{line}\x1b\\{text}\x1b]8;;\x1b\\"


def displayPath(path: Path) -> str:
    try:
        return os.path.relpath(path)
    except ValueError: 
        return str(path)


def plural(count: int, singular: str, pluralForm: str) -> str:
    return f"{count} {singular if count == 1 else pluralForm}"


def formatDiagnostic(d, path: Path, paint: Painter) -> str:
    isError = d.type in ERROR_TYPES
    color = DIAGNOSTIC_COLORS.get(d.type, "31")
    symbol = "✖" if isError else "▲"
    gutter = " " * len(str(d.line))

    location = paint.link(path, d.line, f"{displayPath(path)}:{d.line}")
    out = [
        f"{paint.color(color, f'{symbol} {d.type}')}: {d.message}",
        f"{gutter} {paint.color(DIM, '┌─')} {paint.color(LOCATION, location)}",
    ]
    if d.relatedRule:
        out.append(f"{gutter} {paint.color(DIM, '=')} relacionado con la regla '{d.relatedRule}'")
    return "\n".join(out)


def formatSummary(errors: int, warnings: int, paint: Painter) -> str:
    if errors:
        text = plural(errors, "error", "errores")
        if warnings:
            text += ", " + plural(warnings, "advertencia", "advertencias")
        return paint.color("31", f"✖ {text}")
    if warnings:
        return paint.color("33", f"▲ compilado con {plural(warnings, 'advertencia', 'advertencias')}")
    return paint.color(SUCCESS, "✔ compilado sin errores")


def printSimulation(path: Path) -> int:
    loader = Loader()
    program = loader.load(path)
    diagnostics = loader.diagnostics + check(program, loader.files)
    if any(d.type in ERROR_TYPES for d in diagnostics):
        print(json.dumps({"errors": True}))
        return 1
    print(json.dumps({"errors": False, **simulationData(program)}, ensure_ascii=False))
    return 0


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")

    if len(sys.argv) < 2:
        print("uso: python cli.py <archivo.nw>")
        return 2

    if "--simulacion" in sys.argv[2:]:
        return printSimulation(Path(sys.argv[1]).resolve())

    paint = Painter(useColor())
    if paint.enabled and os.name == "nt" and sys.stdout.isatty():
        os.system("")

    path = Path(sys.argv[1]).resolve()
    loader = Loader()
    program = loader.load(path)
    semanticDiagnostics = check(program, loader.files)

    diagnostics = loader.diagnostics + semanticDiagnostics
    errors = sum(1 for d in diagnostics if d.type in ERROR_TYPES)
    if not errors:
        diagnostics += analyze(program)
    warnings = len(diagnostics) - errors

    order = {file: index for index, file in enumerate(loader.files)}
    diagnostics.sort(key=lambda d: (order.get(d.file, 0), d.line))
    for d in diagnostics:
        print(formatDiagnostic(d, Path(d.file), paint))
        print()

    for declaration in program.declarations:
        print(declaration.value)

    for rule in program.rules:
        print(rule)

    if program.defaultPolicy is not None:
        print(f"política por defecto: {program.defaultPolicy}")

    if not errors and program.simulations:
        print()
        for result in simulate(program):
            print(result)
    if program.declarations or program.rules or program.defaultPolicy is not None:
        print()
    print(formatSummary(errors, warnings, paint))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
