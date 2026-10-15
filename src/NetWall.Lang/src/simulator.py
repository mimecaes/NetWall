from typing import Dict, List, Optional, Tuple

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


def mismatch(rule: Rule, packet: Packet) -> Optional[str]:
    if rule.protocol is not None and rule.protocol != packet.protocol:
        return f"protocolo {packet.protocol} ≠ {rule.protocol}"
    if rule.port is not None and not portContains(rule.port, packet.port.value):
        if rule.port.type == "range":
            return f"puerto {packet.port} fuera de {rule.port}"
        return f"puerto {packet.port} ≠ {rule.port}"
    if rule.source is not None and not networkContains(rule.source, packet.source):
        return f"origen {packet.source} fuera de {rule.source}"
    if rule.destination is not None and not networkContains(rule.destination, packet.destination):
        return f"destino {packet.destination} fuera de {rule.destination}"
    return None


def trace(rules: List[Rule], packet: Packet) -> Tuple[Optional[Rule], List[Tuple[Rule, str]]]:
    skipped = []
    for rule in rules:
        reason = mismatch(rule, packet)
        if reason is None:
            return rule, skipped
        skipped.append((rule, reason))
    return None, skipped


def address(value) -> Optional[str]:
    return None if value is None else str(value)


def simulationData(program: Program) -> Dict:
    packets = []
    for packet in program.simulations:
        rule, skipped = trace(program.rules, packet)
        packets.append({
            "protocol": packet.protocol,
            "port": packet.port.value,
            "source": str(packet.source),
            "destination": str(packet.destination),
            "line": packet.line,
            "action": rule.action if rule is not None else program.defaultPolicy or DEFAULT_POLICY,
            "rule": rule.name if rule is not None else None,
            "skipped": [{"rule": skippedRule.name, "reason": reason} for skippedRule, reason in skipped],
        })

    return {
        "defaultPolicy": program.defaultPolicy or DEFAULT_POLICY,
        "policyDefined": program.defaultPolicy is not None,
        "networks": [
            {"name": d.name, "cidr": str(d.value)}
            for d in program.declarations if d.dataType == "Red" and isinstance(d.value, Network)
        ],
        "rules": [
            {
                "name": rule.name,
                "action": rule.action,
                "protocol": rule.protocol,
                "port": address(rule.port),
                "source": address(rule.source),
                "destination": address(rule.destination),
            }
            for rule in program.rules
        ],
        "packets": packets,
    }


def simulate(program: Program) -> List[str]:
    results = []
    for packet in program.simulations:
        rule, _ = trace(program.rules, packet)
        if rule is not None:
            decision = f"{RESULTS[rule.action]} por la regla '{rule.name}'"
        elif program.defaultPolicy is not None:
            decision = f"{RESULTS[program.defaultPolicy]} por la política por defecto"
        else:
            decision = f"{RESULTS[DEFAULT_POLICY]} por la política por defecto (no definida)"
        results.append(f"{packet}: {decision}")
    return results
