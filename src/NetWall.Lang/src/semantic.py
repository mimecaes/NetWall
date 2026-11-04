import ipaddress
from typing import Dict, List, Optional, Set, Tuple, Union

import predefined
from errors import Diagnostic, lineReference
from nodes import BinaryOp, Call, Declaration, ForEach, FunctionDef, Identifier, If, ListLiteral, ListValue, Network, Port, Program, Rule, Show, UnaryOp, displayValue, formatValue
from predefined import BUILTIN_NAMES, SHOW, SIGNATURES


VALUE_TYPES = {str: "String", int: "Int", float: "Float", bool: "Boolean"}
PACKET_FIELDS = ("protocolo", "puerto", "origen", "destino")
NUMERIC_TYPES = {"Int", "Float"}
EQUALITY_TYPES = {"String", "Boolean", "Red"}
LOGICAL_OPERATORS = {"y", "&&", "o", "||"}
EQUALITY_OPERATORS = {"==", "!="}
UNKNOWN = "?"  # the type of "para cada" variable over a Lista parameter, known only when the call is expanded
PARAMETER = object()  # the value of a parameter, which is known only when the call is expanded


class ExpressionError(Exception):
    # message is None when the expression uses a declaration that already has an error, so it doesn't report it again
    def __init__(self, message: Optional[str] = None):
        super().__init__(message)
        self.message = message


def isCompatible(dataType: str, valueType: str) -> bool: #int can be assigned to float but not vice versa
    return dataType == valueType or (dataType == "Float" and valueType == "Int") or UNKNOWN in (dataType, valueType)


def valueType(value) -> str:
    if isinstance(value, Network):
        return "Red"
    if isinstance(value, ListValue):
        return "Lista"
    return VALUE_TYPES.get(type(value), UNKNOWN)


def sourceText(expression) -> str:
    # an expression as it is written, for the messages
    if isinstance(expression, Identifier):
        return expression.name
    if isinstance(expression, Call):
        return f"{expression.name}(" + ", ".join(sourceText(a) for a in expression.arguments) + ")"
    if isinstance(expression, ListLiteral):
        return "[" + ", ".join(sourceText(e) for e in expression.elements) + "]"
    if isinstance(expression, UnaryOp):
        separator = "" if expression.operator == "!" else " "
        return f"{expression.operator}{separator}{sourceText(expression.operand)}"
    if isinstance(expression, BinaryOp):
        return f"{sourceText(expression.left)} {expression.operator} {sourceText(expression.right)}"
    return formatValue(expression)


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
        if argumentType not in accepted and argumentType != UNKNOWN:
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
        if len(set(types) - {UNKNOWN}) > 1:
            raise ExpressionError("los elementos de una lista deben ser del mismo tipo")
        return "Lista"
    if isinstance(expression, UnaryOp):
        operandType = typeOf(expression.operand, symbols, invalid, functions)
        if operandType not in ("Boolean", UNKNOWN):
            raise ExpressionError(f"el operador '{expression.operator}' no se puede aplicar a {operandType}")
        return "Boolean"

    left = typeOf(expression.left, symbols, invalid, functions)
    right = typeOf(expression.right, symbols, invalid, functions)
    operator = expression.operator
    if UNKNOWN in (left, right):
        valid = True  # checked when the call is expanded
    elif operator in LOGICAL_OPERATORS:
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
    for index, (argument, parameter) in enumerate(zip(call.arguments, parameters), start=1):
        accepted = parameter if isinstance(parameter, tuple) else (parameter,)
        value = evaluate(argument, symbols, invalid)
        if isinstance(value, str):
            network = toNetwork(value)
            if parameter != "String":
                value = network
        if valueType(value) not in accepted:
            raise ExpressionError(f"el argumento {index} de '{call.name}' debe ser de tipo {accepted[0]}, pero es {valueType(value)}")
        values.append(value)
    return predefined.call(call.name, values)

