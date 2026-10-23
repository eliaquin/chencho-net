# Componentes y distribución

La aplicación usa C#, WPF y las bibliotecas de .NET 10. El paquete autocontenido incluye en `third-party/` las licencias y avisos de terceros de .NET Runtime y WPF 10.0.11, obtenidos de sus etiquetas oficiales y conservados sin modificar. Código y licencias de los proyectos de Microsoft: [dotnet/runtime](https://github.com/dotnet/runtime/tree/v10.0.11), [dotnet/wpf](https://github.com/dotnet/wpf/tree/v10.0.11).

El lector de XLSX utiliza `System.IO.Compression`, `System.Xml` y `System.Xml.Linq` de .NET. No automatiza Excel ni incluye motores comerciales de hojas de cálculo.

Las dependencias de pruebas (xUnit, test SDK, runner y coverlet) están fijadas en `tests/Carnets.Tests/Carnets.Tests.csproj`; no se distribuyen con la aplicación publicada. El repositorio no declara una licencia de código abierto para el código propio.

El paquete no distribuye el Excel, fotografías, RAR o video personales de `docs/`. El arte institucional original no se suministra: el operador debe aportar recursos cuyo uso esté autorizado. Los gráficos de demostración son figuras geométricas y datos ficticios generados localmente.
