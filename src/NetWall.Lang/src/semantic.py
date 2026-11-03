import ipaddress
from typing import Dict, List, Optional, Set, Tuple, Union

import predefined
from errors import Diagnostic, lineReference
from nodes import BinaryOp, Call, Declaration, FunctionDef, Identifier, ListLiteral, ListValue, Network, Port, Program, Rule, Show, UnaryOp, displayValue
from predefined import BUILTIN_NAMES, SHOW, SIGNATURES


VALUE_TYPES = {str: "String", int: "Int", float: "Float", bool: "Boolean"}
PACKET_FIELDS = ("protocolo", "puerto", "origen", "destino")
NUMERIC_TYPES = {"Int", "Float"}
EQUALITY_TYPES = {"String", "Boolean", "Red"}
LOGICAL_OPERATORS = {"y", "&&", "o", "||"}
EQUALITY_OPERATORS = {"==", "!="}
PARAMETER = object()  # the value of a parameter, which is known only when the call is expanded


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


def toNetwork(text: str) -> Network:
    network = parseNetwork(text)
    if network is None:
        raise ExpressionError(f"'{text}' no es una red válida")
    return network


def lookup(name: str, symbols: Dict[str, Declaration], invalid: Set[str]) -> Declaration:
    referenced = symbols.get(name)
    if referenced is None:
        raise ExpressionError(f"'{name}' no está declarada")
    if name in invalid:
        raise ExpressionError()
    return referenced


def callType(call: Call, symbols: Dict[str, Declaration], invalid: Set[str], functions: Optional[Dict[str, FunctionDef]] = None) -> str:
    name = call.name
    if name == SHOW or name in (functions or {}):
        raise ExpressionError(f"'{name}' no devuelve un valor")
    if name not in SIGNATURES:
        raise ExpressionError(f"la función '{name}' no está declarada")
    parameters, result = SIGNATURES[name]
    if len(call.arguments) != len(parameters):
        expected = f"{len(parameters)} argumento" + ("" if len(parameters) == 1 else "s")
        raise ExpressionError(f"'{name}' espera {expected}, pero recibió {len(call.arguments)}")

    for index, (argument, parameter) in enumerate(zip(call.arguments, parameters), start=1):
        accepted = parameter if isinstance(parameter, tuple) else (parameter,)
        argumentType = typeOf(argument, symbols, invalid, functions)
        if isinstance(argument, str):
            toNetwork(argument)  # every text argument of these functions is an address or a network
            if "Red" in accepted:
                continue  # a network written between quotes
        if argumentType not in accepted:
            raise ExpressionError(f"el argumento {index} de '{name}' debe ser de tipo {accepted[0]}, pero es {argumentType}")
    return result


def typeOf(expression, symbols: Dict[str, Declaration], invalid: Set[str], functions: Optional[Dict[str, FunctionDef]] = None) -> str:
    
    if isinstance(expression, (bool, str, int, float)):
        return VALUE_TYPES[type(expression)]
    if isinstance(expression, Network):
        return "Red" 
    if isinstance(expression, Call):
        return callType(expression, symbols, invalid, functions)
    if isinstance(expression, Identifier):
        return lookup(expression.name, symbols, invalid).dataType
    if isinstance(expression, ListLiteral):
        types = [typeOf(element, symbols, invalid, functions) for element in expression.elements]
        if len(set(types)) > 1:
            raise ExpressionError("los elementos de una lista deben ser del mismo tipo")
        return "Lista"
    if isinstance(expression, UnaryOp):
        operandType = typeOf(expression.operand, symbols, invalid, functions)
        if operandType != "Boolean":
            raise ExpressionError(f"el operador '{expression.operator}' no se puede aplicar a {operandType}")
        return "Boolean"

    left = typeOf(expression.left, symbols, invalid, functions)
    right = typeOf(expression.right, symbols, invalid, functions)
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


def evaluateCall(call: Call, symbols: Dict[str, Declaration], invalid: Set[str]):
    parameters, _ = SIGNATURES[call.name]
    values = []
    for argument, parameter in zip(call.arguments, parameters):
        value = evaluate(argument, symbols, invalid)
        if isinstance(value, str):
            network = toNetwork(value)
            if parameter != "String":
                value = network
        values.append(value)
    return predefined.call(call.name, values)


