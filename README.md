# NetWall

**NetWall** es un lenguaje de programación para diseñar, probar y simular firewalls, junto con un IDE propio pensado para aprenderlo y enseñarlo.

En lugar de escribir comandos de `iptables` a mano, se describe el firewall con un lenguaje en español: redes, reglas, funciones, condiciones y ciclos. El compilador lo valida, detecta reglas que se contradicen o que nunca se aplican, simula paquetes contra las reglas y, al final, lo traduce a comandos reales de `iptables`.

```
importar "redes_empresa.nw";

regla ssh_admin {
    accion = permitir,
    protocolo = tcp,
    puerto = 22,
    origen = red_admin,
    destino = red_servidores,
    comentario = "ssh solo desde administración"
};

para cada p en puertos_web {
    publicar(p, red_servidores);
}

principal {
    politica_defecto = bloquear;
    simular admin_ssh { protocolo = tcp, puerto = 22, origen = "10.0.99.5", destino = "10.0.1.10" };
}
```

```
tcp 10.0.99.5 → 10.0.1.10 puerto 22: permitido por la regla 'ssh_admin'
```

---

## La idea

Configurar un firewall es fácil de hacer mal: una regla en el orden equivocado, una red mal escrita o una regla que nunca se aplica pasan desapercibidas hasta que algo falla. NetWall convierte esa configuración en un **programa**, y así puede revisarse antes de aplicarse:

- **Se escribe en español**, con palabras como `regla`, `permitir`, `bloquear`, `origen` y `destino`.
- **El compilador entiende redes**: valida los bloques CIDR, los puertos y los tipos, y explica cada error con su posible causa.
- **Avisa de errores lógicos**: reglas que se contradicen, reglas redundantes y reglas que nunca se alcanzan.
- **Se puede probar sin tocar un servidor**: `simular` manda paquetes de prueba y muestra qué regla decide cada uno.
- **Produce un firewall real**: el resultado se exporta como un script de `iptables`.

## El lenguaje

| Elemento | Ejemplo |
|---|---|
| Tipos | `String`, `Int`, `Float`, `Boolean`, `Red`, `Lista` |
| Variables | `Red oficina = "192.168.10.0/24";` |
| Reglas | `regla web { accion = permitir, protocolo = tcp, puerto = 80 };` |
| Rangos y comodines | `puerto = rango(1, 1024)`, `origen = cualquiera` |
| Operadores | `y` / `&&`, `o` / `\|\|`, `no` / `!`, `==`, `!=`, `<`, `>`, `<=`, `>=` |
| Condicionales | `si (...) { } sino si (...) { } sino { }` |
| Ciclos | `para cada p en [80, 443] { ... }` |
| Funciones propias | `funcion publicar(Int p, Red hacia) { regla publico { ... }; }` |
| Funciones de red | `tamano`, `mascara`, `direccion_red`, `broadcast`, `binario`, `contiene`, `interseccion` |
| Importaciones | `importar "redes.nw";` |
| Bloque principal | `principal { politica_defecto = bloquear; simular ...; mostrar(...); }` |

Un programa tiene dos partes:

- **Fuera de `principal`** se construye el firewall. Los `si`, los `para cada` y las funciones se resuelven al compilar y producen la lista final de reglas. Cuando una regla se genera varias veces, recibe un nombre automático como `web[ventas, 80]`.
- **Dentro de `principal`** se prueba: se define la política por defecto y se simulan paquetes. Como en un firewall real, **gana la primera regla que coincide**.

La gramática completa está en [`src/NetWall.Lang/src/gramatica.md`](src/NetWall.Lang/src/gramatica.md).

---

## Arquitectura

El proyecto tiene dos partes independientes que se comunican por línea de comandos:

