# Carnets ICPARD · guía de operación

Versión 1.0.0-preview.1 para Windows x64. Funciona localmente y sin Internet. Esta entrega permite operar la aplicación y el simulador; la aceptación física de Zebra y el arte institucional final siguen pendientes.

## Instalar y abrir

1. Extraiga el ZIP completo en una carpeta local.
2. Ejecute `powershell -ExecutionPolicy Bypass -File .\Install.ps1`. Instala para su usuario y crea un acceso en el menú Inicio, sin modificar sus ajustes existentes. Para colocar también un acceso en el escritorio, agregue `-DesktopShortcut`.
3. Abra **Carnets ICPARD** desde Inicio. También puede ejecutar directamente `app\Carnets.Desktop.exe` sin instalar. El paquete incluye .NET y WPF: no necesita descargar runtimes ni tener Excel instalado.
4. Para actualizar, cierre la aplicación y ejecute el instalador del nuevo paquete. Los ajustes compatibles permanecen bajo `%LocalAppData%\ChenchoNet`, fuera de la carpeta del programa. No se guardan fichas ni selecciones.

## Probar sin impresora

Pulse **Datos de prueba** para cargar dos titulares ficticios y fotografías sintéticas. La aplicación cambia a simulación automática. No se selecciona a nadie: marque las casillas, indique copias y pulse **Revisar selección e imprimir…**.

En la confirmación verá todos los titulares, ambas vistas, cantidad de tarjetas y destino **Cola virtual local**. Confirmar en simulación no envía nada a Windows ni a una impresora. En Configuración puede elegir **Simulación manual guiada** para probar la pausa frente/reverso. El selector de escenarios permite fallar en el segundo trabajo o producir un resultado incierto. Con dos titulares podrá observar un lote parcialmente enviado.

La simulación prueba el flujo de la aplicación; no emula firmware, cinta, alimentación, color ni calibración de la Zebra ZC300. El emulador ZXP de Zebra no se considera un sustituto comprobado para ZC300.

## Cargar titulares de Excel

1. Pulse **Cargar Excel**, seleccione un `.xlsx` y elija la hoja. `Hoja1` se sugiere cuando existe. La primera fila debe contener los encabezados.
2. Se reconocen los once campos del modelo: nombre, apellido, cédula, matrícula, filial, segundo nombre, segundo apellido, exequátur, admisión, expiración y fotos. Si faltan asociaciones o hay encabezados ambiguos, elija manualmente cada columna. El número de columna distingue encabezados iguales. Puede revisar la asociación con **Asociar columnas**.
3. El formato de fechas de texto inicial es `dd/MM/yyyy`. Las fechas numéricas de Excel respetan el calendario del libro. Para otro formato de texto, elíjalo en Configuración y vuelva a cargar.
4. El archivo original nunca se modifica. Una lectura fallida conserva la carga anterior. Una carga correcta limpia toda la selección.
5. Revise los errores de cada fila. Duplicados de matrícula, obligatorios ausentes, fechas inválidas o expiración anterior a admisión bloquean las filas afectadas. Los carnets vencidos requieren reconocimiento en la confirmación, sin cambiar sus fechas.
6. Si aparece advertencia sobre identificadores numéricos, compruebe sus ceros en Excel. La aplicación conserva máscaras explícitas de ceros, pero no puede recuperar ceros que Excel ya perdió.

## Fotos y vista previa

Seleccione **Carpeta de fotos**. `FOTOS` puede contener una ruta absoluta, ruta relativa o nombre PNG/JPEG. Si una ruta antigua corresponde a otra computadora, seleccione la fila y use **Buscar nombre en carpeta**, con confirmación explícita. Si hay varios archivos coincidentes, elija uno con **Elegir foto…**.

Solo cuando `FOTOS` está vacío se busca el nombre exacto de la matrícula en la carpeta, sin recorrer subcarpetas. Una referencia explícita fallida nunca se sustituye por otra foto automáticamente.

Use acercamiento y posición horizontal/vertical para encuadrar sin deformar. El archivo original permanece intacto; el recorte dura la sesión. Se avisa antes de perder recortes al cerrar o recargar. La resolución efectiva menor a 150 ppp produce advertencia. La vista del reverso es institucional e idéntica para todos.

El zoom de pantalla no modifica las medidas de impresión. **Exportar vista PNG** guarda únicamente la vista elegida a 300 ppp en la ubicación indicada; es una exportación explícita con los datos visibles. Mantenga esos archivos bajo su control.

