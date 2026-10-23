# Aplicación de impresión de carnets

Versión: 1.0. Fecha: 23 de octubre de 2026. Estado: especificación funcional y técnica definida; validación de hardware y preparación del arte de impresión pendientes.

## 1. Objetivo y decisiones confirmadas

Sustituir el uso ocasional de BarTender por una aplicación de escritorio dedicada a preparar e imprimir carnets. El operador carga un Excel, selecciona personas, revisa frente y reverso y envía las tarjetas a la Zebra ZC300.

Decisiones confirmadas por el solicitante:

- Tanto el desarrollo como las pruebas y la operación se realizarán directamente en Windows.
- La tecnología elegida es C# con WPF sobre .NET 10 LTS, utilizando inicialmente el controlador Windows de Zebra.
- La impresora objetivo es la Zebra ZC300 mostrada en el material.
- Los datos se cargan desde Excel y el operador selecciona a quién imprimir; no se introducen fichas manualmente en la aplicación.
- La impresión es por ambas caras.
- Por aclaración posterior del solicitante, el Excel incluido define los campos base y las fotografías del carnet definen el modelo visual inicial. Esta precisión sustituye la propuesta anterior de campos genéricos de empleados.

Para evitar confundir la denominación inicial «empleados» con los campos de membresía del material, esta especificación utiliza **titular** para la persona y **matrícula** para el identificador. No agrega cargo, departamento ni código de empleado. Las reglas de validación y los valores predeterminados descritos a continuación son decisiones de diseño del MVP, derivadas del flujo solicitado; no son comportamientos demostrados por el video.

## 2. Alcance del MVP

| Incluido | Límite |
| --- | --- |
| Cargar un archivo `.xlsx` y elegir su hoja | Compatible con `Hoja1` del archivo incluido; sin modificar el original ni necesitar Excel instalado |
| Asociar columnas con campos del carnet | Guardar esa asociación para archivos posteriores con la misma estructura |
| Asociar fotografías locales | Carpeta seleccionada por el operador y resolución de nombres o rutas |
| Buscar y seleccionar titulares | Una persona o una selección explícita de varias personas |
| Vista previa del carnet | Frente variable y reverso institucional fijo |
| Imprimir y volver a imprimir una selección | Cantidad de copias explícita, con valor inicial de una |
| Guardar ajustes locales | Diseño configurado, asociación de columnas, carpeta de fotos y perfil de impresora |

Un único modelo inicial, horizontal y de dos caras, basado en las fotografías incluidas. El operador lo utiliza sin manipular objetos gráficos. Fondos, posiciones, fuentes y textos institucionales se definen en un perfil local versionado preparado durante la implementación; un diseñador visual de plantillas queda fuera del MVP. Se permiten ajustes de calibración y encuadre fotográfico.

Fuera del alcance: gestión de recursos humanos, altas y bajas de empleados, nómina, control de acceso, asistencia, portal web, nube, sincronización entre estaciones, cuentas y roles propios de la aplicación, códigos de barras o QR, codificación de chips o bandas magnéticas, automatizaciones, diseñador general de etiquetas e importación de archivos `.btw` de BarTender. Incorporar cualquiera de estas funciones exige revisar el alcance.

## 3. Flujo del operador

```mermaid
flowchart LR
    A[Cargar Excel y elegir hoja] --> B[Asociar columnas y carpeta de fotos]
    B --> C[Validar datos]
    C --> D[Buscar y seleccionar titulares]
    D --> E[Revisar frente y reverso]
    E --> F[Confirmar impresora y copias]
    F --> G[Enviar a la cola de Windows]
    G --> H[Revisar tarjetas físicas]
```

En cargas posteriores se reutilizan las asociaciones cuando las columnas necesarias siguen presentes. Nunca se seleccionan todos los titulares automáticamente al abrir un archivo.

## 4. Datos del carnet

