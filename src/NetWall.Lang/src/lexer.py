from dataclasses import dataclass
from typing import List, Tuple

from errors import Diagnostic


RESERVED_WORDS = {
    "regla", "accion", "protocolo", "puerto", "origen",
    "destino", "comentario", "politica_defecto", "funcion", "importar",
    "simular", "principal", "si", "sino", "para", "cada", "en",
    "permitir", "bloquear", "tcp", "udp", "rango", "cualquiera",
    "verdadero", "falso", "y", "o", "no",
    "Red", "Int", "Float", "String", "Boolean", "Lista",
}


SYMBOLS = {
    "{": "LBRACE", "}": "RBRACE",
    "(": "LPAREN", ")": "RPAREN",
    "[": "LBRACKET", "]": "RBRACKET",
    ",": "COMMA", ";": "SEMICOLON",
    "=": "EQUALS",
    "<": "LESS", ">": "GREATER", "!": "NOT",
}

OPERATORS = {
    "==": "EQUAL_EQUAL", "!=": "NOT_EQUAL",
    "<=": "LESS_EQUAL", ">=": "GREATER_EQUAL",
    "&&": "AND", "||": "OR",
}


@dataclass
class Token:
    type: str
    value: str
    line: int


def identStart(c: str) -> bool: #starts correctly 
    return c.isalpha()


def identContinue(c: str) -> bool:
    return c.isalnum() or c == "_"


def tokenize(text: str, file: str = "<entrada>") -> Tuple[List[Token], List[Diagnostic]]:
    tokens: List[Token] = []
    diagnostics: List[Diagnostic] = []

    i = 0
    line = 1
    end = len(text)

    while i < end:
        c = text[i]

        if c == "\n":
            line += 1
            i += 1
            continue
        if c.isspace():
            i += 1
            continue

        # omit comments
        if c == "/" and i + 1 < end and text[i + 1] == "/":
            while i < end and text[i] != "\n":
                i += 1
            continue

        if c == '"':
            startLine = line
            j = i + 1
            value = []
            closed = False
            while j < end:
                if text[j] == "\n":
                    break
                if text[j] == '"':
                    closed = True
                    j += 1
                    break
                value.append(text[j])
                j += 1
            if not closed:
                diagnostics.append(Diagnostic(
                    file=file, line=startLine, type="error_lexico",
                    message="cadena de texto sin cerrar (falta '\"')",
                ))
                i = j
                continue
            tokens.append(Token("STRING", "".join(value), startLine))
            i = j
            continue

        if c.isdecimal():
            start = i
            while i < end and text[i].isdecimal():
                i += 1
            if i < end and text[i] == "." and i + 1 < end and text[i + 1].isdecimal(): #float
                i += 1
                while i < end and text[i].isdecimal():
                    i += 1
                tokens.append(Token("FLOAT", text[start:i], line))
            else:
                tokens.append(Token("INT", text[start:i], line))
            continue

        if identStart(c):
            start = i
            while i < end and identContinue(text[i]):
                i += 1
            word = text[start:i]
            if word in RESERVED_WORDS:
                tokens.append(Token("KEYWORD", word, line))
            else:
                tokens.append(Token("IDENT", word, line))
            continue

        if text[i:i + 2] in OPERATORS:
            tokens.append(Token(OPERATORS[text[i:i + 2]], text[i:i + 2], line))
            i += 2
            continue

        if c in SYMBOLS:
            tokens.append(Token(SYMBOLS[c], c, line))
            i += 1
            continue

        #unknown character
        diagnostics.append(Diagnostic( 
            file=file, line=line, type="error_lexico",
            message=f"carácter no reconocido: '{c}'",
        ))
        i += 1

    return tokens, diagnostics