```
┌────────────────────────────── NetWall.IDE (C# / Avalonia) ──────────────────────────────┐
│  editor · explorador · terminal · simulación visual · retos · modo clase · Helpy        │
└───────────────┬───────────────────────────────────────────────────────┬─────────────────┘
                │ texto con colores ANSI                                │ JSON por stdin/stdout
                │ compilar · ejecutar · exportar                        │ --simulacion · --retos
                ▼                                                       ▼
┌──────────────────────────────── NetWall.Lang (Python) ──────────────────────────────────┐
│                                       cli.py                                            │
│                                                                                         │
│  loader ──► lexer ──► parser ──► semantic ──► expander ──► analyzer ──┬──► simulator    │
│  (archivos   (tokens)  (árbol de  (tipos,      (si, para    (contradic-│                │
│   e importar)          nodos)     redes,       cada y        ciones,   └──► exporter    │
│                                   nombres)     funciones     redundan-       (iptables) │
│                                                → reglas)     cias)                      │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

### Compilador: `src/NetWall.Lang`

Está escrito en **Python, solo con la biblioteca estándar**. No usa generadores de analizadores como Lark, PLY o ANTLR: el lexer y el parser están hechos a mano.

| Módulo | Responsabilidad |
|---|---|
| `lexer.py` | Convierte el texto en tokens (`KEYWORD`, `IDENT`, `INT`, `STRING`, símbolos y operadores). |
| `parser.py` | Parser descendente recursivo que construye el árbol de sintaxis con las clases de `nodes.py`. Se recupera de los errores para reportar varios en una sola pasada. |
| `nodes.py` | Estructuras del árbol: `Program`, `Rule`, `Declaration`, `FunctionDef`, `If`, `ForEach`, `Network`, `Port`... |
| `loader.py` | Carga el archivo principal y sus `importar` de forma recursiva, detecta importaciones circulares y archivos repetidos, y une todo en un solo programa. |
| `semantic.py` | Verifica nombres, tipos, redes CIDR, puertos, funciones y llamadas. |
| `expander.py` | Evalúa los `si`, desenrolla los `para cada` y expande las llamadas a funciones hasta obtener la lista final de reglas. |
| `analyzer.py` | Analiza las reglas finales: contradicciones, redundancias y reglas inalcanzables. |
| `simulator.py` | Pasa cada paquete por las reglas en orden y explica qué regla lo decidió y por qué las anteriores no coincidieron. |
| `exporter.py` | Traduce las reglas a un script de `iptables` para la cadena `INPUT`, con la política por defecto al final y un aviso si el resultado bloquearía SSH. |
| `predefined.py` | Funciones de red calculadas con operaciones de bits. |
| `errors.py` | Catálogo de diagnósticos con una **posible causa** para cada error. |
| `cli.py` | Punto de entrada: modos de texto para personas y modos JSON para el IDE. |

```
python cli.py compilar archivo.nw    # valida y muestra las reglas finales
python cli.py ejecutar archivo.nw    # compila y además ejecuta principal (simulación)
python cli.py exportar archivo.nw    # genera archivo.iptables.txt
```

### IDE: `src/NetWall.IDE`

Está hecho en **C# con .NET 10 y Avalonia 11**, usando AvaloniaEdit para el editor y CommunityToolkit.Mvvm para el patrón MVVM. El IDE no reimplementa el lenguaje: llama al compilador de Python como un proceso aparte.

- **Compilar, Ejecutar y Exportar** escriben el comando en la terminal integrada y muestran la salida con colores ANSI y enlaces clicables a `archivo.nw:línea`.
- **La simulación, los retos y el modo clase** llaman al CLI con `--simulacion`, `--simulacion-personalizada` o `--retos`, le envían los paquetes en JSON por la entrada estándar y dibujan el resultado.
- **El resaltado de sintaxis** usa un escáner en C# que reproduce las reglas de `lexer.py`.

---

## Lo que hace único al IDE

### Simulación visual
Muestra la red del programa: equipos, servidores, Internet y el firewall en el centro, con paquetes animados que viajan por ella. Se puede simular de dos formas:
- **Programa completo**: usa las reglas y los paquetes `simular` del archivo.
- **Ciertas reglas**: activas o desactivas reglas una por una, armas tu propia red con computadoras y servidores, y envías paquetes nuevos.

Un panel de registro filtra los paquetes permitidos y bloqueados, y un inspector muestra el **recorrido del paquete por cada regla**: cuáles no coincidieron, por qué y cuál decidió.

### Retos
Un camino de aprendizaje con **14 retos en 4 módulos**: Variables y redes, Reglas, Avanzado y un Examen final. Cada reto tiene una lección, un enunciado, una pista y código inicial. Se valida compilando la solución y enviándole paquetes de prueba. El progreso se guarda.

### Modo clase
Pensado para un laboratorio de redes:
- El profesor crea una clase (`.nwclase`) con ejercicios de **escribir código** o de **arreglar código con errores**. Cada ejercicio tiene paquetes de prueba y una solución.
- El profesor **abre una sala en la red local** y los estudiantes la encuentran automáticamente y se unen. La sala usa TCP y el descubrimiento usa difusión UDP; admite hasta 60 estudiantes, cada uno con su avatar de gatito.
- Un **panel en vivo** muestra el avance de cada estudiante. Los resultados se validan en la computadora del profesor.

### Helpy, el asistente
Un gato asistente que acompaña al programador:
- **Modo ayuda**: escribes una palabra clave y presionas `Tab` para expandirla en una plantilla, por ejemplo una regla completa.
- **Asistente de código** (`Ctrl+Shift+A`): genera reglas, variables, paquetes `simular`, importaciones o el bloque `principal` a partir de un formulario y los inserta en el cursor. Usa las redes y los archivos del proyecto.

### Diagnósticos que enseñan
Cada error o advertencia del compilador incluye la línea, el archivo y una **posible causa** en lenguaje claro. En la terminal, la ubicación es un enlace que abre el archivo en esa línea.

### Pensado para proyectos
- Detecta automáticamente el archivo con `principal` y lo marca con una estrella en el explorador. Avisa si falta o si hay más de uno.
- Compila, ejecuta y exporta el **proyecto completo**, no solo la pestaña abierta.
- Pide confirmación antes de descartar cambios sin guardar y antes de abrir archivos muy grandes o binarios.

### Detalles
- Temas **Nebula**, **Dracula** y **Aurora**.
- Extensiones opcionales: **Indent Rainbow**, que colorea los niveles de indentación, y **DevCat**, una mascota que vive en la terminal. Escribir `gato$` en la terminal abre su galería.
- Pantalla de bienvenida con proyectos recientes y atajos de teclado (`Ctrl+N`, `Ctrl+O`, `Ctrl+S`, `Ctrl+Shift+S`, `Ctrl+B`, `Ctrl+W`).

---

## Estructura del repositorio

```
src/
├── NetWall.Lang/src/     compilador en Python (cli.py y módulos)
└── NetWall.IDE/          IDE en C# / Avalonia
    ├── Views/            ventanas, paneles y diálogos
    ├── ViewModels/       lógica de la interfaz (MVVM)
    ├── Services/         terminal, simulación, retos, modo clase, explorador
    ├── Controls/         controles propios (canvas de simulación, Helpy, editor)
    └── Assets/           retos, íconos, fuentes y sprites
pruebas/                  programas .nw de ejemplo y de errores
build-release.ps1         genera el ejecutable para Windows
```

Para ver el lenguaje en acción, abre la carpeta `pruebas/completa/` en el IDE: `firewall.nw` usa importaciones, funciones, condicionales, ciclos y simulación de paquetes. Los archivos de `pruebas/validos/` muestran cada parte del lenguaje por separado.

---

## Cómo ejecutarlo

### Desde el código

Requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/) y Python 3.

```
dotnet run --project src/NetWall.IDE
```

El compilador también puede usarse solo, sin el IDE:

```
python src/NetWall.Lang/src/cli.py ejecutar pruebas/completa/firewall.nw
```

### Ejecutable para Windows

```
powershell -ExecutionPolicy Bypass -File build-release.ps1
```

Genera `build/NetWall.exe`, un **único archivo** que incluye el IDE, .NET y un Python portátil con el compilador. Quien lo reciba no necesita instalar nada: solo abrirlo. Funciona en Windows de 64 bits. Como el ejecutable no está firmado, la primera vez Windows SmartScreen pedirá confirmar con **Más información → Ejecutar de todas formas**.