Fuente: `Base de Datos Zebra ZC300.xlsx`, hoja `Hoja1`, encabezados A1:K1. Se requieren las once columnas asociadas; pueden aparecer en otro orden o con encabezados equivalentes. La obligatoriedad de valores se define en esta tabla.

| Columna del archivo base | Campo interno | Etiqueta o destino en el carnet | Tratamiento y obligatoriedad del valor |
| --- | --- | --- | --- |
| A: `NOMBRE` | `nombre` | Nombre | Texto obligatorio |
| B: `APELLIDO` | `apellido` | Apellido | Texto obligatorio |
| C: `CEDULA` | `cedula` | Cédula | Texto obligatorio; conservar guiones y ceros |
| D: `MATRÍCULA` | `matricula` | Matrícula | Identificador obligatorio y único en la carga; almacenar como texto |
| E: `FILIAL` | `filial` | Provincia | Texto obligatorio; correspondencia comprobada en la referencia |
| F: `2do NOMBRE ` | `segundoNombre` | 2do. Nombre | Opcional; vacío o `-` se imprime como `-` |
| G: `2do APELLIDO` | `segundoApellido` | 2do. Apellido | Opcional; vacío o `-` se imprime como `-` |
| H: `NO. EXEQUATUR` | `numeroExequatur` | No. Exequátur | Texto obligatorio; preservar su formato |
| I: `FECHA DE ADMISION ` | `fechaAdmision` | Fecha de Admisión | Fecha obligatoria, impresa como `dd/MM/yyyy` |
| J: `FECHA DE EXPIRACION ` | `fechaExpiracion` | Fecha de Expiración | Fecha obligatoria, impresa como `dd/MM/yyyy` |
| K: `FOTOS` | `referenciaFoto` | Fotografía del titular | PNG o JPEG obligatorio; puede resolverse por matrícula si la celda está vacía |

Los espacios finales de los encabezados F, I y J son parte del archivo observado, pero no deben impedir reconocer las columnas. No se interpreta matrícula como fecha aunque una celda tenga un formato incorrecto.

La matrícula identifica los registros durante la sesión; cambiar el orden de la tabla o filtrar nunca cambia su relación con la foto o el carnet. Las fechas se toman del Excel: la aplicación no calcula una nueva fecha de vencimiento ni renueva registros. Si la expiración es anterior a la admisión, bloquear esa fila. Si está vencida respecto a la fecha local de la estación, advertirlo y solicitar reconocimiento antes del envío, sin alterar el dato.

### Composición de referencia

- **Frente:** fondo azul, logo en la zona superior izquierda y nombre institucional en la cabecera. Bajo el logo aparecen la leyenda «CARNET DE MIEMBRO» y la foto. A la derecha, cinco filas de dos campos: Nombre / 2do. Nombre; Apellido / 2do. Apellido; Cédula / No. Exequátur; Matrícula / Fecha de Admisión; Provincia / Fecha de Expiración. Los valores se imprimen sobre recuadros claros, como en la fotografía.
- **Reverso:** fondo claro, «ICPARD» en la parte superior, franja oscura con la leyenda «ESTA CREDENCIAL IDENTIFICA AL TITULAR COMO MIEMBRO DEL ICPARD» y dos bloques de firmas con los nombres y cargos visibles en la referencia. Es el mismo reverso para todos los titulares.
- Las leyendas, logo, firmas, nombres y cargos institucionales son contenido fijo del perfil. Se transcriben del material y no se solicitan en cada fila ni se deducen de los datos de una persona.
- Las fotografías definen composición y apariencia, pero no suministran medidas exactas ni arte limpio. Los elementos se reconstruirán en un lienzo de impresión; no se imprimen la mano, la mesa, las sombras, el desgaste ni la perspectiva de la fotografía. Los gráficos institucionales deben obtenerse de originales o de una extracción cuya calidad se revise antes de la aceptación física.

## 5. Requisitos funcionales

### RF-01. Carga de Excel

