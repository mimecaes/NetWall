import os
import sys
from pathlib import Path

from lexer import tokenize
from parser import parse


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


def formatDiagnostic(d, sourceLines, path: Path, paint: Painter) -> str:
    isError = d.type in ERROR_TYPES
    color = DIAGNOSTIC_COLORS.get(d.type, "31")
    symbol = "✖" if isError else "▲"
    gutter = " " * len(str(d.line))

    location = paint.link(path, d.line, f"{displayPath(path)}:{d.line}")
    out = [
        f"{paint.color(color, f'{symbol} {d.type}')}: {d.message}",
        f"{gutter} {paint.color(DIM, '┌─')} {paint.color(LOCATION, location)}",
    ]
    if 1 <= d.line <= len(sourceLines):
        source = sourceLines[d.line - 1].rstrip()
        out.append(f"{paint.color(DIM, f'{d.line} │')} {source}")
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


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")

    if len(sys.argv) < 2:
        print("uso: python cli.py <archivo.nw>")
        return 2

    paint = Painter(useColor())
    if paint.enabled and os.name == "nt" and sys.stdout.isatty():
        os.system("")

    path = Path(sys.argv[1])
    with open(path, encoding="utf-8") as f:
        text = f.read()

    tokens, lexerDiagnostics = tokenize(text, file=str(path))
    program, parserDiagnostics = parse(tokens, file=str(path))

    diagnostics = sorted(lexerDiagnostics + parserDiagnostics, key=lambda d: d.line)
    sourceLines = text.splitlines()
    for d in diagnostics:
        print(formatDiagnostic(d, sourceLines, path, paint))
        print()

    for declaration in program.declarations:
        print(declaration.value)

    errors = sum(1 for d in diagnostics if d.type in ERROR_TYPES)
    warnings = len(diagnostics) - errors
    if program.declarations:
        print()
    print(formatSummary(errors, warnings, paint))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
