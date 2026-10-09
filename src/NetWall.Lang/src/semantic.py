import ipaddress
from typing import Dict, List, Optional, Tuple

from errors import Diagnostic, lineReference
from nodes import Declaration, Identifier, Network, Port, Program, Rule


VALUE_TYPES = {str: "String", int: "Int", float: "Float"}
PACKET_FIELDS = ("protocolo", "puerto", "origen", "destino")


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


def checkAddress(value, symbols: Dict[str, Declaration], order: Dict[str, int], file: str, line: int) -> Tuple[object, Optional[str]]:
    if isinstance(value, Identifier):
        referenced = symbols.get(value.name)
        if referenced is None or (order.get(referenced.file, 0), referenced.line) > (order.get(file, 0), line):
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


def checkHost(value, symbols: Dict[str, Declaration], order: Dict[str, int], file: str, line: int, fieldName: str) -> Tuple[object, Optional[str]]:
    value, message = checkAddress(value, symbols, order, file, line)
    if message is None and isinstance(value, Network) and value.mask != 32:
        message = f"el {fieldName} de un paquete debe ser una dirección, no una red"
    return value, message


def check(program: Program, files: List[str]) -> List[Diagnostic]:
    diagnostics: List[Diagnostic] = []
    symbols: Dict[str, Declaration] = {}
    order = {file: index for index, file in enumerate(files)}

    for declaration in program.declarations:
        previous = symbols.get(declaration.name)
        if previous is not None:
            message = f"'{declaration.name}' ya fue declarada ({lineReference(previous.file, previous.line, declaration.file)})"
        else:
            message = checkDeclaration(declaration, symbols)
            symbols[declaration.name] = declaration

        if message is not None:
            diagnostics.append(Diagnostic(
                file=declaration.file, line=declaration.line, type="error_semantico", message=message,
            ))

    seenRules: Dict[str, Rule] = {}

    for rule in program.rules:
        previous = seenRules.get(rule.name)
        if previous is not None:
            diagnostics.append(Diagnostic(
                file=rule.file, line=rule.line, type="error_semantico",
                message=f"la regla '{rule.name}' ya fue definida ({lineReference(previous.file, previous.line, rule.file)})",
            ))
        else:
            seenRules[rule.name] = rule

        if rule.action is None:
            diagnostics.append(Diagnostic(
                file=rule.file, line=rule.line, type="error_semantico",
                message=f"a la regla '{rule.name}' le falta el campo 'accion'",
            ))

        if rule.port is not None:
            message = checkPort(rule.port)
            if message is not None:
                diagnostics.append(Diagnostic(file=rule.file, line=rule.port.line, type="error_semantico", message=message))

        line = rule.fieldLines.get("origen", rule.line)
        rule.source, message = checkAddress(rule.source, symbols, order, rule.file, line)
        if message is not None:
            diagnostics.append(Diagnostic(file=rule.file, line=line, type="error_semantico", message=message))

        line = rule.fieldLines.get("destino", rule.line)
        rule.destination, message = checkAddress(rule.destination, symbols, order, rule.file, line)
        if message is not None:
            diagnostics.append(Diagnostic(file=rule.file, line=line, type="error_semantico", message=message))

    for packet in program.simulations:
        for name in PACKET_FIELDS:
            if name not in packet.fieldLines:
                diagnostics.append(Diagnostic(
                    file=packet.file, line=packet.line, type="error_semantico",
                    message=f"al paquete le falta el campo '{name}'",
                ))

        if "accion" in packet.fieldLines:
            diagnostics.append(Diagnostic(
                file=packet.file, line=packet.fieldLines["accion"], type="error_semantico",
                message="un paquete no puede tener 'accion'",
            ))

        if packet.port is not None:
            if packet.port.type == "range":
                message = "el puerto de un paquete debe ser un número, no un rango"
            else:
                message = checkPort(packet.port)
            if message is not None:
                diagnostics.append(Diagnostic(file=packet.file, line=packet.port.line, type="error_semantico", message=message))

        line = packet.fieldLines.get("origen", packet.line)
        packet.source, message = checkHost(packet.source, symbols, order, packet.file, line, "origen")
        if message is not None:
            diagnostics.append(Diagnostic(file=packet.file, line=line, type="error_semantico", message=message))

        line = packet.fieldLines.get("destino", packet.line)
        packet.destination, message = checkHost(packet.destination, symbols, order, packet.file, line, "destino")
        if message is not None:
            diagnostics.append(Diagnostic(file=packet.file, line=line, type="error_semantico", message=message))

    return diagnostics