- Permitir seleccionar un `.xlsx`, mostrar sus hojas y elegir la hoja de titulares. Sugerir `Hoja1` cuando exista.
- Usar la primera fila como encabezados. Omitir filas completamente vacías, conservando el número de fila original en los errores. Rechazar con un mensaje claro otros formatos, libros protegidos que no se puedan leer o archivos dañados.
- Mostrar una vista tabular antes de imprimir. No ejecutar macros, fórmulas ni conexiones externas. Un campo que dependa de una fórmula sin valor calculado disponible debe señalarse como pendiente de corregir en el Excel.
- Leer fechas reales de Excel considerando el calendario del libro. Las fechas escritas como texto se interpretan únicamente con el formato configurado, inicialmente `dd/MM/yyyy`; una fecha inválida genera un error visible.
- Los identificadores se tratan como texto. Si Excel ya perdió ceros iniciales y no existe un formato explícito que los conserve, advertirlo; no inventar los ceros faltantes.
- Conservar la carga anterior si falla la lectura de un archivo nuevo. Al cargar o recargar correctamente, limpiar la selección y mostrar los resultados de la nueva validación.

### RF-02. Asociación de columnas

- Permitir elegir qué columna corresponde a cada campo utilizado por el diseño. Admitir columnas adicionales sin importarlas al carnet.
- Sugerir coincidencias ignorando espacios externos, mayúsculas y acentos de encabezados, pero pedir resolver cualquier ambigüedad. Dos encabezados equivalentes después de normalizarlos no pueden asociarse silenciosamente.
- Recordar la asociación. Si en el siguiente archivo falta una columna necesaria, solicitar corregirla antes de habilitar la impresión de los registros afectados.
- Quitar espacios externos de los valores para su presentación. Conservar caracteres internos y no convertir nombres a mayúsculas salvo que el diseño lo establezca.

### RF-03. Fotografías

- Permitir elegir una carpeta de fotos. Admitir una columna que contenga un nombre de archivo, una ruta relativa a esa carpeta o una ruta absoluta existente.
- Para rutas absolutas de otra computadora, ofrecer explícitamente buscar el nombre de archivo en la carpeta elegida; no asumir que la ruta antigua existe.
- Dar prioridad a la referencia explícita de `FOTOS`. Si está vacía, buscar una coincidencia exacta entre matrícula y nombre de archivo, con extensión PNG, JPG o JPEG, sin recorrer subcarpetas automáticamente. Comparar nombres sin distinguir mayúsculas. Si varias fotos coinciden, pedir elegir una; si una ruta explícita falla, no sustituirla por una coincidencia por matrícula sin intervención del operador.
- Mostrar foto ausente, archivo ilegible o coincidencia ambigua como error del titular afectado. No reutilizar la foto del titular anterior.
- Mantener la proporción de la imagen. Aplicar un encuadre inicial centrado que llene el marco, con ajuste manual de recorte; nunca deformar la fotografía ni modificar el archivo original.
- Advertir cuando la resolución efectiva en el tamaño impreso sea inferior a 150 ppp. La advertencia no bloquea la impresión. No prometer mejorar el detalle de una foto pequeña.

### RF-04. Validación y selección

- Buscar por matrícula, cédula, nombre o apellido y seleccionar una o varias filas, con una acción explícita para seleccionar todos los resultados filtrados. La búsqueda ignora mayúsculas y acentos, sin modificar los datos impresos.
- Mostrar cantidad seleccionada y cantidad de tarjetas resultante: titulares seleccionados multiplicados por copias. Un frente y un reverso pertenecen a la misma tarjeta.
- Mantener la selección al filtrar u ordenar; indicar cuántos seleccionados quedan fuera de la vista y mostrar la lista completa en la confirmación. Disponer de una acción para limpiar toda la selección.
- Detectar campos obligatorios vacíos, matrícula duplicada dentro de la hoja, fechas inválidas, fotos faltantes y contenido que exceda el espacio del diseño.
- Bloquear todas las filas con matrícula duplicada hasta corregir el archivo; no elegir una arbitrariamente. Para comparar matrículas, quitar espacios externos y conservar ceros iniciales.
- Los errores de otras filas no impiden imprimir una selección válida. Si la selección contiene errores, bloquear el envío y ofrecer quitar esas filas de la selección de forma explícita.

