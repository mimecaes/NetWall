from typing import List

from errors import Diagnostic
from nodes import Program


VALUE_TYPES = {str: "String", int: "Int", float: "Float"}


def isCompatible(dataType: str, valueType: str) -> bool: #float can be assigned to int but not vice versa
    return dataType == valueType or (dataType == "Float" and valueType == "Int")


def check(program: Program, file: str = "<entrada>") -> List[Diagnostic]:
    diagnostics: List[Diagnostic] = []

    for declaration in program.declarations:
        valueType = VALUE_TYPES[type(declaration.value)]
        if not isCompatible(declaration.dataType, valueType):
            diagnostics.append(Diagnostic(
                file=file, line=declaration.line, type="error_semantico",
                message=f"no se puede asignar un valor {valueType} a '{declaration.name}' de tipo {declaration.dataType}",
            ))

    return diagnostics