def evaluate(expression, symbols: Dict[str, Declaration], invalid: Set[str]):
    if isinstance(expression, Call):
        return evaluateCall(expression, symbols, invalid)
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


def checkDeclaration(declaration: Declaration, symbols: Dict[str, Declaration], invalid: Set[str], functions: Dict[str, FunctionDef]):
    value = declaration.value

    if declaration.dataType == "Red" and isinstance(value, str):
        network = parseNetwork(value)
        if network is None:
            raise ExpressionError(f"'{value}' no es una red válida")
        declaration.value = network
        return

    valueType = typeOf(value, symbols, invalid, functions)
    if not isCompatible(declaration.dataType, valueType):
        raise ExpressionError(f"no se puede asignar un valor {valueType} a '{declaration.name}' de tipo {declaration.dataType}")
    declaration.value = evaluate(value, symbols, invalid)
    if declaration.dataType == "Float" and type(declaration.value) is int:
        declaration.value = float(declaration.value)


def showText(show: Show, symbols: Dict[str, Declaration]) -> str:
    return "".join(displayValue(evaluate(argument, symbols, set())) for argument in show.arguments)


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
        if referenced.value is PARAMETER:
            return None
        if name in invalid:
            return None  # the declaration already has an error
        port.value = referenced.value
    return checkPort(port)


def undeclared(expression, symbols: Dict[str, Declaration], order: Dict[str, int], file: str, line: int) -> Optional[str]:
    # the first name in the expression that isn't declared before that line
    if isinstance(expression, Identifier):
        return None if declaredBefore(expression.name, symbols, order, file, line) else expression.name
    if isinstance(expression, Call):
        children = expression.arguments
    elif isinstance(expression, ListLiteral):
        children = expression.elements
    elif isinstance(expression, UnaryOp):
        children = [expression.operand]
    elif isinstance(expression, BinaryOp):
        children = [expression.left, expression.right]
    else:
        return None
    for child in children:
        name = undeclared(child, symbols, order, file, line)
        if name is not None:
            return name
    return None


def checkArgument(argument, symbols: Dict[str, Declaration], invalid: Set[str], order: Dict[str, int], functions: Dict[str, FunctionDef], file: str, line: int) -> str:
    name = undeclared(argument, symbols, order, file, line)
    if name is not None:
        raise ExpressionError(f"'{name}' no está declarada")
    return typeOf(argument, symbols, invalid, functions)


def checkShow(show: Show, symbols: Dict[str, Declaration], invalid: Set[str], order: Dict[str, int], functions: Dict[str, FunctionDef], inFunction: bool = False):
    for argument in show.arguments:
        checkArgument(argument, symbols, invalid, order, functions, show.file, show.line)
    if not inFunction:  # inside a function the parameters have a value only when the call is expanded
        for argument in show.arguments:
            evaluate(argument, symbols, invalid)


def declaredFunction(call: Call, functions: Dict[str, FunctionDef], order: Dict[str, int]) -> Optional[FunctionDef]:
    function = functions.get(call.name)
    if function is None or (order.get(function.file, 0), function.line) > (order.get(call.file, 0), call.line):
        return None
    return function


def checkCall(call: Call, symbols: Dict[str, Declaration], invalid: Set[str], order: Dict[str, int], functions: Dict[str, FunctionDef], inFunction: bool = False):
    # a call written as an instruction, at the top level or inside a function
    if call.name in SIGNATURES:
        raise ExpressionError(f"el resultado de '{call.name}' no se usa")
    function = declaredFunction(call, functions, order)
    if function is None:
        raise ExpressionError(f"la función '{call.name}' no está declarada")
    parameters = function.parameters
    if len(call.arguments) != len(parameters):
        expected = f"{len(parameters)} argumento" + ("" if len(parameters) == 1 else "s")
        raise ExpressionError(f"'{call.name}' espera {expected}, pero recibió {len(call.arguments)}")

    for index, (argument, parameter) in enumerate(zip(call.arguments, parameters), start=1):
        argumentType = checkArgument(argument, symbols, invalid, order, functions, call.file, call.line)
        if parameter.dataType == "Red" and isinstance(argument, str):
            toNetwork(argument)  # a network written between quotes
            continue
        if not isCompatible(parameter.dataType, argumentType):
            raise ExpressionError(f"el argumento {index} de '{call.name}' debe ser de tipo {parameter.dataType}, pero es {argumentType}")
    if not inFunction:
        for argument in call.arguments:
            evaluate(argument, symbols, invalid)


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
        if referenced.value is PARAMETER:
            return value, None
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