def evaluate(expression, symbols: Dict[str, Declaration], invalid: Set[str]):
    if isinstance(expression, Call):
        return evaluateCall(expression, symbols, invalid)
    if isinstance(expression, Identifier):
        return symbols[expression.name].value
    if isinstance(expression, ListLiteral):
        elements = [evaluate(element, symbols, invalid) for element in expression.elements]
        elementType = valueType(elements[0]) if elements else None
        texts = [e.name if isinstance(e, Identifier) else displayValue(v) for e, v in zip(expression.elements, elements)]
        return ListValue(elements, elementType, texts)
    if isinstance(expression, UnaryOp):
        operand = evaluate(expression.operand, symbols, invalid)
        if not isinstance(operand, bool):
            raise ExpressionError(f"el operador '{expression.operator}' no se puede aplicar a {valueType(operand)}")
        return not operand
    if not isinstance(expression, BinaryOp):
        return expression

    operator = expression.operator
    left = evaluate(expression.left, symbols, invalid)
    # the right operand is evaluated only if the left one doesn't decide
    if operator in LOGICAL_OPERATORS:
        decided = left is (operator in ("o", "||"))
        right = None if decided else evaluate(expression.right, symbols, invalid)
        if not isinstance(left, bool) or (right is not None and not isinstance(right, bool)):
            raise ExpressionError(f"el operador '{operator}' no se puede aplicar a {valueType(left)} y {valueType(right)}")
        return left if decided else right
    right = evaluate(expression.right, symbols, invalid)
    numeric = all(valueType(value) in NUMERIC_TYPES for value in (left, right))
    if operator not in EQUALITY_OPERATORS and not numeric:
        raise ExpressionError(f"el operador '{operator}' no se puede aplicar a {valueType(left)} y {valueType(right)}")
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
        if referenced.dataType not in ("Int", UNKNOWN):
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


def checkShow(show: Show, symbols: Dict[str, Declaration], invalid: Set[str], order: Dict[str, int], functions: Dict[str, FunctionDef], deferred: bool = False):
    for argument in show.arguments:
        checkArgument(argument, symbols, invalid, order, functions, show.file, show.line)
    if not deferred:  # parameters and loop variables have a value only when they are expanded
        for argument in show.arguments:
            evaluate(argument, symbols, invalid)


def declaredFunction(call: Call, functions: Dict[str, FunctionDef], order: Dict[str, int]) -> Optional[FunctionDef]:
    function = functions.get(call.name)
    if function is None or (order.get(function.file, 0), function.line) > (order.get(call.file, 0), call.line):
        return None
    return function


def checkCall(call: Call, symbols: Dict[str, Declaration], invalid: Set[str], order: Dict[str, int], functions: Dict[str, FunctionDef], deferred: bool = False):
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
    if not deferred:
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
        if referenced.dataType not in ("Red", UNKNOWN):
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


def checkNames(program: Program, order: Dict[str, int]) -> Tuple[List[Diagnostic], Set[int], Set[int], Dict[str, object]]:
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

    return diagnostics, duplicates, predefinedNames, first


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
    return diagnostics, invalid


def checkFunction(function: FunctionDef, symbols: Dict[str, Declaration], invalid: Set[str], order: Dict[str, int], functions: Dict[str, FunctionDef], used: Set[str], globalNames: Dict[str, object]) -> List[Diagnostic]:
    diagnostics, invalidParameters = checkFunctionNames(function)
    names = {parameter.name for parameter in function.parameters}
    scope = dict(symbols)
    for parameter in function.parameters:
        scope[parameter.name] = Declaration(parameter.name, parameter.dataType, PARAMETER, parameter.line, function.file)
    scopeInvalid = (invalid - names) | invalidParameters

    localUses: Set[str] = set()
    checker = BlockChecker(order, functions, globalNames, diagnostics)
    checker.block(function.body, scope, scopeInvalid, localUses, deferred=True, naming="local")
    used |= localUses - names
    return diagnostics


