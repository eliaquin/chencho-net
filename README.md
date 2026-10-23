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

La solución incluye `Carnets.Core` (datos, validación, fotos, configuración y planificación de envíos), `Carnets.Desktop` (WPF) y `Carnets.Tests`. La interfaz está en implementación; el núcleo tiene pruebas funcionales con el Excel incluido y datos sintéticos.

La simulación es un adaptador local determinista, no un emulador del firmware de Zebra. Permite probar trabajos de ambas caras, copias, reinserción manual y fallos parciales sin impresora. Ninguna simulación acredita calidad, alimentación o calibración físicas. Véase [verificación Windows](docs/verificacion-windows.md).
