import ipaddress
from typing import List

from nodes import Network


# name : (parameter types, result type), a tuple of types means that any of them is accepted.
SIGNATURES = {
    "tamano": (("Red",), "Int"),
    "contiene": (("Red", ("Red", "String")), "Boolean"),
    "interseccion": (("Red", "Red"), "Boolean"),
    "mascara": (("Red",), "String"),
    "direccion_red": (("Red",), "String"),
    "broadcast": (("Red",), "String"),
    "binario": (("String",), "String"),
}

SHOW = "mostrar"
BUILTIN_NAMES = set(SIGNATURES) | {SHOW, "rango"}

ALL_BITS = (1 << 32) - 1


def hostMask(network: Network) -> int:
    return (1 << (32 - network.mask)) - 1


def address(value: int) -> str:
    return str(ipaddress.IPv4Address(value))


def contains(outer: Network, inner: Network) -> bool:
    hostBits = 32 - outer.mask
    return inner.mask >= outer.mask and inner.baseInt >> hostBits == outer.baseInt >> hostBits


def size(network: Network) -> int:
    return hostMask(network) + 1


def intersect(first: Network, second: Network) -> bool:
    # two CIDR blocks share an address only if one is inside the other
    return contains(first, second) or contains(second, first)


def mask(network: Network) -> str:
    return address(ALL_BITS ^ hostMask(network))


def networkAddress(network: Network) -> str:
    return address(network.baseInt)


def broadcast(network: Network) -> str:
    return address(network.baseInt | hostMask(network))


def binary(ip: str) -> str:
    # converts the address written in the parameter to the binary format
    written = ipaddress.IPv4Interface(ip).ip
    return ".".join(f"{int(octet):08b}" for octet in str(written).split("."))


FUNCTIONS = {
    "tamano": size,
    "contiene": contains,
    "interseccion": intersect,
    "mascara": mask,
    "direccion_red": networkAddress,
    "broadcast": broadcast,
    "binario": binary,
}


def call(name: str, values: List[object]) -> object:
    return FUNCTIONS[name](*values)
