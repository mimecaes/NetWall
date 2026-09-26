import sys

from lexer import tokenize
from parser import parse


def main():
    if len(sys.argv) < 2:
        print("uso: python cli.py <archivo.nw>")
        return

    path = sys.argv[1]
    with open(path, encoding="utf-8") as f:
        text = f.read()

    tokens, lexerDiagnostics = tokenize(text, file=path)
    program, parserDiagnostics = parse(tokens, file=path)

    for d in lexerDiagnostics + parserDiagnostics:
        print(d)

    for declaration in program.declarations:
        print(declaration.value)


if __name__ == "__main__":
    main()
