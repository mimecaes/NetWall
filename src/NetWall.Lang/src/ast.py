from dataclasses import dataclass, field
from typing import List, Optional, Union


@dataclass
class Network:
    cidr: str
    base_int: int
    mask: int


@dataclass
class Port:
    type: str  # if between value or range
    value: Optional[int] = None
    from_: Optional[int] = None
    to: Optional[int] = None


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


@dataclass
class Declaration:
    name: str
    data_type: str
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
    default_policy: Optional[str] = None
    simulations: List[Packet] = field(default_factory=list)
