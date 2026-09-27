from typing import List, Optional, Tuple

from errors import Diagnostic
from lexer import SYMBOLS, Token
from nodes import Declaration, Program

TYPES = {"String", "Int", "Float"}

TOKEN_DESCRIPTIONS = {name: f"'{symbol}'" for symbol, name in SYMBOLS.items()}
TOKEN_DESCRIPTIONS.update({
    "IDENT": "un nombre",
    "STRING": "una cadena de texto",
    "INT": "un número entero",
    "FLOAT": "un número decimal",
    "KEYWORD": "una palabra reservada",
})


def describeExpected(tokenType: str, value: Optional[str] = None) -> str:
    if value is not None:
        return f"'{value}'"
    return TOKEN_DESCRIPTIONS.get(tokenType, tokenType)


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
            self._error(f"se esperaba {describeExpected(tokenType, value)}")
        return self._advance()

    def _parseType(self) -> str:
        t = self._current()
        if t is None or t.type != "KEYWORD" or t.value not in TYPES:
            self._error("se esperaba un tipo")
        return self._advance().value

    def _parseValue(self):
        t = self._current()
        if t is not None and t.type == "STRING":
            return self._advance().value
        if t is not None and t.type == "INT":
            return int(self._advance().value)
        if t is not None and t.type == "FLOAT":
            return float(self._advance().value)
        self._error("se esperaba un valor")

    def _parseDeclaration(self) -> Declaration: 
        line = self._current().line if self._current() is not None else 0
        self._expect("KEYWORD", "declarar")
        name = self._expect("IDENT").value
        self._expect("COLON")
        dataType = self._parseType()
        self._expect("EQUALS")
        value = self._parseValue()
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
