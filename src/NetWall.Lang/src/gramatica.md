# NetWall — Gramática formal (EBNF)

Notacion: `::=` define, `|` alternativa (or), `[ ]` opcional, `{ }` cero o más,
literales entre comillas.

## Estructura general

```
programa        ::= { importacion } { declaracion | funcion_decl | regla_decl | llamada_stmt | mostrar_stmt | si_sino | para_stmt } bloque_principal

modulo          ::= { importacion } { declaracion | funcion_decl | regla_decl | llamada_stmt | mostrar_stmt | si_sino | para_stmt }

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
parametros      ::= parametro { "," parametro }
parametro       ::= tipo IDENT

llamada_stmt    ::= llamada_funcion ";"
```

- Una función es una plantilla de reglas: no devuelve un valor. Usada dentro de una expresión da `'{f}' no devuelve un valor`.
- Los parámetros solo existen dentro del cuerpo y ocultan una variable con el mismo nombre. Dos parámetros no pueden llamarse igual.
- La función debe declararse antes de la llamada, en el mismo archivo o en un archivo importado. Por eso una función solo puede llamarse a sí misma de forma directa, y eso da `la función '{f}' se llama a sí misma ({f} → {f})`.
- Dentro de una función (y de un `si` o un `para cada`) no se permiten declaraciones, `importar`, `funcion`, `principal`, `politica_defecto` ni `simular`: `'{instrucción}' no está permitida dentro de una función`, con la primera palabra de la instrucción (por ejemplo `'Int'`).
- Las llamadas no pueden ir dentro de `principal`.
- Al compilar, cada llamada se reemplaza por las reglas de la función, en la posición de la llamada. Cada regla generada se llama `regla[arg1, arg2]`, con los argumentos tal como están escritos: el nombre de una variable o el valor de un literal. Dentro de una llamada anidada, un parámetro se muestra con el argumento que recibió. Si el nombre se repite, la regla recibe además `#2`, `#3`, etc. La línea de la regla generada es la de la llamada de nivel superior.

## Bloque principal

```
bloque_principal ::= "principal" "{" { sentencia_principal } "}"

sentencia_principal ::= politica_stmt
                       | simular_stmt
                       | mostrar_stmt

politica_stmt   ::= "politica_defecto" "=" accion_valor ";"

simular_stmt    ::= "simular" (IDENT | struct_literal) ";"

struct_literal  ::= IDENT "{" campo { "," campo } [ "," ] "}"
```

## Sentencias del cuerpo de una función (y de si / para)

```
sentencia       ::= regla_decl
                   | llamada_stmt
                   | mostrar_stmt
                   | si_sino
                   | para_stmt

llamada_funcion ::= IDENT "(" [ argumentos ] ")"
argumentos      ::= expresion { "," expresion }
```

## Funciones predefinidas y mostrar

```
mostrar_stmt    ::= "mostrar" "(" [ argumentos ] ")" ";"
```

- `mostrar` y las funciones predefinidas son nombres (`IDENT`), no palabras reservadas, pero no pueden usarse como nombre de una variable, una regla ni una función: `'{f}' es una función predefinida y no puede redefinirse`.
- `mostrar` a nivel superior se imprime al compilar, con el prefijo `[compilación]`. Dentro de `principal` se imprime al ejecutar, en orden junto a los `simular`.
- `mostrar` no devuelve un valor: no puede usarse dentro de una expresión.
- Las demás funciones devuelven un valor y se usan dentro de una expresión. Escritas solas como instrucción dan `el resultado de '{f}' no se usa`.

| Función | Parámetros | Devuelve |
|---|---|---|
| `tamano(r)` | Red | Int |
| `contiene(r, x)` | Red, y Red o String | Boolean |
| `interseccion(a, b)` | Red, Red | Boolean |
| `mascara(r)` | Red | String |
| `direccion_red(r)` | Red | String |
| `broadcast(r)` | Red | String |
| `binario(ip)` | String | String |

