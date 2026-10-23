# Impresión de carnets

Aplicación de escritorio prevista para C#, WPF y .NET 10, desarrollada y probada directamente en Windows. Cargará titulares desde Excel e imprimirá frente y reverso en una Zebra ZC300.

## Documentación

- [Plan de implementación por etapas](docs/plan-implementacion.md): tareas, dependencias, comprobaciones y estado para retomar el desarrollo.
- [Especificaciones versión 1.0](docs/especificaciones-carnets.md): alcance, campos, comportamiento y 25 criterios de aceptación.
- [Decisión tecnológica y arquitectura](docs/evaluacion-tecnologica.md): solución .NET, composición y adaptación a impresión Windows.
- [Análisis del material](docs/analisis-material.md): evidencia del Excel, fotografías y video incluidos en `docs/`.

## Desarrollo en Windows

SDK .NET 10.0.400, Windows x64. No se necesita Microsoft Excel. La lectura de `.xlsx` utiliza ZIP/XML de .NET sin ejecutar fórmulas, macros o conexiones externas ni incorporar bibliotecas de Excel.

```powershell
dotnet restore Carnets.slnx
dotnet build Carnets.slnx
dotnet test tests/Carnets.Tests
dotnet run --project src/Carnets.Desktop
```

La solución incluye `Carnets.Core` (datos, validación, fotos, configuración y planificación de envíos), `Carnets.Desktop` (WPF) y `Carnets.Tests`. La aplicación permite cargar Excel, asociar fotos, ajustar encuadres, seleccionar titulares, revisar ambas caras y simular o enviar trabajos mediante un perfil Windows validado. **Datos de prueba** permite probarla sin personas reales ni impresora.

La simulación es un adaptador local determinista, no un emulador del firmware de Zebra. Permite probar trabajos de ambas caras, copias, reinserción manual y fallos parciales sin impresora. Ninguna simulación acredita calidad, alimentación o calibración físicas. Véase [verificación Windows](docs/verificacion-windows.md).

## Paquete para Windows

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-Package.ps1
```

Genera `artifacts/release/Carnets-1.0.0-preview.1-win-x64.zip`, autocontenido para Windows x64 con .NET/WPF. Extraiga el ZIP completo y ejecute su `Install.ps1` para instalar por usuario, o abra `app/Carnets.Desktop.exe` directamente. El paquete no contiene materiales personales de `docs/`.

- [Guía de instalación y operación](docs/guia-operacion.md).
- [Matriz de los 25 criterios y límites de aceptación](docs/aceptacion.md).
- [Resultado del diagnóstico desde el paquete instalado](docs/evidencia-software.json).
- [Componentes y licencias](docs/licencias.md).

Estado: software disponible para simulación y pruebas controladas. La entrega final de impresión física requiere una ZC300 real, arte institucional original y aceptación de las tarjetas. El perfil inicial no habilita producción automáticamente.
