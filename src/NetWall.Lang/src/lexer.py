from dataclasses import dataclass
from typing import List, Tuple

from errors import Diagnostic


RESERVED_WORDS = {
    "declarar", "regla", "accion", "protocolo", "puerto", "origen",
    "destino", "comentario", "politica_defecto", "funcion", "importar",
    "simular", "principal", "si", "sino", "para", "hasta",
    "permitir", "bloquear", "tcp", "udp", "rango", "cualquiera",
    "Red", "Int", "Float", "String", "Boolean",
}


SYMBOLS = {
    "{": "LBRACE", "}": "RBRACE",
    "(": "LPAREN", ")": "RPAREN",
    ",": "COMMA", ";": "SEMICOLON", ":": "COLON",
    "=": "EQUALS", ".": "DOT",
}


@dataclass
class Token:
    type: str
    value: str
    line: int


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
                    file=file, line=startLine, type="error_sintactico",
                    message="cadena de texto sin cerrar (falta '\"')",
                ))
                i = j
                continue
            tokens.append(Token("STRING", "".join(value), startLine))
            i = j
            continue

        if c.isdigit():
            start = i
            while i < end and text[i].isdigit():
                i += 1
            if i < end and text[i] == "." and i + 1 < end and text[i + 1].isdigit(): #float
                i += 1
                while i < end and text[i].isdigit():
                    i += 1
                tokens.append(Token("FLOAT", text[start:i], line))
            else:
                tokens.append(Token("INT", text[start:i], line))
            continue

        i += 1

    return tokens, diagnostics
