from pathlib import Path
from typing import List, Set

from errors import Diagnostic
from lexer import tokenize
from nodes import Program
from parser import parse


class Loader:
    def __init__(self):
        self.diagnostics: List[Diagnostic] = []
        self.files: List[str] = []
        self.loaded: Set[str] = set()
        self.loading: List[str] = []

    def load(self, path: Path, isModule: bool = False) -> Program:
        file = str(path)
        self.loaded.add(file)
        self.loading.append(file)
        text = path.read_text(encoding="utf-8")

        tokens, lexerDiagnostics = tokenize(text, file=file)
        program, parserDiagnostics = parse(tokens, file=file, isModule=isModule)
        self.diagnostics += lexerDiagnostics + parserDiagnostics

        merged = Program()
        for imported in program.imports:
            target = (path.parent / imported.path).resolve()
            if str(target) in self.loading:
                cycle = self.loading[self.loading.index(str(target)):] + [str(target)]
                self.diagnostics.append(Diagnostic(
                    file=file, line=imported.line, type="error_semantico",
                    message="importación circular: " + " → ".join(Path(f).name for f in cycle),
                ))
                continue
            if str(target) in self.loaded:
                continue
            if not target.is_file():
                self.diagnostics.append(Diagnostic(
                    file=file, line=imported.line, type="error_semantico",
                    message=f"no se encontró el archivo '{imported.path}'",
                ))
                continue
            module = self.load(target, isModule=True)
            merged.declarations += module.declarations
            merged.rules += module.rules

        self.loading.pop()
        self.files.append(file)
        merged.declarations += program.declarations
        merged.rules += program.rules
        merged.defaultPolicy = program.defaultPolicy
        merged.simulations = program.simulations
        return merged
