import ipaddress
from typing import Dict, List, Optional, Set, Tuple, Union

from errors import Diagnostic, lineReference
from nodes import BinaryOp, Declaration, Identifier, ListLiteral, ListValue, Network, Port, Program, Rule, UnaryOp


VALUE_TYPES = {str: "String", int: "Int", float: "Float", bool: "Boolean"}
PACKET_FIELDS = ("protocolo", "puerto", "origen", "destino")
NUMERIC_TYPES = {"Int", "Float"}
EQUALITY_TYPES = {"String", "Boolean", "Red"}
LOGICAL_OPERATORS = {"y", "&&", "o", "||"}
EQUALITY_OPERATORS = {"==", "!="}


class ExpressionError(Exception):
    # message is None when the expression uses a declaration that already has an error, so it doesn't report it again
    def __init__(self, message: Optional[str] = None):
        super().__init__(message)
        self.message = message


def isCompatible(dataType: str, valueType: str) -> bool: #int can be assigned to float but not vice versa
    return dataType == valueType or (dataType == "Float" and valueType == "Int")


def parseNetwork(cidr: str) -> Optional[Network]:
    try:
        network = ipaddress.IPv4Network(cidr, strict=False)
    except ValueError:
        return None
    return Network(cidr=cidr, baseInt=int(network.network_address), mask=network.prefixlen)


def lookup(name: str, symbols: Dict[str, Declaration], invalid: Set[str]) -> Declaration:
    referenced = symbols.get(name)
    if referenced is None:
        raise ExpressionError(f"'{name}' no está declarada")
    if name in invalid:
        raise ExpressionError()
    return referenced


def typeOf(expression, symbols: Dict[str, Declaration], invalid: Set[str]) -> str:
    
    if isinstance(expression, (bool, str, int, float)):
        return VALUE_TYPES[type(expression)]
    if isinstance(expression, Identifier):
        return lookup(expression.name, symbols, invalid).dataType
    if isinstance(expression, ListLiteral):
        types = [typeOf(element, symbols, invalid) for element in expression.elements]
        if len(set(types)) > 1:
            raise ExpressionError("los elementos de una lista deben ser del mismo tipo")
        return "Lista"
    if isinstance(expression, UnaryOp):
        operandType = typeOf(expression.operand, symbols, invalid)
        if operandType != "Boolean":
            raise ExpressionError(f"el operador '{expression.operator}' no se puede aplicar a {operandType}")
        return "Boolean"

    left = typeOf(expression.left, symbols, invalid)
    right = typeOf(expression.right, symbols, invalid)
    operator = expression.operator
    if operator in LOGICAL_OPERATORS:
        valid = left == right == "Boolean"
    elif operator in EQUALITY_OPERATORS:
        valid = {left, right} <= NUMERIC_TYPES or (left == right and left in EQUALITY_TYPES)
    else:
        valid = {left, right} <= NUMERIC_TYPES
    if not valid:
        raise ExpressionError(f"el operador '{operator}' no se puede aplicar a {left} y {right}")
    return "Boolean"


def sameValue(left, right) -> bool:
    # two networks are equal if they have the same network address and prefix
    if isinstance(left, Network) and isinstance(right, Network):
        return (left.baseInt, left.mask) == (right.baseInt, right.mask)
    return left == right


def evaluate(expression, symbols: Dict[str, Declaration], invalid: Set[str]):
    if isinstance(expression, Identifier):
        return symbols[expression.name].value
    if isinstance(expression, ListLiteral):
        elements = [evaluate(element, symbols, invalid) for element in expression.elements]
        elementType = typeOf(expression.elements[0], symbols, invalid) if elements else None
        return ListValue(elements, elementType)
    if isinstance(expression, UnaryOp):
        return not evaluate(expression.operand, symbols, invalid)
    if not isinstance(expression, BinaryOp):
        return expression

    operator = expression.operator
    left = evaluate(expression.left, symbols, invalid)
    # the right operand is evaluated only if the left one doesn't decide
    if operator in ("y", "&&"):
        return left and evaluate(expression.right, symbols, invalid)
    if operator in ("o", "||"):
        return left or evaluate(expression.right, symbols, invalid)
    right = evaluate(expression.right, symbols, invalid)
    if operator == "==":
        return sameValue(left, right)
    if operator == "!=":
        return not sameValue(left, right)
    if operator == "<":
        return left < right
    if operator == ">":
        return left > right
    if operator == "<=":
        return left <= right
    return left >= right


def checkDeclaration(declaration: Declaration, symbols: Dict[str, Declaration], invalid: Set[str]):
    value = declaration.value

    if declaration.dataType == "Red" and isinstance(value, str):
        network = parseNetwork(value)
        if network is None:
            raise ExpressionError(f"'{value}' no es una red válida")
        declaration.value = network
        return

    valueType = typeOf(value, symbols, invalid)
    if not isCompatible(declaration.dataType, valueType):
        raise ExpressionError(f"no se puede asignar un valor {valueType} a '{declaration.name}' de tipo {declaration.dataType}")
    declaration.value = evaluate(value, symbols, invalid)
    if declaration.dataType == "Float" and type(declaration.value) is int:
        declaration.value = float(declaration.value)