Donde se espera una Red se acepta una variable `Red`, una red escrita entre comillas o `cualquiera`. Todo texto que recibe una de estas funciones debe ser una dirección o una red válida.

## Control de flujo

```
si_sino         ::= "si" "(" expresion ")" "{" { sentencia } "}"
                    { "sino" "si" "(" expresion ")" "{" { sentencia } "}" }
                    [ "sino" "{" { sentencia } "}" ]

para_stmt       ::= "para" "cada" IDENT "en" expresion "{" { sentencia } "}"
```

- `si` y `para cada` van a nivel superior o dentro de una función, y pueden anidarse entre sí. No llevan `;` después de `}`.
- No pueden ir dentro de `principal`: `se esperaba 'politica_defecto', 'simular' o 'mostrar'`.
- Se resuelven al compilar. Del `si` solo se expande la primera rama cuya condición es verdadera; las demás igual se revisan.
- La condición es de tipo Boolean: `la condición de 'si' debe ser de tipo Boolean, pero es {T}`.
- Después de `en` va una lista o una variable `Lista`: `'para cada' requiere una lista, pero '{x}' es de tipo {T}`. Una lista vacía no produce vueltas.
- La variable del ciclo solo existe dentro del bloque, toma el tipo de los elementos y no puede usar un nombre ya declarado (`'{x}' ya fue declarada ({ref})`).
- Dentro de un `si` o un `para cada` no se permiten declaraciones, `importar`, `funcion`, `principal`, `politica_defecto` ni `simular`: `'{instrucción}' no está permitida dentro de 'si'` (o `dentro de 'para cada'`).
- Nombres de las reglas:
  - Una regla dentro de un `si` de nivel superior conserva su nombre, que no puede repetir un nombre global. Dos ramas distintas pueden definir una regla con el mismo nombre, porque solo se expande una.
  - Dentro de un `para cada`, el nombre lleva entre corchetes el elemento de cada vuelta: `web[80]`. Con ciclos anidados van todos, de afuera hacia adentro: `w[22, 1]`.
  - Dentro de una función, primero van los argumentos de la llamada y después los elementos de los ciclos: `r[22, 2]`.
  - Un elemento se muestra como está escrito en la lista: el nombre de una variable o el valor de un literal.

## Expresiones

Precedencia de mayor a menor: `no` / `!`, relacionales, `y` / `&&`, `o` / `||`. Los operadores del mismo nivel se agrupan de izquierda a derecha.

```
expresion       ::= expr_y { ( "o" | "||" ) expr_y }
expr_y          ::= expr_rel { ( "y" | "&&" ) expr_rel }
expr_rel        ::= expr_no { operador_rel expr_no }
expr_no         ::= ( "no" | "!" ) expr_no | primario
primario        ::= STRING | INT | FLOAT | BOOLEANO | "cualquiera" | IDENT | llamada_funcion
                   | "(" expresion ")" | lista
lista           ::= "[" [ expresion { "," expresion } ] "]"
operador_rel    ::= "==" | "!=" | "<" | ">" | "<=" | ">="
```

- Todas las operaciones devuelven Boolean.
- `==` y `!=`: Int y Float entre sí, o dos operandos del mismo tipo String, Boolean o Red. Dos Red son iguales si tienen la misma dirección de red y el mismo prefijo.
- `<`, `>`, `<=`, `>=`: solo Int y Float.
- `y`, `o`, `no` (y `&&`, `||`, `!`): solo Boolean. `y` y `o` se evalúan en cortocircuito.
- Una `lista` tiene todos sus elementos del mismo tipo (Int y Float no se mezclan) y puede estar vacía. No puede usarse en un campo de regla.
- Una cadena se convierte en red solo cuando se asigna directamente a una variable `Red` o se pasa a una función predefinida que espera una Red.
- `cualquiera` es un valor de tipo Red (0.0.0.0/0).

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
- Todos los archivos comparten los mismos nombres: una variable, función o regla no puede declararse en dos archivos distintos, y una variable, una función y una regla no pueden llamarse igual.
- Las funciones de un módulo se llaman por su nombre directo, igual que las variables.