## Seleccionar y enviar

1. Busque por matrícula, cédula, nombre o apellido. Los filtros ignoran acentos y mayúsculas. Ordene pulsando los encabezados.
2. Marque titulares individualmente o use **Seleccionar resultados**. La selección se conserva al filtrar; el contador indica seleccionados ocultos. **Limpiar selección** quita todos. **Quitar seleccionados con error** requiere esa acción explícita.
3. Indique de 1 a 100 copias enteras. Tarjetas = titulares seleccionados × copias; dos caras corresponden a una tarjeta.
4. Pulse **Revisar selección e imprimir…**. Revise la lista completa, impresora/modalidad y ambas vistas. Las advertencias aparecen al elegir cada titular. Si hay vencimientos, **Confirmar** permanece deshabilitado hasta marcar su reconocimiento explícito; desmarcarlo vuelve a bloquear la confirmación.
5. Confirme cada envío. El doble clic no crea otro lote; durante el envío no puede cambiar archivo, configuración, selección ni recortes.

La modalidad normal siempre es de dos caras. En automático se prepara un trabajo de dos páginas por titular con sus copias. En manual se completa una tarjeta a la vez: frente, pausa para comprobar salida y reinsertar, confirmación y reverso. Nunca mezcle tarjetas de titulares diferentes entre ciclos.

**Detener pendientes** evita nuevos envíos. No cancela trabajos que Windows ya aceptó. **Consultar cola** muestra los estados disponibles; para cancelar un trabajo aceptado use Configuración de Windows → Impresoras y escáneres → su impresora → Abrir cola.

El resumen distingue tarjetas con ambas caras aceptadas por cola, pendientes, incompletas, fallidas y desconocidas. Ni Windows ni el simulador acreditan salida física correcta. Ante un resultado incierto revise la impresora y la cola antes de repetir; no existe reintento ni reanudación automática.

## Recuperar una tarjeta incompleta

Después de revisar el estado físico, seleccione un único titular, una copia y **Recuperación explícita · solo frente** o **solo reverso**. Identifique la matrícula y la cara en la confirmación. Un envío de recuperación se registra como una cara enviada; no se afirma que la tarjeta esté físicamente completa. Vuelva a elegir la modalidad normal para un carnet nuevo.

## Preparar una Zebra real

La estación actual no tiene ZC300 y no se validó ninguna modalidad física. Instale el controlador oficial de la unidad y seleccione expresamente su cola. Nunca se cambia a otra impresora si desaparece.

Use **Prueba de calibración…** para enviar datos ficticios con marcas a 3 mm. En manual escriba antes las instrucciones de alimentación y reinserción que probará. Mida tres tarjetas: desplazamiento máximo 1 mm y variación de escala máxima 1 %. Ajuste offsets por cara y giro del reverso (0°/180°), y compruebe asociación, orden y orientación con distintos titulares/copias.

Solo después de comprobarlo marque la modalidad físicamente validada en Configuración. La validación se vincula a la impresora exacta. Cinta y panel negro se configuran en el controlador Zebra.

El perfil inicial `%LocalAppData%\ChenchoNet\profile.json` contiene una composición limpia, pero el sello es provisional y faltan las firmas originales. El responsable del arte debe colocar los PNG/JPEG originales dentro de esa misma carpeta, referenciarlos mediante rutas relativas en `Logo`, `PresidentSignature` y `SecretarySignature`, revisar la calidad al tamaño final y aprobar el perfil mediante `ArtworkApproved`. No copie la fotografía completa del carnet como fondo. La impresión de producción queda bloqueada hasta aprobar el arte y la modalidad física.

## Diagnóstico reproducible del paquete

```powershell
$app = '.\app\Carnets.Desktop.exe'
$check = Start-Process -FilePath $app -ArgumentList @('--self-check', 'C:\ruta\sin-espacios\verificacion') -PassThru -Wait -WindowStyle Hidden
$check.ExitCode
```

Use una carpeta nueva para el diagnóstico. Solo usa datos ficticios y configuración aislada; escribe `self-check.json`, vistas PNG y un libro sintético de rendimiento. El código 0 indica éxito del diagnóstico de software. No imprime, no accede a impresoras y no valida hardware ni arte final. `--demo` abre la aplicación con datos ficticios y selección vacía.
