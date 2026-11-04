import copy
from dataclasses import dataclass, field
from typing import Dict, List, Optional

from errors import Diagnostic
from nodes import Call, Declaration, FunctionDef, Identifier, Port, Program, Rule, Show, displayValue
from semantic import ExpressionError, checkPort, evaluate, showText, toNetwork


COMPILE_PREFIX = "[compilación] "


@dataclass
class Expansion:
    rules: List[Rule] = field(default_factory=list)  # the flat list of rules, in their final order
    output: List[str] = field(default_factory=list)  # lines printed by the top-level mostrar calls while compiling
    diagnostics: List[Diagnostic] = field(default_factory=list)  # errors that depend on the values of a call


class Expander:
    def __init__(self, program: Program):
        self.symbols: Dict[str, Declaration] = {declaration.name: declaration for declaration in program.declarations}
        self.functions: Dict[str, FunctionDef] = {function.name: function for function in program.functions}
        self.expansion = Expansion()
        self.names: Dict[str, int] = {}

    def error(self, origin: Call, message: str):
        self.expansion.diagnostics.append(Diagnostic(file=origin.file, line=origin.line, type="error_semantico", message=message))

    def show(self, show: Show, scope: Dict[str, Declaration], origin: Optional[Call]):
        try:
            self.expansion.output.append(COMPILE_PREFIX + showText(show, scope))
        except ExpressionError as error:
            self.error(origin or show, error.message)

    def argumentText(self, argument, value, texts: Dict[str, str]) -> str:
        
        if isinstance(argument, Identifier):
            return texts.get(argument.name, argument.name)
        return displayValue(value)

    def call(self, call: Call, scope: Dict[str, Declaration], texts: Dict[str, str], origin: Call):
        function = self.functions[call.name]
        local = dict(self.symbols)
        localTexts: Dict[str, str] = {}
        for argument, parameter in zip(call.arguments, function.parameters):
            try:
                value = evaluate(argument, scope, set())
                if parameter.dataType == "Red" and isinstance(value, str):
                    value = toNetwork(value)
            except ExpressionError as error:
                self.error(origin, error.message)
                return
            if parameter.dataType == "Float" and type(value) is int:
                value = float(value)
            local[parameter.name] = Declaration(parameter.name, parameter.dataType, value, parameter.line, function.file)
            localTexts[parameter.name] = self.argumentText(argument, value, texts)

        label = ", ".join(localTexts[parameter.name] for parameter in function.parameters)
        for statement in function.body:
            if isinstance(statement, Rule):
                self.rule(statement, local, f"{statement.name}[{label}]", origin)
            elif isinstance(statement, Show):
                self.show(statement, local, origin)
            else:
                self.call(statement, local, localTexts, origin)

    def rule(self, template: Rule, scope: Dict[str, Declaration], name: str, origin: Call):
        # the generated rule takes the line of the top-level call
        rule = copy.copy(template)
        count = self.names.get(name, 0) + 1
        self.names[name] = count
        rule.name = name if count == 1 else f"{name}#{count}"
        rule.line = origin.line
        rule.file = origin.file
        rule.fieldLines = {}
        if template.port is not None and isinstance(template.port.value, Identifier):
            rule.port = Port(type="value", value=scope[template.port.value.name].value, line=origin.line)
            message = checkPort(rule.port)
            if message is not None:
                self.error(origin, message)
        if isinstance(template.source, Identifier):
            rule.source = scope[template.source.name].value
        if isinstance(template.destination, Identifier):
            rule.destination = scope[template.destination.name].value
        self.expansion.rules.append(rule)

    def run(self, statements: list) -> Expansion:
        for statement in statements:
            if isinstance(statement, Rule):
                self.expansion.rules.append(statement)
            elif isinstance(statement, Show):
                self.show(statement, self.symbols, None)
            elif isinstance(statement, Call):
                self.call(statement, self.symbols, {}, statement)
        return self.expansion


def expand(program: Program) -> Expansion:
    return Expander(program).run(program.statements)
