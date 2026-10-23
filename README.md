# Impresión de carnets

Aplicación de escritorio prevista para C#, WPF y .NET 10, desarrollada y probada directamente en Windows. Cargará titulares desde Excel e imprimirá frente y reverso en una Zebra ZC300.

## Documentación

- [Plan de implementación por etapas](docs/plan-implementacion.md): tareas, dependencias, comprobaciones y estado para retomar el desarrollo.
- [Especificaciones versión 1.0](docs/especificaciones-carnets.md): alcance, campos, comportamiento y 25 criterios de aceptación.
- [Decisión tecnológica y arquitectura](docs/evaluacion-tecnologica.md): solución .NET, composición y adaptación a impresión Windows.
- [Análisis del material](docs/analisis-material.md): evidencia del Excel, fotografías y video incluidos en `docs/`.

## Al abrir el proyecto en Windows

Comenzar por **E0.1 del plan de implementación**. La solución .NET todavía no está creada. Los comandos definitivos de compilación, prueba y ejecución se añadirán aquí durante la etapa 0, después de comprobarlos en Windows.

Las primeras comprobaciones del entorno serán `dotnet --info` y `dotnet --list-sdks`. Los materiales originales se conservarán intactos; las pruebas usarán carpetas de trabajo y datos sintéticos cuando corresponda.
