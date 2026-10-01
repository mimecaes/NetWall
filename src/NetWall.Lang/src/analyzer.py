from typing import List, Optional, Tuple

from errors import Diagnostic
from nodes import Network, Port, Program, Rule
from simulator import networkContains


ANY = Network(cidr="cualquiera", baseInt=0, mask=0)


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


def covers(outer: Rule, inner: Rule) -> bool:
    if outer.protocol is not None and outer.protocol != inner.protocol:
        return False
    outerLow, outerHigh = portBounds(outer.port)
    innerLow, innerHigh = portBounds(inner.port)
    if not outerLow <= innerLow <= innerHigh <= outerHigh:
        return False
    return networkCovers(outer.source, inner.source) and networkCovers(outer.destination, inner.destination)


def analyze(program: Program, file: str = "<entrada>") -> List[Diagnostic]:
    diagnostics: List[Diagnostic] = []

    for index, rule in enumerate(program.rules):
        for earlier in program.rules[:index]:
            if earlier.action != rule.action and covers(earlier, rule):
                diagnostics.append(Diagnostic(
                    file=file, line=rule.line, type="advertencia_alcanzabilidad",
                    message=f"la regla '{rule.name}' nunca se aplica: '{earlier.name}' (línea {earlier.line}) la cubre por completo",
                ))
                break

    return diagnostics