def checkNames(program: Program, order: Dict[str, int]) -> Tuple[List[Diagnostic], Set[int], Set[int]]:
    # variables, functions and rules share one global namespace, so the second declaration of a name is an error
    diagnostics: List[Diagnostic] = []
    duplicates: Set[int] = set()
    predefinedNames: Set[int] = set()
    first: Dict[str, Union[Declaration, Rule, FunctionDef]] = {}
    items = sorted(program.declarations + program.rules + program.functions, key=lambda item: (order.get(item.file, 0), item.line))

    for item in items:
        if item.name in BUILTIN_NAMES:
            predefinedNames.add(id(item))
            diagnostics.append(Diagnostic(
                file=item.file, line=item.line, type="error_semantico",
                message=f"'{item.name}' es una función predefinida y no puede redefinirse",
            ))
            continue
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

    return diagnostics, duplicates, predefinedNames


def markUsed(value, used: Set[str]):
    if isinstance(value, Identifier):
        used.add(value.name)
    elif isinstance(value, Port):
        markUsed(value.value, used)
    elif isinstance(value, ListLiteral):
        for element in value.elements:
            markUsed(element, used)
    elif isinstance(value, (Call, Show)):
        for argument in value.arguments:
            markUsed(argument, used)
    elif isinstance(value, Rule):
        for fieldValue in (value.port, value.source, value.destination):
            markUsed(fieldValue, used)
    elif isinstance(value, UnaryOp):
        markUsed(value.operand, used)
    elif isinstance(value, BinaryOp):
        markUsed(value.left, used)
        markUsed(value.right, used)


def checkRule(rule: Rule, symbols: Dict[str, Declaration], invalid: Set[str], order: Dict[str, int]) -> List[Diagnostic]:
    diagnostics: List[Diagnostic] = []
    if rule.action is None:
        diagnostics.append(Diagnostic(
            file=rule.file, line=rule.line, type="error_semantico",
            message=f"a la regla '{rule.name}' le falta el campo 'accion'",
        ))

    if rule.port is not None:
        message = resolvePort(rule.port, symbols, invalid, order, rule.file)
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
    return diagnostics


def checkFunctionNames(function: FunctionDef) -> Tuple[List[Diagnostic], Set[str]]:
    diagnostics: List[Diagnostic] = []
    invalid: Set[str] = set()
    seen: Dict[str, int] = {}
    for parameter in function.parameters:
        if parameter.name in BUILTIN_NAMES:
            message = f"'{parameter.name}' es una función predefinida y no puede redefinirse"
            invalid.add(parameter.name)
        elif parameter.name in seen:
            message = f"'{parameter.name}' ya fue declarada (línea {seen[parameter.name]})"
        else:
            seen[parameter.name] = parameter.line
            continue
        diagnostics.append(Diagnostic(file=function.file, line=parameter.line, type="error_semantico", message=message))

    rules: Dict[str, int] = {}
    for statement in function.body:
        if not isinstance(statement, Rule):
            continue
        if statement.name in rules:
            diagnostics.append(Diagnostic(
                file=statement.file, line=statement.line, type="error_semantico",
                message=f"la regla '{statement.name}' ya fue definida (línea {rules[statement.name]})",
            ))
        else:
            rules[statement.name] = statement.line
    return diagnostics, invalid