class BlockChecker:
    # checks the instructions of a block
    def __init__(self, order: Dict[str, int], functions: Dict[str, FunctionDef], globalNames: Dict[str, object], diagnostics: List[Diagnostic]):
        self.order = order
        self.functions = functions
        self.globalNames = globalNames
        self.diagnostics = diagnostics

    def error(self, node, line: int, message: Optional[str]):
        if message is not None:
            self.diagnostics.append(Diagnostic(file=node.file, line=line, type="error_semantico", message=message))

    def ruleName(self, rule: Rule, seen: Dict[str, int], naming: str):
        # naming says where the rule is, "top" is the top level, which checkNames already checked
        # "global" is a top-level si, where the rule keeps its name, "local" is a function or a para cada,
        # where the generated name gets brackets
        if naming == "top":
            return
        if rule.name in BUILTIN_NAMES:
            self.error(rule, rule.line, f"'{rule.name}' es una función predefinida y no puede redefinirse")
            return
        previous = self.globalNames.get(rule.name) if naming == "global" else None
        if previous is not None:
            reference = lineReference(previous.file, previous.line, rule.file)
            if isinstance(previous, Rule):
                self.error(rule, rule.line, f"la regla '{rule.name}' ya fue definida ({reference})")
            else:
                self.error(rule, rule.line, f"'{rule.name}' ya fue declarada ({reference})")
        elif rule.name in seen:
            self.error(rule, rule.line, f"la regla '{rule.name}' ya fue definida (línea {seen[rule.name]})")
        else:
            seen[rule.name] = rule.line

    def condition(self, statement: If, branch, scope, invalid: Set[str], used: Set[str], deferred: bool):
        markUsed(branch.condition, used)
        try:
            conditionType = checkArgument(branch.condition, scope, invalid, self.order, self.functions, statement.file, branch.line)
            if conditionType not in ("Boolean", UNKNOWN):
                raise ExpressionError(f"la condición de 'si' debe ser de tipo Boolean, pero es {conditionType}")
            if not deferred:
                evaluate(branch.condition, scope, invalid)
        except ExpressionError as error:
            self.error(statement, branch.line, error.message)

    def loop(self, statement: ForEach, scope, invalid: Set[str], used: Set[str], deferred: bool):
        markUsed(statement.iterable, used)
        elementType = UNKNOWN
        try:
            iterableType = checkArgument(statement.iterable, scope, invalid, self.order, self.functions, statement.file, statement.line)
            if iterableType not in ("Lista", UNKNOWN):
                raise ExpressionError(f"'para cada' requiere una lista, pero '{sourceText(statement.iterable)}' es de tipo {iterableType}")
            elementType = listElementType(statement.iterable, scope, invalid, self.functions)
            if not deferred:
                evaluate(statement.iterable, scope, invalid)
        except ExpressionError as error:
            self.error(statement, statement.line, error.message)

        name = statement.variable
        previous = scope.get(name) or self.functions.get(name) or self.globalNames.get(name)
        if name in BUILTIN_NAMES:
            self.error(statement, statement.line, f"'{name}' es una función predefinida y no puede redefinirse")
        elif previous is not None:
            self.error(statement, statement.line, f"'{name}' ya fue declarada ({lineReference(previous.file, previous.line, statement.file)})")

        # the variable exists only inside the block
        local = dict(scope)
        local[name] = Declaration(name, elementType, PARAMETER, statement.line, statement.file)
        localUses: Set[str] = set()
        self.block(statement.body, local, invalid - {name}, localUses, deferred=True, naming="local")
        used |= localUses - {name}

    def block(self, statements: list, scope, invalid: Set[str], used: Set[str], deferred: bool, naming: str):
        seen: Dict[str, int] = {}
        for statement in statements:
            if isinstance(statement, Rule):
                self.ruleName(statement, seen, naming)
                markUsed(statement, used)
                self.diagnostics.extend(checkRule(statement, scope, invalid, self.order))
            elif isinstance(statement, If):
                inner = "global" if naming in ("top", "global") else "local"
                for branch in statement.branches:
                    self.condition(statement, branch, scope, invalid, used, deferred)
                    self.block(branch.body, scope, invalid, used, deferred, inner)
                if statement.elseBody is not None:
                    self.block(statement.elseBody, scope, invalid, used, deferred, inner)
            elif isinstance(statement, ForEach):
                self.loop(statement, scope, invalid, used, deferred)
            else:
                markUsed(statement, used)
                try:
                    if isinstance(statement, Show):
                        checkShow(statement, scope, invalid, self.order, self.functions, deferred)
                    else:
                        checkCall(statement, scope, invalid, self.order, self.functions, deferred)
                except ExpressionError as error:
                    self.error(statement, statement.line, error.message)


def listElementType(iterable, scope, invalid: Set[str], functions: Dict[str, FunctionDef]) -> str:
    if isinstance(iterable, ListLiteral):
        types = [typeOf(element, scope, invalid, functions) for element in iterable.elements]
        known = [t for t in types if t != UNKNOWN]
        return known[0] if known else UNKNOWN
    if isinstance(iterable, Identifier):
        value = scope[iterable.name].value
        if isinstance(value, ListValue) and value.elementType is not None:
            return value.elementType
    return UNKNOWN


def blockCalls(statements: list):
    for statement in statements:
        if isinstance(statement, Call):
            yield statement
        elif isinstance(statement, If):
            for branch in statement.branches:
                yield from blockCalls(branch.body)
            if statement.elseBody is not None:
                yield from blockCalls(statement.elseBody)
        elif isinstance(statement, ForEach):
            yield from blockCalls(statement.body)

def calledFunctions(function: FunctionDef, functions: Dict[str, FunctionDef], order: Dict[str, int]) -> List[Tuple[Call, FunctionDef]]:
    found = []
    for statement in blockCalls(function.body):
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
    diagnostics, duplicates, predefinedNames, globalNames = checkNames(program, order)
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

    BlockChecker(order, functions, globalNames, diagnostics).block(program.statements, symbols, invalid, used, deferred=False, naming="top")

    for function in functions.values():
        diagnostics += checkFunction(function, symbols, invalid, order, functions, used, globalNames)
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

    for statement in program.mainStatements:
        if not isinstance(statement, Show):
            continue
        markUsed(statement, used)
        try:
            checkShow(statement, symbols, invalid, order, functions)
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
