from typing import List, Optional, Tuple

from errors import Diagnostic
from lexer import SYMBOLS, Token
from nodes import BinaryOp, Declaration, Identifier, Import, ListLiteral, Network, Packet, Port, Program, Rule, UnaryOp

TYPES = {"String", "Int", "Float", "Boolean", "Red", "Lista"}
STATEMENT_KEYWORDS = {"importar", "regla", "principal"} | TYPES

OR_OPERATORS = (("KEYWORD", "o"), ("OR", None))
AND_OPERATORS = (("KEYWORD", "y"), ("AND", None))
RELATIONAL_OPERATORS = tuple((name, None) for name in ("EQUAL_EQUAL", "NOT_EQUAL", "LESS", "GREATER", "LESS_EQUAL", "GREATER_EQUAL"))
NOT_OPERATORS = (("KEYWORD", "no"), ("NOT", None))

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
    def __init__(self, tokens: List[Token], file: str, isModule: bool = False):
        self.tokens = tokens
        self.isModule = isModule
        self.pos = 0
        self.statementStart = 0
        self.depth = 0
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
        if t.type == "LBRACE":
            self.depth += 1
        elif t.type == "RBRACE":
            self.depth -= 1
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

    def _peekIsName(self) -> bool:
        # a name, or a reserved word followed by '=' (as in "int y = 3;", where 'y' is reserved)
        following = self.tokens[self.pos + 1:self.pos + 3]
        if not following:
            return False
        if following[0].type == "IDENT":
            return True
        return following[0].type == "KEYWORD" and len(following) > 1 and following[1].type == "EQUALS"

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

    def _matchOperator(self, operators) -> Optional[str]:
        t = self._current()
        for tokenType, value in operators:
            if t is not None and t.type == tokenType and (value is None or t.value == value):
                return self._advance().value
        return None

    def _parseBinary(self, operators, parseOperand):
        # operators of the same level group from left to right
        left = parseOperand()
        operator = self._matchOperator(operators)
        while operator is not None:
            left = BinaryOp(operator, left, parseOperand())
            operator = self._matchOperator(operators)
        return left

    def _parseExpression(self):
        return self._parseBinary(OR_OPERATORS, self._parseAnd)

    def _parseAnd(self):
        return self._parseBinary(AND_OPERATORS, self._parseRelational)

    def _parseRelational(self):
        return self._parseBinary(RELATIONAL_OPERATORS, self._parseUnary)

    def _parseUnary(self):
        operator = self._matchOperator(NOT_OPERATORS)
        if operator is not None:
            return UnaryOp(operator, self._parseUnary())
        return self._parsePrimary()

    def _parseList(self) -> ListLiteral:
        self._expect("LBRACKET")
        elements = []
        if not self._check("RBRACKET"):
            elements.append(self._parseExpression())
            while self._check("COMMA"):
                self._advance()
                elements.append(self._parseExpression())
            if not self._check("RBRACKET"):
                self._error("se esperaba ',' o ']'")
        self._advance()
        return ListLiteral(elements)

    def _parsePrimary(self):
        t = self._current()
        if t is not None and t.type == "STRING":
            return self._advance().value
        if t is not None and t.type == "INT":
            return int(self._advance().value)
        if t is not None and t.type == "FLOAT":
            return float(self._advance().value)
        if t is not None and t.type == "IDENT":
            return Identifier(self._advance().value)
        if t is not None and t.type == "KEYWORD" and t.value in ("verdadero", "falso"):
            return self._advance().value == "verdadero"
        if t is not None and t.type == "LPAREN":
            self._advance()
            expression = self._parseExpression()
            self._expect("RPAREN")
            return expression
        if t is not None and t.type == "LBRACKET":
            return self._parseList()
        self._error("se esperaba un valor")

    def _parseDeclaration(self) -> Declaration: 
        line = self._current().line if self._current() is not None else 0
        dataType = self._parseType()
        name = self._expect("IDENT").value
        self._expect("EQUALS")
        value = self._parseExpression()
        self._expect("SEMICOLON")
        return Declaration(name=name, dataType=dataType, value=value, line=line, file=self.file)

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
        if self._check("IDENT"):
            t = self._advance()
            return Port(type="value", value=Identifier(t.value), line=t.line)
        self._error("se esperaba un número entero, un valor Int o 'rango'")

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

    def _parseFields(self) -> Tuple[dict, dict]:
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
        return fields, lines

    def _parseRule(self) -> Rule:
        line = self._current().line
        self._expect("KEYWORD", "regla")
        name = self._expect("IDENT").value
        fields, lines = self._parseFields()
        self._expect("SEMICOLON")
        return Rule(
            name=name, action=fields.get("accion"), protocol=fields.get("protocolo"),
            port=fields.get("puerto"), source=fields.get("origen"), destination=fields.get("destino"),
            comment=fields.get("comentario"), line=line, fieldLines=lines, file=self.file,
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

    def _parseSimulate(self, program: Program):
        line = self._advance().line
        self._expect("IDENT")
        if not self._check("LBRACE"):
            self._error("simular una variable todavía no está soportado")
        fields, lines = self._parseFields()
        self._expect("SEMICOLON")
        program.simulations.append(Packet(
            protocol=fields.get("protocolo"), port=fields.get("puerto"), source=fields.get("origen"),
            destination=fields.get("destino"), line=line, fieldLines=lines, file=self.file,
        ))

    def _parseMain(self, program: Program):
        self.mainLine = self._advance().line
        self._expect("LBRACE")
        blockDepth = self.depth
        policyLine = None
        while not self._check("RBRACE"):
            if self._current() is None:
                self._error("se esperaba '}'")
            self.statementStart = self.pos
            try:
                if self._check("KEYWORD", "politica_defecto"):
                    policyLine = self._parsePolicy(program, policyLine)
                elif self._check("KEYWORD", "simular"):
                    self._parseSimulate(program)
                else:
                    self._error("se esperaba 'politica_defecto' o 'simular'")
            except _ParseError:
                self._synchronizeInBlock(blockDepth)
        self._advance()

    def _parseImport(self, program: Program):
        line = self._advance().line
        path = self._expect("STRING").value
        self._expect("SEMICOLON")
        program.imports.append(Import(path=path, line=line))

    def _parseStatement(self, program: Program):
        if self._check("KEYWORD", "importar"):
            self._parseImport(program)
        elif self._current().type == "KEYWORD" and self._current().value in TYPES:
            program.declarations.append(self._parseDeclaration())
        elif self._check("KEYWORD", "regla"):
            program.rules.append(self._parseRule())
        elif self._check("KEYWORD", "principal"):
            if self.isModule:
                self._report(self._current().line, "error_sintactico", "un archivo importado no puede tener un bloque 'principal'")
            self._parseMain(program)
        elif self._check("IDENT") and self._peekIsName():
            # two names in a row: a misspelled type such as "int x = 5;"
            line = self._current().line
            self._report(line, "error_sintactico", "se esperaba un tipo")
            self._skipLine(line)
        else:
            self._error("se esperaba una instrucción")

    def _synchronize(self):
        while self._current() is not None:
            t = self._current()
            if t.type == "SEMICOLON":
                self._advance()
                return
            if t.type == "KEYWORD" and t.value in STATEMENT_KEYWORDS:
                return
            self._advance()

    def _skipLine(self, line: int):
        while self._current() is not None and self._current().line == line:
            if self._advance().type == "SEMICOLON":
                return

    def _synchronizeInBlock(self, blockDepth: int):
        while self._current() is not None:
            if self._check("RBRACE") and self.depth == blockDepth:
                return
            if self._check("SEMICOLON") and self.depth == blockDepth:
                self._advance()
                return
            self._advance()

    def parseProgram(self) -> Program:
        program = Program()
        misplacedReported = False
        seenStatement = False
        while self._current() is not None:
            self.statementStart = self.pos
            if self.mainLine is not None and not misplacedReported:
                self._report(self._current().line, "error_sintactico", "el bloque 'principal' debe ser lo último del programa")
                misplacedReported = True
            
            if not self._check("KEYWORD", "importar"):
                seenStatement = True
            elif seenStatement:
                self._report(self._current().line, "error_sintactico", "'importar' debe ir al inicio del archivo")
            try:
                self._parseStatement(program)
            except _ParseError:
                self._synchronize()
        if self.mainLine is None and not self.isModule:
            line = self.tokens[-1].line if self.tokens else 1
            self._report(line, "error_semantico", "ningún archivo del proyecto contiene un bloque 'principal'")
        program.mainLine = self.mainLine
        return program


def parse(tokens: List[Token], file: str = "<entrada>", isModule: bool = False) -> Tuple[Program, List[Diagnostic]]:
    parser = Parser(tokens, file, isModule)
    program = parser.parseProgram()
    return program, parser.diagnostics