### RF-05. Diseño y vista previa

- El modelo define tamaño físico, orientación, fondo, posiciones de textos, marco de foto y contenido del reverso de acuerdo con la sección 4.
- Usar tarjeta CR80 horizontal, de tamaño nominal 85,60 × 53,98 mm. La correspondencia con el área de impresión del controlador se verifica mediante calibración.
- Separar elementos fijos de datos variables. Reconstruir el diseño de referencia como un lienzo limpio; no utilizar la foto completa del carnet como fondo impreso.
- Mostrar todos los campos y la foto del titular seleccionado y permitir alternar entre frente y reverso.
- Utilizar las mismas reglas de composición para la vista previa y la salida impresa. El zoom de pantalla no modifica las dimensiones físicas.
- Para texto largo, reducir la tipografía hasta un mínimo de 7 puntos para valores variables; si todavía no cabe, señalar el error y bloquear esa fila. Mantener cada valor dentro del recuadro correspondiente, sin recortar ni superponer nombres silenciosamente.
- Para segundo nombre y segundo apellido vacíos, imprimir `-`, conservando los recuadros del diseño. Nunca reutilizar valores de otra fila.

### RF-06. Impresión

- Enumerar las impresoras instaladas en Windows y permitir elegir la ZC300. Recordar la selección, pero bloquear el envío si deja de estar disponible. No cambiar silenciosamente a otra impresora.
- Antes del envío mostrar impresora, titulares, copias, total de tarjetas y modalidad de impresión a dos caras. El operador debe confirmar cada envío. Copias es un entero entre 1 y 100 por titular y comienza en 1; rechazar valores fuera de ese intervalo.
- Aplicar tamaño de tarjeta, orientación y escala física del 100 %, sin encabezados o pies de página de documentos. Las opciones específicas de cinta y panel negro pueden configurarse mediante el controlador de Zebra.
- La operación normal imprime ambas caras. Utilizar dúplex automático solo si la unidad y su controlador lo admiten y la prueba física ha validado orientación y orden.
- Incluir un modo manual guiado si no hay dúplex automático: una tarjeta por vez, frente primero, pausa para retirar y reinsertar, confirmación del operador y envío del reverso. Mantener el mismo titular y número de copia bloqueados durante ese ciclo. No ofrecer una pasada masiva de frentes seguida de otra de reversos.
- El perfil documenta la orientación física de reinserción y el modo de alimentación validado para esa unidad. No habilitar el modo manual sin haber comprobado ambos en la ZC300 real. Si ninguna modalidad puede producir las dos caras correctamente, la aceptación de impresión queda bloqueada.
- Imprimir solo un frente o un reverso se limita a pruebas y recuperación explícita de una tarjeta incompleta; no es el valor por defecto. Identificar al titular y la cara antes de confirmar esa acción.
- Evitar que un doble clic envíe dos veces el mismo trabajo. Conservar la asociación entre frente y reverso de cada titular en todas las modalidades.
- Permitir detener los envíos pendientes. Detener no cancela automáticamente los trabajos que Windows ya aceptó; indicar cómo revisarlos en su cola.
- Permitir volver a imprimir mediante una nueva selección y confirmación. No reintentar automáticamente después de un resultado incierto o un reinicio.

### RF-07. Estado y errores de impresión

