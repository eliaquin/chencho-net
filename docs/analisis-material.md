# Análisis del material de referencia

Fecha de revisión: 23 de octubre de 2026.

## 1. Cómo se revisó

Se inspeccionaron las cuatro fotografías, la estructura y los valores del Excel, el contenido del RAR y sus dos imágenes. Del video de aproximadamente 2 minutos y 9 segundos se revisaron fotogramas cada cinco segundos y una transcripción automática local de la narración completa. La transcripción contiene errores de reconocimiento; se utiliza para identificar el proceso y no como cita literal.

No se ejecutó BarTender ni se conectó una impresora. No hay archivos `.btw` ni artes originales en el material entregado. El directorio contiene material de referencia y no tiene aplicación implementada ni un repositorio Git inicializado al momento de la revisión.

## 2. Inventario y observaciones

| Fuente | Observación directa | Uso en la especificación |
| --- | --- | --- |
| `WhatsApp Image 2026-10-23 at 12.49.01 (1).jpeg` | Frente horizontal de un carnet de miembro del ICPARD, con fondo azul, logo, fotografía y campos variables | Referencia de composición de un carnet con fotografía |
| `WhatsApp Image 2026-10-23 at 12.49.01.jpeg` | Reverso con identificación institucional, leyenda y dos firmas | Evidencia de diseño a dos caras en el ejemplo |
| `WhatsApp Image 2026-10-23 at 12.49.01 (2).jpeg` | Otra imagen del mismo reverso; contenido visual equivalente | No representa una tercera cara ni un modelo adicional |
| `WhatsApp Image 2026-10-23 at 13.09.11.jpeg` | Impresora Zebra con pantalla «ZC300 lista» | Identificación visual del modelo; el usuario confirmó su uso en Windows |
| `WhatsApp Video 2026-10-23 at 13.00.33.mp4` | Edición del modelo en BarTender, conexión a Excel y propiedades de campos vinculados | Define el flujo de datos y la separación entre plantilla y registros |
| `Base de Datos Zebra ZC300.xlsx` | Una hoja `Hoja1`, encabezados A1:K1 y dos registros en A2:K3 | Ejemplo concreto de columnas, tipos y referencias a fotos |
| `Fotos prueba.rar` | Contiene `Fotos prueba/21718.png` y `Fotos prueba/23101.png`; ambas de 156 × 191 píxeles | Evidencia de asociación entre identificador y nombre de foto |

Los originales permanecen intactos en esta misma carpeta. No se copian nombres completos, cédulas ni retratos de titulares a los requisitos; los bloques institucionales fijos se describen como parte del diseño.

## 3. Secuencia del video

Los intervalos son aproximados, según el muestreo visual y los segmentos de la narración.

| Intervalo | Evidencia |
| --- | --- |
| 00:00–00:20 | Se muestran el frente y el reverso del modelo de carnet |
| 00:20–00:55 | Se abre la configuración de la fuente de datos y se identifica un documento Excel; la narración describe registros y rutas de imágenes |
| 00:55–01:20 | Se exploran opciones de vista y se explica la configuración de los elementos del modelo |
| 01:20–02:00 | Se abren las propiedades de un texto y se muestra su vínculo con el campo `NOMBRE` de la base de datos |
| 02:00–02:09 | Se describe que el mismo mecanismo alimenta otros campos y se vuelve a la vista del carnet |

El video explica cómo se compone el carnet. No demuestra una impresión física, selección de lotes, recuperación de errores, dúplex automático ni generación de carnets de empleados.

## 4. Estructura del Excel de muestra

Encabezados observados en `Hoja1!A1:K1`:

| Columna | Encabezado original | Observación de los dos registros |
| --- | --- | --- |
| A | `NOMBRE` | Texto con espacios externos en algunos valores |
| B | `APELLIDO` | Texto |
| C | `CEDULA` | Texto con guiones |
| D | `MATRÍCULA` | Valores numéricos que representan identificadores |
| E | `FILIAL` | Texto; el diseño visible utiliza la etiqueta «Provincia» |
| F | `2do NOMBRE ` | Encabezado con espacio final; un registro utiliza `-` |
| G | `2do APELLIDO` | Texto |
| H | `NO. EXEQUATUR` | Texto |
| I | `FECHA DE ADMISION ` | Fechas de Excel; encabezado con espacio final |
| J | `FECHA DE EXPIRACION ` | Fechas de Excel; encabezado con espacio final |
| K | `FOTOS` | Rutas absolutas de Windows a una carpeta de fotografías |

La columna `FOTOS` referencia archivos cuyos nombres coinciden con la matrícula y con los dos PNG del RAR. Por indicación posterior del solicitante, esta estructura se utiliza como base del producto: matrícula identifica al titular y `FOTOS` proporciona la referencia principal a su imagen.

Consecuencias prácticas:

- Normalizar encabezados evita fallos por espacios finales y diferencias de acentuación.
- Las fechas deben leerse como fechas; los identificadores, como texto.
- Hace falta una forma de reasociar la carpeta de fotos al mover los archivos entre computadoras.
- La aclaración posterior del solicitante establece que los campos del Excel y la foto del carnet son la base. Se incluyen matrícula, exequátur y filial; no se agregan cargo o departamento. `FILIAL` se imprime bajo la etiqueta Provincia, como en el frente fotografiado.
- Las fotos disponibles son pequeñas. La calidad final depende del tamaño del marco y del material fuente; debe revisarse en la impresora.

## 5. Confirmaciones del solicitante

Se confirmó la Zebra ZC300 y el uso de Excel para seleccionar a quién imprimir. Posteriormente se decidió desarrollar y probar directamente en Windows con .NET y WPF, imprimir ambas caras y utilizar los campos del Excel incluido y las fotografías del carnet como base. Estas aclaraciones reemplazan la propuesta inicial de un modelo genérico de empleados con otros campos. La especificación utiliza «titular» y «matrícula» para reflejar el material sin inventar un esquema de recursos humanos.

El frente queda definido por los diez campos textuales y la fotografía. El reverso es institucional y fijo. La capacidad de dúplex automático de la unidad y la calidad del arte reconstruido todavía deben verificarse físicamente; el producto contempla reinserción manual guiada cuando sea necesaria y haya sido validada.

## 6. Impresora: información pública y límites

Zebra documenta impresión a 300 ppp, material CR80 y variantes con impresión a una o dos caras para la familia ZC300. Esto orienta la prueba de salida, pero no acredita las opciones instaladas en la unidad fotografiada. Véanse la [ficha técnica de Zebra](https://www.zebra.com/gb/en/products/spec-sheets/printers/card/zc300.html) y la [página de soporte de la ZC300](https://www.zebra.com/us/en/support-downloads/printers/card/zc300.html), consultadas el 23 de octubre de 2026.

Al comenzar la implementación en Windows deben verificarse en la estación la versión del controlador, la modalidad de caras y el tipo de cinta. El tamaño nominal de una tarjeta y el lienzo de imagen del controlador no deben equipararse simplemente por conversión a píxeles: la calibración depende de la ruta de impresión.
