from pathlib import Path
from typing import List, Optional

from nodes import Network, Program, Rule
from simulator import DEFAULT_POLICY, portContains


CHAIN = "INPUT"
COMMAND = "sudo iptables"
TARGETS = {"permitir": "ACCEPT", "bloquear": "DROP"}
BOTH_PROTOCOLS = ("tcp", "udp")
SSH_PORT = 22
COMMENT_LIMIT = 255  # bytes the iptables comment module accepts


def outputPath(mainFile: Path) -> Path:
    return mainFile.with_suffix(".iptables.txt")


def quote(text: str) -> str:
    # Single quotes stop the shell from expanding $ or ` inside a comment.
    return "'" + text.replace("'", "'\\''") + "'"


def limitComment(text: str) -> str:
    return text.encode("utf-8")[:COMMENT_LIMIT].decode("utf-8", "ignore")


def portText(rule: Rule) -> str:
    if rule.port.type == "range":
        return f"{rule.port.fromValue}:{rule.port.toValue}"
    return str(rule.port.value)


def isAnyAddress(network: Optional[Network]) -> bool:
    # cualquiera and 0.0.0.0/0 both arrive as a network without a prefix.
    return network is None or network.mask == 0


def ruleProtocols(rule: Rule) -> List[Optional[str]]:
    if rule.protocol is not None:
        return [rule.protocol]
    # iptables only accepts --dport after -p, so a port without a protocol needs one rule for each.
    return list(BOTH_PROTOCOLS) if rule.port is not None else [None]


def ruleCommands(rule: Rule) -> List[str]:
    commands = []
    for protocol in ruleProtocols(rule):
        parts = [COMMAND, "-A", CHAIN]
        if protocol is not None:
            parts += ["-p", protocol]
        if not isAnyAddress(rule.source):
            parts += ["-s", str(rule.source)]
        if not isAnyAddress(rule.destination):
            parts += ["-d", str(rule.destination)]
        if rule.port is not None:
            parts += ["--dport", portText(rule)]
        if rule.comment is not None:
            parts += ["-m", "comment", "--comment", quote(limitComment(rule.comment))]
        parts += ["-j", TARGETS[rule.action]]
        commands.append(" ".join(parts))
    return commands


def allowsSsh(program: Program, policy: str) -> bool:
    for rule in program.rules:
        if rule.protocol not in (None, "tcp"):
            continue
        if rule.port is not None and not portContains(rule.port, SSH_PORT):
            continue
        if rule.action == "permitir":
            return True
        if isAnyAddress(rule.source) and isAnyAddress(rule.destination):
            return False
    return policy == "permitir"


def exportText(program: Program, sourceName: str) -> str:
    policy = program.defaultPolicy or DEFAULT_POLICY

    lines = [
        f"# Generado por NetWall desde {sourceName}",
        f"# Reemplaza todas las reglas actuales de la cadena {CHAIN}.",
    ]
    if not allowsSsh(program, policy):
        lines += [
            "# ATENCION: estas reglas no permiten SSH (tcp 22).",
            "# Si estas conectado a un servidor remoto, perderas el acceso.",
        ]
    lines += [
        "",
        f"{COMMAND} -P {CHAIN} ACCEPT",
        f"{COMMAND} -F {CHAIN}",
        f"{COMMAND} -A {CHAIN} -i lo -j ACCEPT",
        f"{COMMAND} -A {CHAIN} -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT",
        "",
    ]
    for rule in program.rules:
        lines.append(f"# {rule.name}")
        lines += ruleCommands(rule)
    lines += [
        "",
        "# Politica por defecto, al final para no cortar la conexion mientras se pega.",
        f"{COMMAND} -P {CHAIN} {TARGETS[policy]}",
    ]
    return "\n".join(lines) + "\n"
