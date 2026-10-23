# Matriz de aceptación

23 de octubre de 2026. Implementación Windows/WPF .NET 10. La evidencia de software no equivale a aceptación física. Se usa una cola simulada de la aplicación, no un firmware emulado de ZC300.

## Evidencia reproducible

`dotnet test tests/Carnets.Tests`: 56 pruebas de Core y WPF, incluyendo archivo y fotos originales, fixtures sintéticos, ventana y diálogos reales. El diagnóstico `--self-check` ejecutado desde el ZIP extraído e instalado por usuario termina con código 0; su informe está en [evidencia-software.json](evidencia-software.json). La reinstalación sin elevación conserva ajustes byte por byte; véase [evidencia-instalacion.json](evidencia-instalacion.json). Las vistas y el XLSX sintético de 1.000 filas se generan bajo `artifacts/` y no se versionan.

La estación no dispone de Zebra ZC300, según inventario y confirmación del usuario. No hay modalidad física instalada que pueda marcarse como no aplicable frente a otra ya aceptada: ambas conservan validación pendiente. El sello inicial es provisional; logo y firmas originales y su revisión al tamaño final siguen pendientes.

## Criterios

| ID | Evidencia actual de software | Aceptación restante |
| --- | --- | --- |
| CA-01 | `IncludedWorkbookHasElevenColumnsAndTwoRows`, fechas y original sin cambios; fotos reales decodificadas con reasociación explícita | Repetición del flujo de operador con el arte final |
| CA-02 | `ReloadKeepsAssociationsClearsSelectionAndBadFilePreservesPriorSession`; recarga real con selección/filtro previos, 11 asociaciones y fallo sin reemplazar la sesión | Repetir en la estación definitiva |
| CA-03 | Normalización/ambigüedad comprobadas; diálogo WPF real con elección de la segunda columna equivalente; asociaciones con columnas reordenadas | Comprobado en software; repetir con los archivos operativos |
| CA-04 | `ValuesPreserveIdentifiersAndAccentsAndSeparateOptionalNames`, máscaras numéricas con ceros y fechas estrictas | Aprobación visual al tamaño impreso |
| CA-05 | `ExplicitPhotoFailureDoesNotFallbackAndRelocationIsExplicit` y reasociación de las dos fotos del RAR | Recorrido de operador con su carpeta de trabajo |
| CA-06 | Fotos ausentes/ambiguas/corruptas bloqueadas; decodificación entre hilos comprobada | Comprobado en pruebas de software; repetir en estación final |
| CA-07 | Duplicados bloqueados, opcionales `-`, texto largo bloqueado a 7 pt y geometría sin superposición | Aprobación física de legibilidad |
| CA-08 | Dos trabajos de ambas caras con dos copias por titular comprobados en simulación desde el paquete | ZC300 real: agrupación respetada por controlador y tarjetas correctas |
| CA-09 | `ConcurrentSendIsRejected` y `CancellingPrintConfirmationCreatesNoSimulatedBatch`; exclusión y cancelación en la confirmación real con lista completa | Repetición final de operación física |
| CA-10 | Impresora exacta, sin sustitución; fallos simulados y bloqueo de producción sin perfil validado | Impresora real ausente/desconectada y fallo del controlador |
| CA-11 | Recibos y resumen distinguen aceptación de cola de salida física; cola virtual explícita | Estado/identificadores de cola Windows con ZC300 |
| CA-12 | Fallo parcial/resultado incierto sin reintento; datos y selección solo en memoria | Interrumpir un lote real y reiniciar la aplicación en estación final |
| CA-13 | Patrón CR80 con marcas a 3 mm y ajustes por cara implementados | **Pendiente: tres tarjetas reales, ≤1 mm de desplazamiento y ≤1 % de escala, con arte final** |
| CA-14 | Publicación autocontenida; importación/composición/simulación sin clientes de red ni servicios externos | Verificar operación con la conexión de la estación físicamente desactivada; no se desconectó la red del usuario |
| CA-15 | Geometría física en DIP invariable, zoom de pantalla independiente y WPF con barras de desplazamiento | Recorrido de teclado y operación con escalado real de Windows 100/125/150 % |
| CA-16 | Diez campos, foto, Provincia y nombres separados; renderización con datos sintéticos y con el Excel/fotos originales | Logo original y aprobación visual impresa |
| CA-17 | `BackContainsNoVariableDataAndRemainsSameAcrossHolders`; posiciones/textos fijos en perfil | Firmas originales y aprobación visual impresa |
| CA-18 | Ocho caras, cuatro ciclos manuales y cuatro pausas comprobados desde el paquete; cancelación deja incompleta sin avanzar | Alimentación y reinserción comprobadas en ZC300 real |
| CA-19 | `OperatorTableKeepsSelectionWhenFilteringAndSortingAndCropDoesNotChangePhoto`; casilla real, filtro/orden y conteo de ocultos | Lista completa implementada en confirmación; recorrido manual final |
| CA-20 | Fecha invertida bloqueada; prueba del diálogo real: confirmación deshabilitada sin reconocimiento, desmarcar vuelve a bloquear, envío simulado aceptado conservando la fecha | Repetir con operación física |
| CA-21 | Configuración versionada y atómica; reinstalación sin elevación conserva ajustes compatibles byte por byte y diagnóstico instalado pasa | Recorrido manual del restablecimiento explícito de un perfil corrupto en estación final |
| CA-22 | 1.000 filas XLSX leídas/validadas; ventana y vistas dentro de objetivos desde paquete instalado | Repetir en la estación definitiva con su escalado |
| CA-23 | Detención de pendientes y recuperación explícita de cara implementadas; aceptados no cancelados automáticamente | Recuperación de una tarjeta física identificada y consulta/cancelación en cola real |
| CA-24 | Huellas de fotos/recursos, fallo si cambian, copia de píxeles inmutable y dibujos congelados antes de enviar | Repetición con un lote del controlador real y revisión del resumen parcial |
| CA-25 | Valores inválidos de copias rechazados y multiplicación comprobada en simulación | Dos copias físicas por titular, sin páginas/caras cruzadas |

## Definición de terminado

El software y el paquete se entregan para evaluación en simulación. **La definición completa de terminado de las especificaciones no está satisfecha**: faltan arte final, revisión del operador y evidencia física de la ZC300. No se marca la etapa 1 ni la aceptación de impresión de la etapa 7 como completadas. No se reemplazan esos requisitos por pruebas a PDF o una cola virtual.
