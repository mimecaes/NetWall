from typing import List, Optional, Tuple

from errors import Diagnostic
from lexer import SYMBOLS, Token
from nodes import Declaration, Identifier, Network, Port, Program, Rule

TYPES = {"String", "Int", "Float", "Red"}
STATEMENT_KEYWORDS = {"declarar", "regla", "principal"}

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
        self.mainLine: Optional[int] = None
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

    def _report(self, line: int, type: str, message: str):
        self.diagnostics.append(Diagnostic(file=self.file, line=line, type=type, message=message))

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

    def _parseAddress(self):
        if self._check("STRING"):
            return self._advance().value
        if self._check("IDENT"):
            return Identifier(self._advance().value)
        if self._check("KEYWORD", "cualquiera"):
            self._advance()
            return Network(cidr="cualquiera", baseInt=0, mask=0)
        self._error("se esperaba una red, un nombre o 'cualquiera'")

    def _parsePort(self) -> Port:
        if self._check("INT"):
            t = self._advance()
            return Port(type="value", value=int(t.value), line=t.line)
        if self._check("KEYWORD", "rango"):
            line = self._advance().line
            self._expect("LPAREN")
            fromValue = int(self._expect("INT").value)
            self._expect("COMMA")
            toValue = int(self._expect("INT").value)
            self._expect("RPAREN")
            return Port(type="range", fromValue=fromValue, toValue=toValue, line=line)
        self._error("se esperaba un número entero o 'rango'")

    def _parseField(self, fields: dict, lines: dict):
        t = self._current()
        if self._check("KEYWORD", "accion"):
            self._advance()
            self._expect("EQUALS")
            value = self._expectOneOf("permitir", "bloquear").value
        elif self._check("KEYWORD", "protocolo"):
            self._advance()
            self._expect("EQUALS")
            value = self._expectOneOf("tcp", "udp").value
        elif self._check("KEYWORD", "puerto"):
            self._advance()
            self._expect("EQUALS")
            value = self._parsePort()
        elif self._check("KEYWORD", "origen"):
            self._advance()
            self._expect("EQUALS")
            value = self._parseAddress()
        elif self._check("KEYWORD", "destino"):
            self._advance()
            self._expect("EQUALS")
            value = self._parseAddress()
        elif self._check("KEYWORD", "comentario"):
            self._advance()
            self._expect("EQUALS")
            value = self._expect("STRING").value
        else:
            self._error("se esperaba un campo de la regla")
        if t.value in fields:
            self._report(t.line, "error_semantico", f"el campo '{t.value}' está repetido (ya aparece en la línea {lines[t.value]})")
        else:
            fields[t.value] = value
            lines[t.value] = t.line

    def _parseRule(self) -> Rule:
        line = self._current().line
        self._expect("KEYWORD", "regla")
        name = self._expect("IDENT").value
        self._expect("LBRACE")
        fields = {}
        lines = {}
        self._parseField(fields, lines)
        while self._check("COMMA"):
            self._advance()
            if self._check("RBRACE"):
                break
            self._parseField(fields, lines)
        if not self._check("RBRACE"):
            self._error("se esperaba ',' o '}'")
        self._advance()
        self._expect("SEMICOLON")
        return Rule(
            name=name, action=fields.get("accion"), protocol=fields.get("protocolo"),
            port=fields.get("puerto"), source=fields.get("origen"), destination=fields.get("destino"),
            comment=fields.get("comentario"), line=line, fieldLines=lines,
        )

    def _parsePolicy(self, program: Program, policyLine: Optional[int]) -> int:
        line = self._advance().line
        self._expect("EQUALS")
        policy = self._expectOneOf("permitir", "bloquear").value
        self._expect("SEMICOLON")
        if policyLine is not None:
            self._report(line, "error_semantico", f"la política por defecto ya fue definida en la línea {policyLine}")
            return policyLine
        program.defaultPolicy = policy
        return line

    def _parseMain(self, program: Program):
        self.mainLine = self._advance().line
        self._expect("LBRACE")
        policyLine = None
        while not self._check("RBRACE"):
            if self._current() is None:
                self._error("se esperaba '}'")
            self.statementStart = self.pos
            try:
                if self._check("KEYWORD", "politica_defecto"):
                    policyLine = self._parsePolicy(program, policyLine)
                else:
                    self._error("se esperaba 'politica_defecto'")
            except _ParseError:
                self._synchronizeInBlock()
        self._advance()

    def _parseStatement(self, program: Program):
        if self._check("KEYWORD", "declarar"):
            program.declarations.append(self._parseDeclaration())
        elif self._check("KEYWORD", "regla"):
            program.rules.append(self._parseRule())
        elif self._check("KEYWORD", "principal"):
            self._parseMain(program)
        else:
            self._error("se esperaba 'declarar', 'regla' o 'principal'")

    def _synchronize(self):
        while self._current() is not None:
            t = self._current()
            if t.type == "SEMICOLON":
                self._advance()
                return
            if t.type == "KEYWORD" and t.value in STATEMENT_KEYWORDS:
                return
            self._advance()

    def _synchronizeInBlock(self):
        depth = 0
        while self._current() is not None:
            if self._check("LBRACE"):
                depth += 1
            elif self._check("RBRACE"):
                if depth == 0:
                    return
                depth -= 1
            elif self._check("SEMICOLON") and depth == 0:
                self._advance()
                return
            self._advance()

    def parseProgram(self) -> Program:
        program = Program()
        misplacedReported = False
        while self._current() is not None:
            self.statementStart = self.pos
            if self.mainLine is not None and not misplacedReported:
                self._report(self._current().line, "error_sintactico", "el bloque 'principal' debe ser lo último del programa")
                misplacedReported = True
            try:
                self._parseStatement(program)
            except _ParseError:
                self._synchronize()
        if self.mainLine is None:
            line = self.tokens[-1].line if self.tokens else 1
            self._report(line, "error_sintactico", "falta el bloque 'principal'")
        return program


def parse(tokens: List[Token], file: str = "<entrada>") -> Tuple[Program, List[Diagnostic]]:
    parser = Parser(tokens, file)
    program = parser.parseProgram()
    return program, parser.diagnostics
