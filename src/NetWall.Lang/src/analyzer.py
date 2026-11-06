from typing import Dict, List, Optional, Set, Tuple

from errors import Diagnostic, lineReference
from nodes import Network, Port, Program, Rule
from simulator import networkContains


ANY = Network(cidr="cualquiera", baseInt=0, mask=0)
VERBS = {"permitir": "permite", "bloquear": "bloquea"}


def portBounds(port: Optional[Port]) -> Tuple[int, int]:
    if port is None:
        return 0, 65535
    if port.type == "range":
        return port.fromValue, port.toValue
    return port.value, port.value


def networkCovers(outer: Optional[Network], inner: Optional[Network]) -> bool:
    outer = outer or ANY
    inner = inner or ANY
    return inner.mask >= outer.mask and networkContains(outer, inner)


def covers(outer: Rule, inner: Rule, bounds: Dict[int, Tuple[int, int]]) -> bool:
    if outer.protocol is not None and outer.protocol != inner.protocol:
        return False
    outerLow, outerHigh = bounds[id(outer)]
    innerLow, innerHigh = bounds[id(inner)]
    if not outerLow <= innerLow <= innerHigh <= outerHigh:
        return False
    return networkCovers(outer.source, inner.source) and networkCovers(outer.destination, inner.destination)


def networksOverlap(first: Optional[Network], second: Optional[Network]) -> bool:
    return networkCovers(first, second) or networkCovers(second, first)


def overlaps(first: Rule, second: Rule, bounds: Dict[int, Tuple[int, int]]) -> bool:
    if first.protocol is not None and second.protocol is not None and first.protocol != second.protocol:
        return False
    firstLow, firstHigh = bounds[id(first)]
    secondLow, secondHigh = bounds[id(second)]
    if max(firstLow, secondLow) > min(firstHigh, secondHigh):
        return False
    return networksOverlap(first.source, second.source) and networksOverlap(first.destination, second.destination)


def plural(count: int, singular: str, pluralForm: str) -> str:
    return f"{count} {singular if count == 1 else pluralForm}"


def findCovering(rules: List[Rule], rule: Rule, bounds: Dict[int, Tuple[int, int]]) -> Optional[Rule]:
    for earlier in rules:
        if covers(earlier, rule, bounds):
            return earlier
    return None


def analyze(program: Program) -> List[Diagnostic]:
    diagnostics: List[Diagnostic] = []
    neverApplied: Set[int] = set()
    bounds = {id(rule): portBounds(rule.port) for rule in program.rules}

    for index, rule in enumerate(program.rules):
        earlierRules = program.rules[:index]

        covering = findCovering(earlierRules, rule, bounds)
        if covering is not None:
            if covering.action != rule.action:
                type = "advertencia_alcanzabilidad"
                message = f"la regla '{rule.name}' nunca se aplica: '{covering.name}' ({lineReference(covering.file, covering.line, rule.file)}) la cubre por completo"
            else:
                type = "redundancia"
                message = f"la regla '{rule.name}' es redundante: '{covering.name}' ({lineReference(covering.file, covering.line, rule.file)}) ya {VERBS[rule.action]} todo lo que ella cubre"
            diagnostics.append(Diagnostic(file=rule.file, line=rule.line, type=type, message=message))
            neverApplied.add(index)
            continue

        conflicts = [
            earlier for earlierIndex, earlier in enumerate(earlierRules)
            if earlierIndex not in neverApplied and earlier.action != rule.action
            and overlaps(earlier, rule, bounds) and not covers(rule, earlier, bounds)
        ]
        if conflicts:
            first = conflicts[0]
            others = len(conflicts) - 1
            extra = f" y {plural(others, 'regla más', 'reglas más')}" if others else ""
            diagnostics.append(Diagnostic(
                file=rule.file, line=rule.line, type="contradiccion",
                message=f"la regla '{rule.name}' se cruza con '{first.name}' ({lineReference(first.file, first.line, rule.file)}){extra} con acciones opuestas: en la parte común decide '{first.name}' porque va antes",
            ))

    if program.defaultPolicy is None and program.mainLine is not None:
        diagnostics.append(Diagnostic(
            file=program.mainFile, line=program.mainLine, type="politica_no_definida",
            message="principal no define una política por defecto: se usará bloquear",
        ))

    return diagnostics
