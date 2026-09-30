import ipaddress
from typing import Dict, List, Optional, Tuple

from errors import Diagnostic
from nodes import Declaration, Identifier, Network, Port, Program, Rule


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


def checkPort(port: Port) -> Optional[str]:
    values = [port.value] if port.type == "value" else [port.fromValue, port.toValue]
    for value in values:
        if not 0 <= value <= 65535:
            return f"el puerto {value} está fuera del rango 0-65535"
    if port.type == "range" and port.fromValue > port.toValue:
        return f"el rango {port.fromValue}-{port.toValue} es inválido: el inicio es mayor que el final"
    return None


def checkAddress(value, symbols: Dict[str, Declaration], line: int) -> Tuple[object, Optional[str]]:
    if isinstance(value, Identifier):
        referenced = symbols.get(value.name)
        if referenced is None or referenced.line > line:
            return value, f"'{value.name}' no está declarada"
        if referenced.dataType != "Red":
            return value, f"'{value.name}' es de tipo {referenced.dataType}, se esperaba Red"
        return referenced.value, None
    if isinstance(value, str):
        network = parseNetwork(value)
        if network is None:
            return value, f"'{value}' no es una red válida"
        return network, None
    return value, None


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

    seenRules: Dict[str, Rule] = {}

    for rule in program.rules:
        previous = seenRules.get(rule.name)
        if previous is not None:
            diagnostics.append(Diagnostic(
                file=file, line=rule.line, type="error_semantico",
                message=f"la regla '{rule.name}' ya fue definida en la línea {previous.line}",
            ))
        else:
            seenRules[rule.name] = rule

        if rule.action is None:
            diagnostics.append(Diagnostic(
                file=file, line=rule.line, type="error_semantico",
                message=f"a la regla '{rule.name}' le falta el campo 'accion'",
            ))

        if rule.port is not None:
            message = checkPort(rule.port)
            if message is not None:
                diagnostics.append(Diagnostic(file=file, line=rule.port.line, type="error_semantico", message=message))

        line = rule.fieldLines.get("origen", rule.line)
        rule.source, message = checkAddress(rule.source, symbols, line)
        if message is not None:
            diagnostics.append(Diagnostic(file=file, line=line, type="error_semantico", message=message))

        line = rule.fieldLines.get("destino", rule.line)
        rule.destination, message = checkAddress(rule.destination, symbols, line)
        if message is not None:
            diagnostics.append(Diagnostic(file=file, line=line, type="error_semantico", message=message))

    return diagnostics
