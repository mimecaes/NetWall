import json
import os
import sys
from pathlib import Path
from typing import List, Tuple

from errors import ERROR_TYPES, Diagnostic
from loader import Loader
from nodes import Program
from semantic import check
from analyzer import analyze
from expander import expand
from exporter import exportText, outputPath
from simulator import DEFAULT_POLICY, packetFrom, simulate, simulationData


MODES = ("compilar", "ejecutar", "exportar")
USAGE = "uso: python cli.py compilar|ejecutar|exportar <archivo.nw>"

DIAGNOSTIC_COLORS = {
    "error_lexico": "31",
    "error_sintactico": "31",
    "error_semantico": "31",
    "advertencia_alcanzabilidad": "33",
    "contradiccion": "35",
    "redundancia": "34",
    "politica_no_definida": "33",
    "advertencia_no_usada": "33",
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

    out = [f"{paint.color(color, f'{symbol} {d.type}')}: {d.message}"]
    if d.line:
        location = paint.link(path, d.line, f"{displayPath(path)}:{d.line}")
        out.append(f"{gutter} {paint.color(DIM, '┌─')} {paint.color(LOCATION, location)}")
    if d.cause:
        out.append(f"{gutter} {paint.color(DIM, '=')} posible causa: {d.cause}")
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


def compileProgram(path: Path) -> Tuple[Loader, Program, List[Diagnostic], int, List[str]]:
    
    loader = Loader()
    if not path.is_file():
        message = f"no se encontró el archivo principal '{displayPath(path)}'"
        diagnostic = Diagnostic(file=str(path), line=0, type="error_semantico", message=message)
        return loader, Program(), [diagnostic], 1, []

    program = loader.load(path)
    diagnostics = loader.diagnostics + check(program, loader.files)
    errors = sum(1 for d in diagnostics if d.type in ERROR_TYPES)
    if errors:
        return loader, program, [d for d in diagnostics if d.type in ERROR_TYPES], errors, []

    expansion = expand(program)
    if expansion.diagnostics:
        return loader, program, expansion.diagnostics, len(expansion.diagnostics), []
    program.rules = expansion.rules
    diagnostics += analyze(program)
    return loader, program, diagnostics, errors, expansion.output


def printSimulation(path: Path, custom: bool) -> int:
    _, program, _, errors, _ = compileProgram(path)
    if errors:
        print(json.dumps({"errors": True}))
        return 1

    rules, packets = program.rules, program.simulations
    if custom:
        request = json.load(sys.stdin)
        selected = set(request.get("rules", []))
        rules = [rule for rule in program.rules if rule.name in selected]
        packets = [packetFrom(packet) for packet in request.get("packets", [])]
        if None in packets:
            print(json.dumps({"errors": True}))
            return 1

    print(json.dumps({"errors": False, **simulationData(program, rules, packets)}, ensure_ascii=False))
    return 0


def challengeValue(value) -> object:
    return value if isinstance(value, (bool, int, float, str)) else str(value)


def printChallenge(path: Path) -> int:
    _, program, diagnostics, errors, _ = compileProgram(path)

    result = {
        "errors": errors > 0,
        "diagnostics": [
            {"type": d.type, "line": d.line, "message": d.message, "cause": d.cause, "file": Path(d.file).name}
            for d in diagnostics
        ],
    }
    if not errors:
        request = json.load(sys.stdin)
        packets = [packetFrom(packet) for packet in request.get("packets", [])]
        if None in packets:
            print(json.dumps({"errors": True, "diagnostics": []}))
            return 1

        result["declarations"] = [
            {"name": d.name, "type": d.dataType, "value": challengeValue(d.value)}
            for d in program.declarations
        ]
        result.update(simulationData(program, program.rules, packets))

    print(json.dumps(result, ensure_ascii=False))
    return 1 if errors else 0


def startPainter() -> Painter:
    paint = Painter(useColor())
    if paint.enabled and os.name == "nt" and sys.stdout.isatty():
        os.system("")
    return paint


def diagnosticLines(loader: Loader, diagnostics: List[Diagnostic], paint: Painter) -> List[str]:
    order = {file: index for index, file in enumerate(loader.files)}
    diagnostics.sort(key=lambda d: (order.get(d.file, 0), d.line))
    if not diagnostics:
        return []
    return ["\n\n".join(formatDiagnostic(d, Path(d.file), paint) for d in diagnostics)]


def printExport(path: Path) -> int:
    paint = startPainter()
    loader, program, diagnostics, errors, output = compileProgram(path)
    warnings = len(diagnostics) - errors

    lines = list(output)
    if output and diagnostics:
        lines.append("")
    lines += diagnosticLines(loader, diagnostics, paint)

    if not errors:
        target = outputPath(path)
        try:
            with target.open("w", encoding="utf-8", newline="\n") as file:
                file.write(exportText(program, path.name))
        except OSError:
            print(paint.color("31", f"✖ no se pudo escribir '{displayPath(target)}'"))
            return 1

        if lines:
            lines.append("")
        location = paint.link(target, 1, displayPath(target))
        lines.append(f"exportado: {paint.color(LOCATION, location)}")

    if lines:
        print("\n".join(lines))
        print()
    print(formatSummary(errors, warnings, paint))
    return 1 if errors else 0


def printProgram(path: Path, execute: bool) -> int:
    paint = startPainter()

    loader, program, diagnostics, errors, output = compileProgram(path)
    warnings = len(diagnostics) - errors

    lines = list(output)
    if output and (diagnostics or program.rules or execute):
        lines.append("")
    lines += diagnosticLines(loader, diagnostics, paint)

    # with errors there is no list of rules and principal block doesn't run
    if not errors:
        if program.rules:
            lines.append("reglas (orden final):")
            lines += [f"  {rule}" for rule in program.rules]
        if execute:
            if program.defaultPolicy is not None:
                lines.append(f"política por defecto: {program.defaultPolicy}")
            else:
                lines.append(f"política por defecto: {DEFAULT_POLICY} (no definida)")
            if program.mainStatements:
                lines.append("")
                lines += simulate(program)

    if lines:
        print("\n".join(lines))
        print()
    print(formatSummary(errors, warnings, paint))
    return 1 if errors else 0


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    args = sys.argv[1:]

    if len(args) == 2 and args[0] == "exportar":
        return printExport(Path(args[1]).resolve())

    if len(args) == 2 and args[0] in MODES:
        return printProgram(Path(args[1]).resolve(), args[0] == "ejecutar")

    if len(args) >= 2 and args[0] not in MODES:
        flags = args[1:]
        if "--retos" in flags:
            sys.stdin.reconfigure(encoding="utf-8")
            return printChallenge(Path(args[0]).resolve())
        if "--simulacion" in flags or "--simulacion-personalizada" in flags:
            sys.stdin.reconfigure(encoding="utf-8")
            return printSimulation(Path(args[0]).resolve(), "--simulacion-personalizada" in flags)

    print(USAGE)
    return 2


if __name__ == "__main__":
    sys.exit(main())
