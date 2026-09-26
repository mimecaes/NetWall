from typing import List, Optional, Tuple

from errors import Diagnostic
from lexer import Token
from nodes import Declaration, Program


class _ParseError(Exception): #signals a parse error it returns diagnostics
    pass


class Parser:
    def __init__(self, tokens: List[Token], file: str):
        self.tokens = tokens
        self.pos = 0
        self.file = file
        self.diagnostics: List[Diagnostic] = []

    def _current(self) -> Optional[Token]:
        if self.pos < len(self.tokens):
            return self.tokens[self.pos]
        return None

    def _advance(self) -> Token:
        t = self.tokens[self.pos]
        self.pos += 1
        return t

    def _error(self, message: str):
        t = self._current()
        line = t.line if t is not None else (self.tokens[-1].line if self.tokens else 0)
        self.diagnostics.append(Diagnostic(
            file=self.file, line=line, type="error_sintactico", message=message,
        ))
        raise _ParseError(message)

    def _expect(self, tokenType: str, value: Optional[str] = None) -> Token:
        t = self._current()
        if t is None or t.type != tokenType or (value is not None and t.value != value):
            expected = tokenType if value is None else f"{tokenType} {value!r}"
            self._error(f"se esperaba {expected}")
        return self._advance()

    def _parseDeclaration(self) -> Declaration: 
        line = self._current().line if self._current() is not None else 0
        self._expect("KEYWORD", "declarar")
        name = self._expect("IDENT").value
        self._expect("COLON")
        dataType = self._expect("KEYWORD", "String").value
        self._expect("EQUALS")
        value = self._expect("STRING").value
        self._expect("SEMICOLON")
        return Declaration(name=name, dataType=dataType, value=value, line=line)

    def parseProgram(self) -> Program:
        program = Program()
        while self._current() is not None:
            program.declarations.append(self._parseDeclaration())
        return program


def parse(tokens: List[Token], file: str = "<entrada>") -> Tuple[Program, List[Diagnostic]]:
    parser = Parser(tokens, file)
    try:
        program = parser.parseProgram()
    except _ParseError:
        program = Program()
    return program, parser.diagnostics