- Distinguir validación, preparación, envío, aceptación por la cola, fallo de envío y resultado desconocido. La aceptación de Windows no demuestra que la tarjeta física salió correctamente.
- Mostrar estados o errores de la cola cuando estén disponibles. No prometer detectar todos los atascos, la falta de cinta o la falta de tarjetas desde la aplicación.
- Si falla un lote después de un envío parcial, mostrar qué registros se enviaron y cuáles no; marcar los inciertos. El operador decide cuáles repetir tras revisar la impresora.
- En dúplex automático, preparar un trabajo por titular con frente y reverso consecutivos y sus copias, enviándolos en el orden mostrado en la confirmación. En modo manual, enviar una cara por trabajo y una copia por ciclo, con las pausas descritas en RF-06. Verificar que el controlador respeta estas agrupaciones.
- El resumen de sesión distingue tarjetas enviadas, pendientes, incompletas y de resultado desconocido. Los conteos de caras enviados no se presentan como tarjetas físicas completadas.
- Si el adaptador permite conservar el identificador de trabajo, mostrarlo para facilitar la consulta o cancelación desde la cola de Windows. Cerrar la aplicación no cancela trabajos ya aceptados por Windows.

### RF-08. Configuración y datos locales

- Guardar perfil de diseño, asociación de columnas y ajustes de impresión bajo el usuario de Windows. No exigir permisos de administrador para el uso diario.
- Mantener los datos de titulares, ajustes de recorte y resumen de impresión en memoria durante la sesión. No crear una base de personas ni un historial persistente en el MVP.
- Al cerrar o recargar, advertir si se perderán ajustes de encuadre de la sesión. Reabrir la aplicación no debe enviar trabajos ni restaurar una selección lista para imprimir.
- No sobrescribir el Excel ni las fotografías. Durante un envío, bloquear cambios de archivo, diseño, selección y recortes, y trabajar con datos consistentes. Si una foto cambia o deja de poder leerse después de la revisión, detener la preparación de la tarjeta afectada y solicitar revisarla antes de enviarla.
- Guardar la configuración de forma atómica y con versión de esquema. Ante un perfil corrupto, conservar el archivo para diagnóstico y ofrecer restablecerlo; nunca imprimir automáticamente con valores sustituidos.
- No transmitir datos, fotos ni carnets a servicios externos. Los registros técnicos deben evitar nombres, cédulas, contenido del Excel y fotografías.

## 6. Interfaz

Una ventana principal con tres áreas: archivo y búsqueda; tabla de titulares con selección y errores; vista previa del carnet. Acciones principales: **Cargar Excel**, **Carpeta de fotos**, **Revisar selección** e **Imprimir**. La matrícula permanece visible al revisar y durante el modo manual.

Un panel de configuración reúne la asociación de columnas, la carpeta de fotos, la impresora, la modalidad automática o manual y la calibración por cara. La operación habitual no necesita abrir propiedades de cada texto del diseño como en el video. Los mensajes indican hoja, fila, campo, causa y acción correctiva. Las advertencias se distinguen de los errores que bloquean una fila.

## 7. Requisitos de operación y calidad

- Interfaz en español, con navegación por teclado, selección visible y mensajes legibles con escalado de Windows al 100 %, 125 % y 150 %.
- Funcionamiento local sin conexión a Internet durante carga, revisión e impresión. La instalación del controlador y de los componentes de ejecución debe quedar documentada.
- Objetivos de rendimiento: abrir en menos de 5 segundos, cargar y validar una hoja de 1.000 titulares en menos de 5 segundos sin contar la decodificación completa de todas sus fotos, y actualizar una vista previa en menos de 1 segundo. Medir y registrar resultados en la estación real; todavía no son cifras verificadas. Usar un conjunto sintético de 1.000 filas para esta prueba, sin confundirlo con el tamaño del archivo proporcionado.
- Cargar fotos al necesitarlas y preparar trabajos sin congelar la interfaz. La velocidad mecánica de la impresora no se incluye en esos objetivos.
- Distribución instalable para la versión y arquitectura de la estación Windows real, que deben documentarse al comenzar la implementación. El paquete incluirá los componentes de .NET necesarios o verificará su presencia, con instrucciones de instalación sin conexión. Actualizar la aplicación debe conservar sus ajustes compatibles.
- Configurar offsets por cara en milímetros y giro de reverso de 0° o 180° según la prueba. Una muestra con marcas a 3 mm del borde debe mantener desplazamiento ≤ 1 mm y variación de escala ≤ 1 % en tres tarjetas consecutivas. Si el hardware no permite esa tolerancia, registrar la limitación y acordar un criterio corregido antes de aceptar la salida.

