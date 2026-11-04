import re
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

VALID_TYPES = {
    "error_lexico",
    "error_sintactico",
    "error_semantico",
    "advertencia_alcanzabilidad",
    "contradiccion",
    "redundancia",
    "politica_no_definida",
    "advertencia_no_usada",
}

ERROR_TYPES = {"error_lexico", "error_sintactico", "error_semantico"}

# (message pattern, possible cause)
CAUSES = [
    (r"carácter no reconocido: '.*'",
     "Se usó un símbolo que no pertenece a NetWall, por ejemplo + o #, o un nombre que empieza con guion bajo."),
    (r"cadena de texto sin cerrar \(falta '\"'\)",
     "Falta la comilla de cierre. Las cadenas deben abrirse y cerrarse en la misma línea."),

    (r"se esperaba 'permitir' o 'bloquear'",
     "El valor de accion o de politica_defecto está mal escrito."),
    (r"se esperaba 'tcp' o 'udp'",
     "El protocolo no está soportado o está mal escrito."),
    (r"se esperaba un tipo",
     "El tipo no existe o está mal escrito. Los tipos empiezan con mayúscula: Int, Float, String, Boolean, Red y Lista."),
    (r"se esperaba un valor",
     "Falta el valor después de '=' o en un argumento."),
    (r"se esperaba una red, un nombre o 'cualquiera'",
     "origen o destino no tienen una red entre comillas, una variable de tipo Red ni cualquiera."),
    (r"se esperaba un número entero(, un valor Int)? o 'rango'",
     "El campo puerto tiene un valor que no puede ser un puerto."),
    (r"se esperaba un campo de la regla",
     "El nombre del campo está mal escrito. Los campos válidos son accion, protocolo, puerto, origen, destino y comentario."),
    (r"se esperaba ',' o '}'",
     "Los campos de una regla o de un paquete se separan con coma, no con punto y coma."),
    (r"simular una variable todavía no está soportado",
     "simular requiere el paquete escrito entre llaves, no una variable."),
    (r"se esperaba 'politica_defecto'(, 'simular' o 'mostrar'| o 'simular')",
     "Dentro de principal solo se permiten esas instrucciones."),
    (r"'importar' debe ir al inicio del archivo",
     "Hay otra instrucción antes de este importar. Todos los importar deben ir al inicio."),
    (r"un archivo importado no puede tener un bloque 'principal'",
     "El archivo se importa desde otro y también contiene principal. Solo el archivo principal puede tenerlo."),
    (r"se esperaba una instrucción",
     "La línea no empieza con una instrucción válida: importar, una declaración, regla, funcion, si, para cada, una llamada o principal."),
    (r"el bloque 'principal' debe ser lo último del programa",
     "Hay instrucciones después de principal o el bloque aparece dos veces."),
    (r"se esperaba .*",
     "Falta un símbolo o una palabra en esa posición. Si falta ';', revise el final de la instrucción anterior."),

    (r"el campo '.*' está repetido \(ya aparece en la línea \d+\)",
     "El mismo campo se escribió dos veces en una regla o en un paquete."),
    (r"la política por defecto ya fue definida en la línea \d+",
     "politica_defecto aparece más de una vez dentro de principal."),
    (r"'.*' no está declarada",
     "La variable no existe, está mal escrita o se usa antes de su declaración."),
    (r"'.*' no es una red válida",
     "La red no tiene formato IPv4 o CIDR válido, por ejemplo un octeto mayor que 255 o un prefijo mayor que 32."),
    (r"no se puede asignar un valor \w+ a '.*' de tipo \w+",
     "El valor no coincide con el tipo declarado. La única conversión permitida es de Int a Float."),
    (r"el puerto \d+ está fuera del rango 0-65535",
     "Ese número de puerto no existe."),
    (r"el rango \d+-\d+ es inválido: el inicio es mayor que el final",
     "Los extremos de rango(a, b) están invertidos."),
    (r"'.*' es de tipo \w+, se esperaba Red",
     "origen o destino usan una variable que no es de tipo Red."),
    (r"'.*' es de tipo \w+, se esperaba Int",
     "puerto usa una variable que no es de tipo Int."),
    (r"el (origen|destino) de un paquete debe ser una dirección, no una red",
     "Un paquete de simular necesita una IP concreta, no un rango de direcciones."),
    (r"'.*' ya fue declarada \(.*\)",
     "El nombre ya se usa en este archivo o en un archivo importado. Todos los nombres comparten un único alcance global."),
    (r"la regla '.*' ya fue definida \(.*\)",
     "Dos reglas del proyecto tienen el mismo nombre."),
    (r"a la regla '.*' le falta el campo 'accion'",
     "Toda regla debe indicar si permite o bloquea el tráfico."),
    (r"al paquete le falta el campo '.*'",
     "Un paquete de simular necesita protocolo, puerto, origen y destino."),
    (r"un paquete no puede tener 'accion'",
     "La acción la decide el firewall; el paquete solo describe el tráfico."),
    (r"el puerto de un paquete debe ser un número, no un rango",
     "Un paquete va dirigido a un único puerto."),
    (r"la función '.*' no está declarada",
     "La función no existe, está mal escrita, se llama antes de declararse o falta importar su archivo."),
    (r"'.*' espera \d+ argumentos?(\(s\))?, pero recibió \d+",
     "La llamada tiene argumentos de más o de menos."),
    (r"el argumento \d+ de '.*' debe ser de tipo \w+, pero es \w+",
     "Un argumento no coincide con el tipo del parámetro."),
    (r"'.*' es una función predefinida y no puede redefinirse",
     "Se usó el nombre de una función predefinida para una función, una variable, una regla o un parámetro."),
    (r"'.*' no devuelve un valor",
     "mostrar y las funciones del usuario no devuelven valores: solo imprimen o generan reglas. No pueden usarse al declarar una variable ni dentro de otra expresión."),
    (r"el resultado de '.*' no se usa",
     "Las funciones predefinidas que devuelven un valor se usan al declarar una variable, dentro de mostrar o en una condición."),
    (r"la función '.*' se llama a sí misma \(.*\)",
     "Hay recursión directa o indirecta, que no se permite porque la expansión no terminaría."),
    (r"'.*' no está permitida dentro de (una función|'si'|'para cada')",
     "El cuerpo de una función, un si o un para cada solo admite reglas, llamadas, para cada, si y mostrar. Las declaraciones van a nivel superior."),
    (r"la condición de 'si' debe ser de tipo Boolean, pero es \w+",
     "La condición no es una comparación ni un valor Boolean."),
    (r"'para cada' requiere una lista, pero '.*' es de tipo \w+",
     "Después de en debe ir una lista o una variable de tipo Lista."),
    (r"los elementos de una lista deben ser del mismo tipo",
     "La lista mezcla valores de tipos distintos, por ejemplo números y redes."),
    (r"el operador '.*' no se puede aplicar a \w+ y \w+",
     "Los operandos tienen tipos incompatibles para esa operación (sección 14)."),
    (r"el operador '.*' no se puede aplicar a \w+",
     "no y ! solo se aplican a un valor Boolean."),
    (r"importación circular: .*",
     "Los archivos se importan entre sí. Lo que comparten debe moverse a un tercer archivo que no importe a los demás."),
    (r"no se encontró el archivo principal '.*'",
     "La ruta indicada al compilar desde la línea de comandos no existe."),
    (r"no se encontró el archivo '.*'",
     "La ruta está mal escrita. Las rutas son relativas a la carpeta del archivo que importa."),
    (r"ningún archivo del proyecto contiene un bloque 'principal'",
     "Falta escribir principal { } en el archivo que inicia el programa."),
    (r"más de un archivo contiene un bloque 'principal': .*",
     "Solo puede haber un archivo principal por proyecto."),

    (r"la regla '.*' nunca se aplica: .*",
     "Una regla anterior más amplia con la acción opuesta va primero. Si esta regla es una excepción, debe escribirse antes."),
    (r"la regla '.*' es redundante: .*",
     "Una regla anterior más amplia ya incluye este tráfico con la misma acción. La regla puede eliminarse."),
    (r"la regla '.*' se cruza con '(?P<p>[^']*)' .*",
     "Si esta regla debe decidir en la parte común, debe escribirse antes que '{p}'."),
    (r"principal no define una política por defecto: se usará bloquear",
     "Falta politica_defecto dentro de principal. Todo el tráfico que no coincida con ninguna regla se bloqueará."),
    (r"la variable '.*' se declara pero nunca se usa",
     "La variable no se usa en ninguna regla, paquete, llamada, mostrar ni otra declaración. Puede eliminarse."),
]

CAUSE_PATTERNS = [(re.compile(pattern), cause) for pattern, cause in CAUSES]


def causeFor(message: str) -> Optional[str]:
    for pattern, cause in CAUSE_PATTERNS:
        match = pattern.fullmatch(message)
        if match:
            values = match.groupdict()
            return cause.format(**values) if values else cause
    return None


def lineReference(file: str, line: int, fromFile: str) -> str:
    if file == fromFile:
        return f"línea {line}"
    return f"{Path(file).name}:{line}"


@dataclass
class Diagnostic:
    file: str
    line: int  # 0 when the diagnostic has no location in a file
    type: str
    message: str
    cause: Optional[str] = None

    def __post_init__(self):
        if self.type not in VALID_TYPES:
            raise ValueError(f"invalid diagnostic type: {self.type}")
        if self.cause is None:
            self.cause = causeFor(self.message)

    def __str__(self):
        return f"{self.file}:{self.line}: [{self.type}] {self.message}"
