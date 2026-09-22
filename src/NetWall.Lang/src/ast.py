from dataclasses import dataclass, field
from typing import List, Optional, Union


@dataclass
class Red:
    cidr: str
    base_int: int 
    mascara: int 


@dataclass
class Puerto:
    tipo: str  #como un if de valor o rango         
    valor: Optional[int] = None   
    desde: Optional[int] = None   
    hasta: Optional[int] = None


@dataclass
class Regla:
    nombre: str
    accion: Optional[str] 
    protocolo: str   
    puerto: Puerto
    origen: Union[Red, str]   
    destino: Optional[Union[Red, str]]
    comentario: Optional[str]  
    linea: int


@dataclass
class Declaracion:
    nombre: str
    tipo_dato: str
    valor: object
    linea: int


@dataclass
class FuncionDef:
    nombre: str
    parametros: List[str]
    cuerpo: list
    linea: int


@dataclass
class Paquete: #para la simulación
    protocolo: str
    puerto: int
    origen: str
    linea: int


@dataclass
class Programa:
    declaraciones: List[Declaracion] = field(default_factory=list)
    reglas: List[Regla] = field(default_factory=list)  
    funciones: List[FuncionDef] = field(default_factory=list)
    importaciones: List[str] = field(default_factory=list)
    politica_defecto: Optional[str] = None  
    simulaciones: List[Paquete] = field(default_factory=list)
