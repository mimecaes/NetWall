import ipaddress
from typing import List, Optional

from errors import Diagnostic
from nodes import Network, Program


VALUE_TYPES = {str: "String", int: "Int", float: "Float"}


def isCompatible(dataType: str, valueType: str) -> bool: #float can be assigned to int but not vice versa
    return dataType == valueType or (dataType == "Float" and valueType == "Int")


def parseNetwork(cidr: str) -> Optional[Network]:
    try:
        network = ipaddress.IPv4Network(cidr, strict=False)
    except ValueError:
        return None
    return Network(cidr=cidr, baseInt=int(network.network_address), mask=network.prefixlen)


def check(program: Program, file: str = "<entrada>") -> List[Diagnostic]:
    diagnostics: List[Diagnostic] = []

    for declaration in program.declarations:
        if declaration.dataType == "Red" and isinstance(declaration.value, str):
            network = parseNetwork(declaration.value)
            if network is None:
                diagnostics.append(Diagnostic(
                    file=file, line=declaration.line, type="error_semantico",
                    message=f"'{declaration.value}' no es una red válida",
                ))
            else:
                declaration.value = network
            continue

        valueType = VALUE_TYPES[type(declaration.value)]
        if not isCompatible(declaration.dataType, valueType):
            diagnostics.append(Diagnostic(
                file=file, line=declaration.line, type="error_semantico",
                message=f"no se puede asignar un valor {valueType} a '{declaration.name}' de tipo {declaration.dataType}",
            ))

    return diagnostics
