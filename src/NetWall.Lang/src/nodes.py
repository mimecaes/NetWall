from dataclasses import dataclass, field
from typing import List, Optional, Union


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

    def __str__(self) -> str:
        return f"{self.name}: {self.action} {self.protocol} {self.port}"


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


@dataclass
class FunctionDef:
    name: str
    parameters: List[str]
    body: list
    line: int


@dataclass
class Packet:
    protocol: str
    port: int
    source: str
    line: int


@dataclass
class Program:
    declarations: List[Declaration] = field(default_factory=list)
    rules: List[Rule] = field(default_factory=list)
    functions: List[FunctionDef] = field(default_factory=list)
    imports: List[str] = field(default_factory=list)
    defaultPolicy: Optional[str] = None
    simulations: List[Packet] = field(default_factory=list)