def declaredBefore(name: str, symbols: Dict[str, Declaration], order: Dict[str, int], file: str, line: int) -> Optional[Declaration]:
    referenced = symbols.get(name)
    if referenced is None or (order.get(referenced.file, 0), referenced.line) > (order.get(file, 0), line):
        return None
    return referenced


def resolvePort(port: Port, symbols: Dict[str, Declaration], invalid: Set[str], order: Dict[str, int], file: str) -> Optional[str]:
    if isinstance(port.value, Identifier):
        name = port.value.name
        referenced = declaredBefore(name, symbols, order, file, port.line)
        if referenced is None:
            return f"'{name}' no está declarada"
        if referenced.dataType != "Int":
            return f"'{name}' es de tipo {referenced.dataType}, se esperaba Int"
        if name in invalid:
            return None  # the declaration already has an error
        port.value = referenced.value
    return checkPort(port)


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
        referenced = declaredBefore(value.name, symbols, order, file, line)
        if referenced is None:
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


def checkNames(program: Program, order: Dict[str, int]) -> Tuple[List[Diagnostic], Set[int]]:
    # variables and rules share one global namespace, so the second declaration of a name is an error
    diagnostics: List[Diagnostic] = []
    duplicates: Set[int] = set()
    first: Dict[str, Union[Declaration, Rule]] = {}
    items = sorted(program.declarations + program.rules, key=lambda item: (order.get(item.file, 0), item.line))

    for item in items:
        previous = first.get(item.name)
        if previous is None:
            first[item.name] = item
            continue
        duplicates.add(id(item))
        reference = lineReference(previous.file, previous.line, item.file)
        if isinstance(item, Rule) and isinstance(previous, Rule):
            message = f"la regla '{item.name}' ya fue definida ({reference})"
        else:
            message = f"'{item.name}' ya fue declarada ({reference})"
        diagnostics.append(Diagnostic(file=item.file, line=item.line, type="error_semantico", message=message))

    return diagnostics, duplicates


def markUsed(value, used: Set[str]):
    if isinstance(value, Identifier):
        used.add(value.name)
    elif isinstance(value, Port):
        markUsed(value.value, used)
    elif isinstance(value, ListLiteral):
        for element in value.elements:
            markUsed(element, used)
    elif isinstance(value, UnaryOp):
        markUsed(value.operand, used)
    elif isinstance(value, BinaryOp):
        markUsed(value.left, used)
        markUsed(value.right, used)


def check(program: Program, files: List[str]) -> List[Diagnostic]:
    symbols: Dict[str, Declaration] = {}
    order = {file: index for index, file in enumerate(files)}
    used: Set[str] = set()
    invalid: Set[str] = set()  # declarations with an error, so their uses don't report it again
    diagnostics, duplicates = checkNames(program, order)

    for declaration in program.declarations:
        if id(declaration) in duplicates:
            continue
        markUsed(declaration.value, used)
        try:
            checkDeclaration(declaration, symbols, invalid)
        except ExpressionError as error:
            invalid.add(declaration.name)
            if error.message is not None:
                diagnostics.append(Diagnostic(
                    file=declaration.file, line=declaration.line, type="error_semantico", message=error.message,
                ))
        symbols[declaration.name] = declaration

    for rule in program.rules:
        if rule.action is None:
            diagnostics.append(Diagnostic(
                file=rule.file, line=rule.line, type="error_semantico",
                message=f"a la regla '{rule.name}' le falta el campo 'accion'",
            ))

        if rule.port is not None:
            markUsed(rule.port, used)
            message = resolvePort(rule.port, symbols, invalid, order, rule.file)
            if message is not None:
                diagnostics.append(Diagnostic(file=rule.file, line=rule.port.line, type="error_semantico", message=message))

        markUsed(rule.source, used)
        markUsed(rule.destination, used)

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
            markUsed(packet.port, used)
            if packet.port.type == "range":
                message = "el puerto de un paquete debe ser un número, no un rango"
            else:
                message = resolvePort(packet.port, symbols, invalid, order, packet.file)
            if message is not None:
                diagnostics.append(Diagnostic(file=packet.file, line=packet.port.line, type="error_semantico", message=message))

        markUsed(packet.source, used)
        markUsed(packet.destination, used)

        line = packet.fieldLines.get("origen", packet.line)
        packet.source, message = checkHost(packet.source, symbols, order, packet.file, line, "origen")
        if message is not None:
            diagnostics.append(Diagnostic(file=packet.file, line=line, type="error_semantico", message=message))

        line = packet.fieldLines.get("destino", packet.line)
        packet.destination, message = checkHost(packet.destination, symbols, order, packet.file, line, "destino")
        if message is not None:
            diagnostics.append(Diagnostic(file=packet.file, line=line, type="error_semantico", message=message))

    mainFile = files[-1] if files else None
    for declaration in symbols.values():
        if declaration.file == mainFile and declaration.name not in used:
            diagnostics.append(Diagnostic(
                file=declaration.file, line=declaration.line, type="advertencia_no_usada",
                message=f"la variable '{declaration.name}' se declara pero nunca se usa",
            ))

    return diagnostics
