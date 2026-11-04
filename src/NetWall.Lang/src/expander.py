import copy
from dataclasses import dataclass, field
from typing import Dict, List, Optional

from errors import Diagnostic
from nodes import Call, Declaration, ForEach, FunctionDef, Identifier, If, ListLiteral, ListValue, Port, Program, Rule, Show, displayValue
from semantic import ExpressionError, checkPort, evaluate, isCompatible, showText, sourceText, toNetwork, valueType


COMPILE_PREFIX = "[compilación] "


@dataclass
class Expansion:
    rules: List[Rule] = field(default_factory=list)  # the flat list of rules, in their final order
    output: List[str] = field(default_factory=list)  # lines printed by the top-level mostrar calls while compiling
    diagnostics: List[Diagnostic] = field(default_factory=list)  # errors that depend on the values of a call


@dataclass
class Frame:
    # where a block is expanded: its names, the text of each parameter or loop variable,
    # the values between the brackets of the generated names, and the top-level call it comes from
    scope: Dict[str, Declaration]
    texts: Dict[str, str] = field(default_factory=dict)
    labels: Optional[List[str]] = None
    origin: Optional[Call] = None

    def inner(self, name: str, declaration: Declaration, text: str) -> "Frame":
        scope = dict(self.scope)
        scope[name] = declaration
        return Frame(scope, {**self.texts, name: text}, (self.labels or []) + [text], self.origin)


class Expander:
    def __init__(self, program: Program):
        self.symbols: Dict[str, Declaration] = {declaration.name: declaration for declaration in program.declarations}
        self.functions: Dict[str, FunctionDef] = {function.name: function for function in program.functions}
        self.expansion = Expansion()
        self.names: Dict[str, int] = {}

    def error(self, node, frame: Frame, message: str):
        target = frame.origin or node
        self.expansion.diagnostics.append(Diagnostic(file=target.file, line=target.line, type="error_semantico", message=message))

    def text(self, expression, value, frame: Frame) -> str:
        if isinstance(expression, Identifier):
            return frame.texts.get(expression.name, expression.name)
        return displayValue(value)

    def show(self, show: Show, frame: Frame):
        try:
            self.expansion.output.append(COMPILE_PREFIX + showText(show, frame.scope))
        except ExpressionError as error:
            self.error(show, frame, error.message)

    def call(self, call: Call, frame: Frame):
        function = self.functions[call.name]
        local = Frame(dict(self.symbols), labels=[], origin=frame.origin or call)
        for index, (argument, parameter) in enumerate(zip(call.arguments, function.parameters), start=1):
            try:
                value = evaluate(argument, frame.scope, set())
                if parameter.dataType == "Red" and isinstance(value, str):
                    value = toNetwork(value)
            except ExpressionError as error:
                self.error(call, frame, error.message)
                return
            if not isCompatible(parameter.dataType, valueType(value)):
                self.error(call, frame, f"el argumento {index} de '{call.name}' debe ser de tipo {parameter.dataType}, pero es {valueType(value)}")
                return
            if parameter.dataType == "Float" and type(value) is int:
                value = float(value)
            local.scope[parameter.name] = Declaration(parameter.name, parameter.dataType, value, parameter.line, function.file)
            local.texts[parameter.name] = self.text(argument, value, frame)
            local.labels.append(local.texts[parameter.name])
        self.block(function.body, local)

    def conditional(self, statement: If, frame: Frame):
        for branch in statement.branches:
            try:
                value = evaluate(branch.condition, frame.scope, set())
            except ExpressionError as error:
                self.error(statement, frame, error.message)
                return
            if not isinstance(value, bool):
                self.error(statement, frame, f"la condición de 'si' debe ser de tipo Boolean, pero es {valueType(value)}")
                return
            if value:
                self.block(branch.body, frame)
                return
        if statement.elseBody is not None:
            self.block(statement.elseBody, frame)

    def loop(self, statement: ForEach, frame: Frame):
        try:
            value = evaluate(statement.iterable, frame.scope, set())
        except ExpressionError as error:
            self.error(statement, frame, error.message)
            return
        if not isinstance(value, ListValue):
            self.error(statement, frame, f"'para cada' requiere una lista, pero '{sourceText(statement.iterable)}' es de tipo {valueType(value)}")
            return
        if isinstance(statement.iterable, ListLiteral):
            texts = [self.text(e, v, frame) for e, v in zip(statement.iterable.elements, value.elements)]
        else:
            texts = value.texts or [displayValue(v) for v in value.elements]
        for element, text in zip(value.elements, texts):
            declaration = Declaration(statement.variable, valueType(element), element, statement.line, statement.file)
            self.block(statement.body, frame.inner(statement.variable, declaration, text))

    def fieldValue(self, template: Rule, value, frame: Frame, expected: str):
        
        if not isinstance(value, Identifier):
            return value
        resolved = frame.scope[value.name].value
        if valueType(resolved) != expected:
            self.error(template, frame, f"'{value.name}' es de tipo {valueType(resolved)}, se esperaba {expected}")
        return resolved

    def rule(self, template: Rule, frame: Frame):
        rule = copy.copy(template)
        name = template.name if frame.labels is None else f"{template.name}[{', '.join(frame.labels)}]"
        count = self.names.get(name, 0) + 1
        self.names[name] = count
        rule.name = name if count == 1 else f"{name}#{count}"
        if frame.origin is not None:
            # the generated rule takes the line of the top-level call
            rule.line = frame.origin.line
            rule.file = frame.origin.file
            rule.fieldLines = {}
        if template.port is not None and isinstance(template.port.value, Identifier):
            value = self.fieldValue(template, template.port.value, frame, "Int")
            rule.port = Port(type="value", value=value, line=rule.line)
            message = checkPort(rule.port) if valueType(value) == "Int" else None
            if message is not None:
                self.error(template, frame, message)
        rule.source = self.fieldValue(template, template.source, frame, "Red")
        rule.destination = self.fieldValue(template, template.destination, frame, "Red")
        self.expansion.rules.append(rule)

    def block(self, statements: list, frame: Frame):
        for statement in statements:
            if isinstance(statement, Rule):
                self.rule(statement, frame)
            elif isinstance(statement, Show):
                self.show(statement, frame)
            elif isinstance(statement, Call):
                self.call(statement, frame)
            elif isinstance(statement, If):
                self.conditional(statement, frame)
            elif isinstance(statement, ForEach):
                self.loop(statement, frame)

    def run(self, statements: list) -> Expansion:
        self.block(statements, Frame(self.symbols))
        return self.expansion


def expand(program: Program) -> Expansion:
    return Expander(program).run(program.statements)
