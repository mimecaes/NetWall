from typing import List, Optional

from nodes import Network, Packet, Port, Program, Rule


DEFAULT_POLICY = "bloquear"
RESULTS = {"permitir": "permitido", "bloquear": "bloqueado"}


def networkContains(network: Network, address: Network) -> bool:
    hostBits = 32 - network.mask
    return address.baseInt >> hostBits == network.baseInt >> hostBits


def portContains(port: Port, value: int) -> bool:
    if port.type == "range":
        return port.fromValue <= value <= port.toValue
    return port.value == value


def matches(rule: Rule, packet: Packet) -> bool:
    if rule.protocol is not None and rule.protocol != packet.protocol:
        return False
    if rule.port is not None and not portContains(rule.port, packet.port.value):
        return False
    if rule.source is not None and not networkContains(rule.source, packet.source):
        return False
    if rule.destination is not None and not networkContains(rule.destination, packet.destination):
        return False
    return True


def findRule(rules: List[Rule], packet: Packet) -> Optional[Rule]:
    for rule in rules:
        if matches(rule, packet):
            return rule
    return None


def simulate(program: Program) -> List[str]:
    results = []
    for packet in program.simulations:
        rule = findRule(program.rules, packet)
        if rule is not None:
            decision = f"{RESULTS[rule.action]} por la regla '{rule.name}'"
        elif program.defaultPolicy is not None:
            decision = f"{RESULTS[program.defaultPolicy]} por la política por defecto"
        else:
            decision = f"{RESULTS[DEFAULT_POLICY]} por la política por defecto (no definida)"
        results.append(f"{packet}: {decision}")
    return results
