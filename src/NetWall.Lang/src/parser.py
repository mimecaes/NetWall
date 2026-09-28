from typing import List, Optional, Tuple

from errors import Diagnostic
from lexer import SYMBOLS, Token
from nodes import Declaration, Identifier, Port, Program, Rule

TYPES = {"String", "Int", "Float", "Red"}
STATEMENT_KEYWORDS = {"declarar", "regla"}

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
        self.statementStart = 0
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
        previous = self.tokens[self.pos - 1] if self.pos > self.statementStart else None
        if previous is not None and (t is None or t.line > previous.line):
            line = previous.line
        else:
            line = t.line if t is not None else 0
        self.diagnostics.append(Diagnostic(
            file=self.file, line=line, type="error_sintactico", message=message,
        ))
        raise _ParseError(message)

    def _expect(self, tokenType: str, value: Optional[str] = None) -> Token:
        t = self._current()
        if t is None or t.type != tokenType or (value is not None and t.value != value):
            self._error(f"se esperaba {describeExpected(tokenType, value)}")
        return self._advance()

    def _check(self, tokenType: str, value: Optional[str] = None) -> bool:
        t = self._current()
        return t is not None and t.type == tokenType and (value is None or t.value == value)

    def _expectOneOf(self, *values: str) -> Token:
        t = self._current()
        if t is None or t.type != "KEYWORD" or t.value not in values:
            options = " o ".join(f"'{v}'" for v in values)
            self._error(f"se esperaba {options}")
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
        if t is not None and t.type == "IDENT":
            return Identifier(self._advance().value)
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

    def _parseField(self, fields: dict):
        if self._check("KEYWORD", "accion"):
            self._advance()
            self._expect("EQUALS")
            fields["accion"] = self._expectOneOf("permitir", "bloquear").value
        elif self._check("KEYWORD", "protocolo"):
            self._advance()
            self._expect("EQUALS")
            fields["protocolo"] = self._expectOneOf("tcp", "udp").value
        elif self._check("KEYWORD", "puerto"):
            self._advance()
            self._expect("EQUALS")
            port = self._expect("INT")
            fields["puerto"] = Port(type="value", value=int(port.value), line=port.line)
        else:
            self._error("se esperaba un campo de la regla")

    def _parseRule(self) -> Rule:
        line = self._current().line
        self._expect("KEYWORD", "regla")
        name = self._expect("IDENT").value
        self._expect("LBRACE")
        fields = {}
        self._parseField(fields)
        while self._check("COMMA"):
            self._advance()
            if self._check("RBRACE"):
                break
            self._parseField(fields)
        if not self._check("RBRACE"):
            self._error("se esperaba ',' o '}'")
        self._advance()
        self._expect("SEMICOLON")
        return Rule(
            name=name, action=fields.get("accion"), protocol=fields.get("protocolo"),
            port=fields.get("puerto"), source=None, destination=None, comment=None, line=line,
        )

    def _parseStatement(self, program: Program):
        if self._check("KEYWORD", "declarar"):
            program.declarations.append(self._parseDeclaration())
        elif self._check("KEYWORD", "regla"):
            program.rules.append(self._parseRule())
        else:
            self._error("se esperaba 'declarar' o 'regla'")

    def _synchronize(self):
        while self._current() is not None:
            t = self._current()
            if t.type == "SEMICOLON":
                self._advance()
                return
            if t.type == "KEYWORD" and t.value in STATEMENT_KEYWORDS:
                return
            self._advance()

    def parseProgram(self) -> Program:
        program = Program()
        while self._current() is not None:
            self.statementStart = self.pos
            try:
                self._parseStatement(program)
            except _ParseError:
                self._synchronize()
        return program


def parse(tokens: List[Token], file: str = "<entrada>") -> Tuple[Program, List[Diagnostic]]:
    parser = Parser(tokens, file)
    program = parser.parseProgram()
    return program, parser.diagnostics
