from dataclasses import dataclass, field
from typing import List

from nodes import Program, Rule, Show
from semantic import showText


COMPILE_PREFIX = "[compilación] "


@dataclass
class Expansion:
    rules: List[Rule] = field(default_factory=list)  # the flat list of rules, in their final order
    output: List[str] = field(default_factory=list)  # lines printed by the top-level mostrar calls while compiling


def expand(program: Program) -> Expansion:
    
    symbols = {declaration.name: declaration for declaration in program.declarations}
    expansion = Expansion()
    for statement in program.statements:
        if isinstance(statement, Rule):
            expansion.rules.append(statement)
        elif isinstance(statement, Show):
            expansion.output.append(COMPILE_PREFIX + showText(statement, symbols))
    return expansion