## 8. Criterios de aceptación del MVP

Estos escenarios se ejecutarán cuando exista implementación. No representan pruebas ya realizadas.

| ID | Escenario | Resultado esperado |
| --- | --- | --- |
| CA-01 | Cargar el Excel incluido, hoja `Hoja1` | Reconocer once columnas y dos registros, con sus datos y errores, sin modificar el archivo |
| CA-02 | Volver a cargar un archivo con la misma estructura | Reutilizar asociaciones y comenzar sin selección |
| CA-03 | Cambiar encabezados o duplicar uno equivalente | Solicitar resolver la asociación, sin vincular una columna arbitraria |
| CA-04 | Matrícula `000123`, nombres con ñ y fechas `dd/MM/yyyy` | Conservar el identificador y mostrar texto y fechas correctamente |
| CA-05 | Una ruta de foto apunta a otra computadora | Permitir elegir carpeta y recuperar el archivo por nombre con confirmación |
| CA-06 | Foto ausente, ambigua o ilegible | Bloquear ese titular e identificar el problema sin mostrar otra foto |
| CA-07 | Nombres largos, segundos nombres vacíos y matrículas duplicadas | Respetar el mínimo de fuente, imprimir `-` para opcionales vacíos y bloquear duplicados |
| CA-08 | Seleccionar dos titulares y una copia en dúplex automático | Enviar dos trabajos de dos caras y producir dos tarjetas con su foto y reverso correspondientes |
| CA-09 | Imprimir dos veces por doble clic o cancelar antes del envío | Evitar el envío duplicado; cancelar antes del envío no crea trabajos |
| CA-10 | ZC300 ausente o error al enviar | Mostrar el fallo y conservar la selección para corrección; no usar otra impresora |
| CA-11 | Cola acepta el trabajo | Mostrar «Enviado a la cola», sin afirmar emisión física verificada |
| CA-12 | Lote parcialmente enviado o aplicación reiniciada | Identificar incertidumbre cuando sea posible y no reenviar automáticamente |
| CA-13 | Imprimir muestra en la ZC300 real | Validar escala, posición, foto, textos, caras y legibilidad con el material definitivo |
| CA-14 | Usar con Internet desconectado | Completar el flujo sin autenticación ni servicios remotos |
| CA-15 | Abrir y revisar el mismo carnet con escalados de pantalla distintos | Conservar geometría impresa y permitir operar la interfaz |
| CA-16 | Contrastar Excel y frente de referencia | Incluir los diez valores textuales y la foto; imprimir `FILIAL` como Provincia y conservar segundo nombre/apellido separados |
| CA-17 | Revisar reversos de dos titulares distintos | Mostrar la misma composición institucional, leyenda y bloques de firmas; ningún dato personal pasa al reverso |
| CA-18 | Imprimir en modo manual una tarjeta y luego dos copias de otra | Pausar entre caras, identificar titular y copia, exigir confirmación y evitar pasar al siguiente ciclo con una tarjeta incompleta |
| CA-19 | Ordenar o filtrar después de seleccionar | Mantener identidad y foto; indicar seleccionados ocultos y enumerarlos en la confirmación |
| CA-20 | Fecha vencida o expiración anterior a la admisión | Advertir y pedir reconocimiento en el primer caso; bloquear el segundo sin modificar fechas |
| CA-21 | Configuración persistida, corrupta o actualizada | Recuperar ajustes compatibles y ofrecer restablecimiento explícito de los inválidos, sin envío automático |
| CA-22 | Cargar 1.000 registros sintéticos y recorrer vistas previas | Cumplir los tiempos de la sección 7 en la estación documentada y mantener la interfaz operativa |
| CA-23 | Detener un lote o recuperar una cara | Detener nuevos envíos e informar los ya aceptados; exigir identificación y confirmación de la cara a recuperar |
| CA-24 | Cambiar o borrar una foto después de la revisión | Detener la tarjeta afectada antes de su envío y solicitar una nueva revisión, conservando el resumen parcial |
| CA-25 | Solicitar cero copias, decimales o más de cien | Rechazar la cantidad; una selección válida con dos copias produce el doble de tarjetas completas |

