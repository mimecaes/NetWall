from dataclasses import dataclass
from typing import Optional

VALID_TYPES = {
    "error_sintactico",
    "error_semantico",
    "advertencia_alcanzabilidad",
    "contradiccion",
    "redundancia",
}


@dataclass
class Diagnostic:
    file: str
    line: int
    type: str
    message: str
    related_rule: Optional[str] = None

    def __post_init__(self):
        if self.type not in VALID_TYPES:
            raise ValueError(f"invalid diagnostic type: {self.type}")

    def __str__(self):
        base = f"{self.file}:{self.line}: [{self.type}] {self.message}"
        if self.related_rule:
            base += f" (relacionado con '{self.related_rule}')"
        return base
