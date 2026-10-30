# NetWall — Gramática formal (EBNF)

Notacion: `::=` define, `|` alternativa (or), `[ ]` opcional, `{ }` cero o más,
literales entre comillas.

## Estructura general

```
programa        ::= { importacion | declaracion | funcion_decl | regla_decl } bloque_principal

modulo          ::= { importacion | declaracion | funcion_decl | regla_decl }

importacion     ::= "importar" STRING ";"

declaracion     ::= tipo IDENT "=" expresion ";"

tipo            ::= "Red" | "Int" | "Float" | "String" | "Boolean"
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
puerto_valor    ::= INT | "rango" "(" INT "," INT ")"
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
                       | sentencia

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

<!-- pendiente: agregar el resto -->

```
expresion       ::= termino { operador termino }
termino         ::= STRING | INT | FLOAT | BOOLEANO | IDENT | llamada_funcion
operador ::= "?"
```
<!-- pendiente: agregar operadores -->

## Léxico (tokens)

```
IDENT   ::= letra { letra | digito | "_" }
INT     ::= digito { digito }
FLOAT   ::= digito { digito } "." digito { digito }
BOOLEANO ::= "verdadero" | "falso"
STRING  ::= '"' { cualquier_caracter_excepto('"') } '"'
```

## Terminación de construcciones

- Instrucción suelta fuera de llaves → termina en `;`.
- Bloque de control (`principal`, `funcion`, `si`/`sino`, `para cada`) → **no** lleva `;` después del `}`.
- Bloque `regla { }` → **sí** lleva `;` después del `}` (es una declaración, no un bloque de control).
- Campo dentro de `regla { }` o de un struct literal → separado por `,` (excepto el último).

## Importaciones

- El archivo que se compila es un `programa` y debe terminar con `bloque_principal`.
- Un archivo importado con `importar` es un `modulo`: no lleva `bloque_principal`.
- Un `modulo` puede importar otros módulos.
- Las reglas de los módulos importados se colocan antes que las del archivo que los importa, en el orden de los `importar`.
- La ruta del `importar` es relativa a la carpeta del archivo que contiene el `importar`.
- Todos los archivos comparten los mismos nombres: una variable o regla no puede declararse en dos archivos distintos, y una variable y una regla no pueden llamarse igual.
