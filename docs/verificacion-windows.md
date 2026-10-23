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

## Impresora y límites actuales

`Get-Printer` enumera OneNote, Microsoft Print to PDF, Fax y EPSON L4260; no hay ZC300 instalada. El usuario confirmó que no dispone de una ZC300 y solicitó explorar la emulación.

Zebra documenta un emulador para ZXP Series 7/8/9; no se ha encontrado uno específico oficial para ZC300 en su [catálogo de software de tarjetas](https://www.zebra.com/us/en/software/printer-software/card-printers.html). No se considera compatible con ZC300 sin evidencia.

Se implementa simulación de la aplicación. La prueba física, el perfil de controlador, cinta, alimentación, giro, calibración y aceptación del arte final siguen pendientes; no se marcan aprobados a partir de la simulación.