### Cobertura de requisitos

| Requisito | Criterios principales |
| --- | --- |
| RF-01: Excel | CA-01, CA-02, CA-04, CA-20, CA-22 |
| RF-02: columnas | CA-01, CA-03, CA-16 |
| RF-03: fotos | CA-05, CA-06, CA-24 |
| RF-04: selección y validación | CA-07, CA-19, CA-20, CA-25 |
| RF-05: diseño | CA-07, CA-13, CA-15, CA-16, CA-17 |
| RF-06: impresión | CA-08, CA-09, CA-10, CA-13, CA-18, CA-23, CA-25 |
| RF-07: estados y errores | CA-10, CA-11, CA-12, CA-18, CA-23 |
| RF-08: configuración y datos | CA-02, CA-12, CA-14, CA-21, CA-24 |

## 9. Preparación necesaria para la implementación y aceptación

El alcance, los campos, las dos caras y la tecnología quedan definidos en este documento. Lo que falta es material de producción y verificación de la estación, no volver a decidir el producto.

| Elemento | Acción concreta | Momento requerido |
| --- | --- | --- |
| Estación Windows | Registrar versión, arquitectura, controlador Zebra, conexión, cinta y material | Al iniciar la implementación en Windows |
| Dúplex automático o reinserción manual | Identificar opciones instaladas y ejecutar la prueba de ambas caras; fijar perfil de orientación y alimentación | Antes de aceptar el adaptador de impresión |
| Logo, fondo y firmas | Obtener originales o preparar una extracción utilizable de la referencia y revisar su calidad al tamaño final | Antes de aceptar el diseño impreso |
| Tipografía y posiciones definitivas | Ajustar en el lienzo CR80 la composición de referencia y verificar muestras de texto largo | Durante la construcción del modelo |
| Calidad y calibración física | Ejecutar CA-08, CA-13, CA-18 y CA-25 en las modalidades habilitadas | Antes de entregar la aplicación |

No se necesita otro Excel para comenzar: el incluido es la base de compatibilidad. Para ampliar pruebas se crearán copias de trabajo y datos sintéticos; los originales permanecerán intactos.

## 10. Entrega y definición de terminado

La implementación se considera terminada cuando:

1. La solución se compila y ejecuta directamente en Windows con .NET 10 y WPF.
2. Se verifican los 25 criterios de aceptación. Se registra qué modalidad de impresión admite la estación; los escenarios de una modalidad no instalada se marcan como no aplicables, con evidencia, y se prueba la modalidad utilizada para producir ambas caras.
3. Se inspeccionan frente y reverso impresos con el diseño final. Una vista previa o una cola que acepta trabajos no sustituyen esta comprobación.
4. Se entrega instalador o paquete instalable, código fuente, perfil del carnet y una guía breve en español para cargar Excel, reasociar fotos, imprimir, recuperar tarjetas incompletas y revisar la cola.
5. Se documentan estación, versión del controlador, configuración de cinta, modalidad de dos caras y valores de calibración utilizados en las pruebas.

La decisión tecnológica y arquitectura están en [evaluacion-tecnologica.md](evaluacion-tecnologica.md). La evidencia de origen está en [analisis-material.md](analisis-material.md). Esta entrega contiene especificaciones: la aplicación y las pruebas de aceptación todavía no están implementadas ni ejecutadas.

El desarrollo se organiza en [plan-implementacion.md](plan-implementacion.md), con etapas, tareas y comprobaciones para comenzar al abrir el proyecto en Windows.
