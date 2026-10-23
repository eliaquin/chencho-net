# Verificación en Windows

Fecha: 23 de octubre de 2026. Estación Windows 10.0.26300, arquitectura x64. SDK 10.0.400; runtime Microsoft.WindowsDesktop.App 10.0.11.

## Tarea 1: solución y núcleo

- Se conservaron los materiales originales. El test del libro real comprueba once encabezados, dos filas, lectura de fechas y bytes idénticos antes/después.
- `dotnet build Carnets.slnx --no-restore`: correcto, cero advertencias y errores antes de continuar con la interfaz.
- `dotnet test tests/Carnets.Tests --no-restore`: 34 pruebas aprobadas. Incluyen normalización y ambigüedad, fechas 1900/1904, fecha ficticia 29/02/1900, formato de identificadores, fórmulas cacheadas/sin caché, duplicados, búsqueda, selección, configuración corrupta y escritura atómica.
- Pruebas de planificación: dúplex agrupado por titular, copias, frente/pausa/reverso por ciclo manual, detención, fallo parcial, resultado desconocido, foto cambiada y exclusión mutua. La cola simulada conserva trabajos únicamente en memoria.
- Ventana WPF inicial iniciada correctamente: título `MainWindow`, handle de ventana distinto de cero, cierre solicitado sin finalizar forzosamente el proceso.
- Rendimiento de validación: prueba de 1.000 filas por debajo de 5 segundos. Todavía falta medir lectura completa de libro y vista previa.
- Revisión propia antes de merge: comprobados los límites del lector XML (DTD deshabilitada y tamaño acotado), las referencias explícitas de fotos sin sustituciones silenciosas, las fechas sin hora, la ausencia de datos personales en ajustes y los estados parciales sin reintento automático. `git diff --check` sin incidencias.

## Tarea 2: interfaz y composición

- Interfaz WPF en español con selección explícita, búsqueda por campos sin acentos, asociación de columnas, carpeta de fotos, reasociación explícita, encuadre y revisión completa de seleccionados (incluidos ocultos).
- Carga y decodificación en tareas de fondo. La sesión anterior se reemplaza únicamente después de leer, asociar, validar y guardar correctamente los nuevos ajustes.
- Perfil local versionado CR80 y un solo compositor para renderizar ambas caras. Diez valores variables, foto con proporción conservada y textos que bloquean la fila si no caben a 7 puntos. El reverso es independiente de los datos personales.
- `dotnet test tests/Carnets.Tests --no-restore`: 40 pruebas aprobadas, incluidas seis pruebas WPF en STA. Se renderizaron el frente/reverso sintéticos y la ventana real en `artifacts/verification/` (no versionados). Inspección visual realizada; se corrigió el desbordamiento de la cabecera institucional antes de mergear.
- Revisión propia: se corrigió la selección por casilla dentro de la tabla de solo lectura y se situó la revisión de selección inmediatamente después de la vista previa. La configuración no cambia de impresora automáticamente y los perfiles corruptos requieren restablecimiento explícito conservando copia.
- La interfaz de envío, el seguimiento de cola y el paquete se completan en las tareas siguientes. Logo y firmas siguen siendo recursos de producción pendientes; el perfil inicial no se declara aprobado.

## Tarea 3: envío, simulación y recuperación

- Confirmación por envío con impresora/modalidad, lista íntegra de titulares, ambas vistas, advertencias y reconocimiento obligatorio de vencimientos. Copias 1–100. Recuperación explícita de una cara para una tarjeta identificada.
- Simulador local automático/manual con fallo controlado del segundo trabajo y resultado incierto. Detención de pendientes, bloqueo de cambios durante el envío y resumen de sesión únicamente en memoria.
- Adaptador Windows WPF/XPS con tamaño CR80, escala 100 %, orientación, dúplex/copia validados contra el controlador y un documento de dos páginas por titular. Conserva identificadores de cola cuando siguen disponibles y consulta sus estados sin afirmar salida física.
- El modo real requiere arte aprobado y validación física de la impresora exacta. La prueba de calibración con datos ficticios y marcas a 3 mm es una acción explícita anterior a la aceptación; no habilita por sí sola producción.
- `dotnet test tests/Carnets.Tests --no-restore`: 44 pruebas aprobadas. Nuevas comprobaciones cubren arte no aprobado, impresora distinta, instrucciones manuales incompletas, fotos corruptas y recursos modificados después de revisión.
- Revisión propia antes del merge: corregida la posibilidad de releer arte mutable durante el envío. Se congelan los dibujos preparados y se comprueba la huella de los recursos antes/después de prepararlos. Las fotos cambiadas requieren nueva revisión; no se reenvía automáticamente un resultado incierto. `git diff --check` correcto.
- La integración del controlador, el orden físico de caras y los fallos reales de ZC300 permanecen sin ejecutar por ausencia de hardware. No se cuentan como pruebas aprobadas.

## Impresora y límites actuales

`Get-Printer` enumera OneNote, Microsoft Print to PDF, Fax y EPSON L4260; no hay ZC300 instalada. El usuario confirmó que no dispone de una ZC300 y solicitó explorar la emulación.

Zebra documenta un emulador para ZXP Series 7/8/9; no se ha encontrado uno específico oficial para ZC300 en su [catálogo de software de tarjetas](https://www.zebra.com/us/en/software/printer-software/card-printers.html). No se considera compatible con ZC300 sin evidencia.

Se implementa simulación de la aplicación. La prueba física, el perfil de controlador, cinta, alimentación, giro, calibración y aceptación del arte final siguen pendientes; no se marcan aprobados a partir de la simulación.