def checkFunction(function: FunctionDef, symbols: Dict[str, Declaration], invalid: Set[str], order: Dict[str, int], functions: Dict[str, FunctionDef], used: Set[str]) -> List[Diagnostic]:
    diagnostics, invalidParameters = checkFunctionNames(function)
    names = {parameter.name for parameter in function.parameters}
    scope = dict(symbols)
    for parameter in function.parameters:
        scope[parameter.name] = Declaration(parameter.name, parameter.dataType, PARAMETER, parameter.line, function.file)
    scopeInvalid = (invalid - names) | invalidParameters

    localUses: Set[str] = set()
    for statement in function.body:
        markUsed(statement, localUses)
        if isinstance(statement, Rule):
            diagnostics += checkRule(statement, scope, scopeInvalid, order)
            continue
        try:
            if isinstance(statement, Show):
                checkShow(statement, scope, scopeInvalid, order, functions, inFunction=True)
            else:
                checkCall(statement, scope, scopeInvalid, order, functions, inFunction=True)
        except ExpressionError as error:
            if error.message is not None:
                diagnostics.append(Diagnostic(file=statement.file, line=statement.line, type="error_semantico", message=error.message))
    used |= localUses - names
    return diagnostics


def calledFunctions(function: FunctionDef, functions: Dict[str, FunctionDef], order: Dict[str, int]) -> List[Tuple[Call, FunctionDef]]:
    found = []
    for statement in function.body:
        if isinstance(statement, Call):
            target = declaredFunction(statement, functions, order)
            if target is not None:
                found.append((statement, target))
    return found


def checkRecursion(functions: Dict[str, FunctionDef], order: Dict[str, int]) -> List[Diagnostic]:
    diagnostics: List[Diagnostic] = []
    reported: Set[str] = set()
    for start in functions.values():
        stack = [(start, [start.name], None)]
        visited: Set[str] = {start.name}
        while stack:
            function, path, firstCall = stack.pop()
            for call, target in calledFunctions(function, functions, order):
                origin = firstCall or call
                if target is start and not reported & set(path):
                    reported |= set(path)
                    diagnostics.append(Diagnostic(
                        file=origin.file, line=origin.line, type="error_semantico",
                        message=f"la función '{start.name}' se llama a sí misma ({' → '.join(path + [start.name])})",
                    ))
                elif target.name not in visited:
                    visited.add(target.name)
                    stack.append((target, path + [target.name], origin))
    return diagnostics


def check(program: Program, files: List[str]) -> List[Diagnostic]:
    symbols: Dict[str, Declaration] = {}
    order = {file: index for index, file in enumerate(files)}
    used: Set[str] = set()
    invalid: Set[str] = set()  # declarations with an error, so their uses don't report it again
    diagnostics, duplicates, predefinedNames = checkNames(program, order)
    functions = {f.name: f for f in program.functions if id(f) not in duplicates and id(f) not in predefinedNames}

    for declaration in program.declarations:
        if id(declaration) in duplicates:
            continue
        if id(declaration) in predefinedNames:
            invalid.add(declaration.name)
            symbols[declaration.name] = declaration
            continue
        markUsed(declaration.value, used)
        try:
            checkDeclaration(declaration, symbols, invalid, functions)
        except ExpressionError as error:
            invalid.add(declaration.name)
            if error.message is not None:
                diagnostics.append(Diagnostic(
                    file=declaration.file, line=declaration.line, type="error_semantico", message=error.message,
                ))
        symbols[declaration.name] = declaration

    for rule in program.rules:
        markUsed(rule, used)
        diagnostics += checkRule(rule, symbols, invalid, order)

    for function in functions.values():
        diagnostics += checkFunction(function, symbols, invalid, order, functions, used)
    diagnostics += checkRecursion(functions, order)

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

    for statement in program.statements + program.mainStatements:
        if not isinstance(statement, (Call, Show)):
            continue
        markUsed(statement, used)
        try:
            if isinstance(statement, Show):
                checkShow(statement, symbols, invalid, order, functions)
            else:
                checkCall(statement, symbols, invalid, order, functions)
        except ExpressionError as error:
            if error.message is not None:
                diagnostics.append(Diagnostic(file=statement.file, line=statement.line, type="error_semantico", message=error.message))

    mainFile = files[-1] if files else None
    for declaration in symbols.values():
        if declaration.file == mainFile and declaration.name not in used:
            diagnostics.append(Diagnostic(
                file=declaration.file, line=declaration.line, type="advertencia_no_usada",
                message=f"la variable '{declaration.name}' se declara pero nunca se usa",
            ))

    return diagnostics
