# NetWall — Gramática formal (EBNF)

Notacion: `::=` define, `|` alternativa (or), `[ ]` opcional, `{ }` cero o más,
literales entre comillas.

## Estructura general

```
programa        ::= { importacion } { declaracion | funcion_decl | regla_decl } bloque_principal

modulo          ::= { importacion } { declaracion | funcion_decl | regla_decl }

importacion     ::= "importar" STRING ";"

declaracion     ::= tipo IDENT "=" expresion ";"

tipo            ::= "Red" | "Int" | "Float" | "String" | "Boolean" | "Lista"
```

## Reglas de firewall

```
regla_decl      ::= "regla" IDENT "{" campo { "," campo } [ "," ] "}" ";"


campo           ::= "accion" "=" accion_valor
                   | "protocolo" "=" protocolo_valor
                   | "puerto" "=" puerto_valor
                   | "origen" "=" valor
                   | "destino" "=" valor
                   | "comentario" "=" STRING

accion_valor    ::= "permitir" | "bloquear"
protocolo_valor ::= "tcp" | "udp"
puerto_valor    ::= INT | IDENT | "rango" "(" INT "," INT ")"
valor           ::= STRING | IDENT | "cualquiera"
```

## Funciones

```
funcion_decl    ::= "funcion" IDENT "(" [ parametros ] ")" "{" { sentencia } "}"
parametros      ::= IDENT { "," IDENT }
```

## Bloque principal

```
bloque_principal ::= "principal" "{" { sentencia_principal } "}"

sentencia_principal ::= politica_stmt
                       | simular_stmt

politica_stmt   ::= "politica_defecto" "=" accion_valor ";"

simular_stmt    ::= "simular" (IDENT | struct_literal) ";"

struct_literal  ::= IDENT "{" campo { "," campo } [ "," ] "}"
```

## Sentencias generales (dentro de funcion / si / para)

```
sentencia       ::= declaracion
                   | asignacion
                   | si_sino
                   | para_stmt
                   | llamada_funcion ";"

asignacion      ::= IDENT "=" expresion ";"

llamada_funcion ::= IDENT "(" [ argumentos ] ")"
argumentos      ::= expresion { "," expresion }
```

## Control de flujo

<!-- falta agregar el resto -->

```
si_sino         ::= "si" "(" expresion ")" "{" { sentencia } "}"
                    [ "sino" "{" { sentencia } "}" ]

para_stmt       ::= "para" "cada" IDENT "en" expresion "{" { sentencia } "}"
```

## Expresiones

Precedencia de mayor a menor: `no` / `!`, relacionales, `y` / `&&`, `o` / `||`. Los operadores del mismo nivel se agrupan de izquierda a derecha.

```
expresion       ::= expr_y { ( "o" | "||" ) expr_y }
expr_y          ::= expr_rel { ( "y" | "&&" ) expr_rel }
expr_rel        ::= expr_no { operador_rel expr_no }
expr_no         ::= ( "no" | "!" ) expr_no | primario
primario        ::= STRING | INT | FLOAT | BOOLEANO | IDENT | llamada_funcion
                   | "(" expresion ")" | lista
lista           ::= "[" [ expresion { "," expresion } ] "]"
operador_rel    ::= "==" | "!=" | "<" | ">" | "<=" | ">="
```

- Todas las operaciones devuelven Boolean.
- `==` y `!=`: Int y Float entre sí, o dos operandos del mismo tipo String, Boolean o Red. Dos Red son iguales si tienen la misma dirección de red y el mismo prefijo.
- `<`, `>`, `<=`, `>=`: solo Int y Float.
- `y`, `o`, `no` (y `&&`, `||`, `!`): solo Boolean. `y` y `o` se evalúan en cortocircuito.
- Una `lista` tiene todos sus elementos del mismo tipo (Int y Float no se mezclan) y puede estar vacía. No puede usarse en un campo de regla.
- Una cadena se convierte en red solo cuando se asigna directamente a una variable `Red`.

## Léxico (tokens)

```
IDENT   ::= letra { letra | digito | "_" }
INT     ::= digito { digito }
FLOAT   ::= digito { digito } "." digito { digito }
BOOLEANO ::= "verdadero" | "falso"
STRING  ::= '"' { cualquier_caracter_excepto('"') } '"'
```

Símbolos: `{ } ( ) [ ] , ; =` y los operadores `== != < > <= >= && || !`. Un `&` o un `|` solos no son válidos.

## Terminación de construcciones

- Instrucción suelta fuera de llaves → termina en `;`.
- Bloque de control (`principal`, `funcion`, `si`/`sino`, `para cada`) → **no** lleva `;` después del `}`.
- Bloque `regla { }` → **sí** lleva `;` después del `}` (es una declaración, no un bloque de control).
- Campo dentro de `regla { }` o de un struct literal → separado por `,` (excepto el último).

## Importaciones

- El archivo que se compila es un `programa` y debe terminar con `bloque_principal`.
- Un archivo importado con `importar` es un `modulo`: no lleva `bloque_principal`.
- Un `modulo` puede importar otros módulos.
- Todos los `importar` van al inicio del archivo, antes de cualquier otra instrucción; solo pueden estar precedidos por comentarios. Si no, se reporta `'importar' debe ir al inicio del archivo`.
- Las reglas de los módulos importados se colocan antes que las del archivo que los importa, en el orden de los `importar`.
- La ruta del `importar` es relativa a la carpeta del archivo que contiene el `importar`.
- Todos los archivos comparten los mismos nombres: una variable o regla no puede declararse en dos archivos distintos, y una variable y una regla no pueden llamarse igual.
