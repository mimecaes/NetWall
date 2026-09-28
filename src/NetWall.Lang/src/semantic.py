import ipaddress
from typing import Dict, List, Optional

from errors import Diagnostic
from nodes import Declaration, Identifier, Network, Program


VALUE_TYPES = {str: "String", int: "Int", float: "Float"}


def isCompatible(dataType: str, valueType: str) -> bool: #float can be assigned to int but not vice versa
    return dataType == valueType or (dataType == "Float" and valueType == "Int")


def parseNetwork(cidr: str) -> Optional[Network]:
    try:
        network = ipaddress.IPv4Network(cidr, strict=False)
    except ValueError:
        return None
    return Network(cidr=cidr, baseInt=int(network.network_address), mask=network.prefixlen)


def checkDeclaration(declaration: Declaration, symbols: Dict[str, Declaration]) -> Optional[str]:
    value = declaration.value

    if isinstance(value, Identifier):
        referenced = symbols.get(value.name)
        if referenced is None:
            return f"'{value.name}' no está declarada"
        declaration.value = referenced.value
        valueType = referenced.dataType
    elif declaration.dataType == "Red" and isinstance(value, str):
        network = parseNetwork(value)
        if network is None:
            return f"'{value}' no es una red válida"
        declaration.value = network
        return None
    else:
        valueType = VALUE_TYPES[type(value)]

    if not isCompatible(declaration.dataType, valueType):
        return f"no se puede asignar un valor {valueType} a '{declaration.name}' de tipo {declaration.dataType}"
    return None


def check(program: Program, file: str = "<entrada>") -> List[Diagnostic]:
    diagnostics: List[Diagnostic] = []
    symbols: Dict[str, Declaration] = {}

    for declaration in program.declarations:
        previous = symbols.get(declaration.name)
        if previous is not None:
            message = f"'{declaration.name}' ya fue declarada en la línea {previous.line}"
        else:
            message = checkDeclaration(declaration, symbols)
            symbols[declaration.name] = declaration

        if message is not None:
            diagnostics.append(Diagnostic(
                file=file, line=declaration.line, type="error_semantico", message=message,
            ))

    return diagnostics
