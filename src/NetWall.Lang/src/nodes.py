from dataclasses import dataclass, field
from typing import Dict, List, Optional, Union


@dataclass
class Network:
    cidr: str
    baseInt: int
    mask: int

    def __str__(self) -> str:
        return self.cidr


@dataclass
class Port:
    type: str  # if between value or range
    value: Optional[int] = None
    fromValue: Optional[int] = None
    toValue: Optional[int] = None
    line: int = 0

    def __str__(self) -> str:
        if self.type == "range":
            return f"{self.fromValue}-{self.toValue}"
        return str(self.value)


@dataclass
class Rule:
    name: str
    action: Optional[str]
    protocol: str
    port: Port
    source: Union[Network, str]
    destination: Optional[Union[Network, str]]
    comment: Optional[str]
    line: int
    fieldLines: Dict[str, int] = field(default_factory=dict)
    file: str = ""

    def __str__(self) -> str:
        text = f"{self.name}: {self.action} {self.protocol} {self.port} de {self.source} a {self.destination}"
        if self.comment is not None:
            text += f' "{self.comment}"'
        return text


@dataclass
class Identifier:
    name: str

    def __str__(self) -> str:
        return self.name


@dataclass
class Declaration:
    name: str
    dataType: str
    value: object
    line: int
    file: str = ""


@dataclass
class Import:
    path: str
    line: int


@dataclass
class FunctionDef:
    name: str
    parameters: List[str]
    body: list
    line: int


@dataclass
class Packet:
    protocol: Optional[str]
    port: Optional[Port]
    source: Optional[Union[Network, str]]
    destination: Optional[Union[Network, str]]
    line: int
    fieldLines: Dict[str, int] = field(default_factory=dict)
    file: str = ""

    def __str__(self) -> str:
        return f"{self.protocol} {self.source} → {self.destination} puerto {self.port}"


@dataclass
class Program:
    declarations: List[Declaration] = field(default_factory=list)
    rules: List[Rule] = field(default_factory=list)
    functions: List[FunctionDef] = field(default_factory=list)
    imports: List[Import] = field(default_factory=list)
    defaultPolicy: Optional[str] = None
    simulations: List[Packet] = field(default_factory=list)
