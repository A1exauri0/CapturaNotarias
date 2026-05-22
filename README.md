# CapturaNotarias

**CapturaNotarias** es una aplicación moderna de escritorio desarrollada en **C# y .NET 8 (Windows Forms)**. Está diseñada para optimizar y auditar el proceso de captura, previsualización y renombrado de documentos PDF digitalizados provenientes de notarías o escáneres de alto volumen.

## 🚀 Características Principales

- **Vigilancia de Directorios (Watcher):** Monitorea en tiempo real una carpeta específica. Tan pronto como un escáner arroja un nuevo PDF, el sistema lo detecta y lo añade a la cola de trabajo automáticamente.
- **Previsualización Rápida:** Utiliza `WebView2` (tecnología basada en Microsoft Edge/Chromium) para previsualizar los PDFs dentro de la aplicación sin abrir ventanas externas.
- **Renombrado Asistido:** Facilita el renombrado rápido de archivos mediante campos predefinidos: _Mes_, _Año_ y _Folio_, construyendo nombres estructurados de forma automática (Ej: `08_2026_12345.pdf`).
- **Gestión de Usuarios y Turnos:** Control de acceso mediante un PIN Maestro. Soporta el manejo de distintos operadores, asignando turnos de trabajo (_Matutino, Vespertino, Nocturno_) para un control operativo estricto.
- **Auditoría y Trazabilidad Total:**
  - Registra cada acción realizada (Renombrado, Error, Eliminación) en archivos `auditoria.json` locales.
  - Extrae automáticamente el **número total de páginas** de los archivos procesados usando la librería `PDFsharp`.
  - Guarda información sobre el equipo (Nombre de PC), Usuario y Fecha/Hora exacta.
- **Reportes en Excel Automatizados:** Exportación de estadísticas operativas a libros de Excel con formato avanzado (gracias a `ClosedXML`). Agrupa la productividad por Fechas, Turnos y Categorías, generando códigos de colores automatizados y sumatorias de _Archivos Procesados_ y _Páginas Totales_.

## 🛠️ Tecnologías y Librerías Utilizadas

El proyecto utiliza el entorno **.NET 8.0** aprovechando características avanzadas de C# y compilación autocontenida.

- `Microsoft.Web.WebView2` - Para la visualización ágil de PDFs integrados en la interfaz.
- `Newtonsoft.Json` - Para lectura/escritura veloz de la configuración y registros de auditoría.
- `ClosedXML` - Para la generación nativa de hojas de cálculo de Excel (`.xlsx`) sin necesidad de instalar Microsoft Office.
- `PDFsharp` - Para análisis, lectura segura de metadatos y conteo de páginas de los PDFs en segundo plano.

## 📂 Arquitectura de Archivos y Almacenamiento

El programa ha sido diseñado para operar en terminales independientes sin depender forzosamente de una base de datos central.

1.  **Directorio de Red (`\\192.168.1.10\NOTARIAS`)**: Por defecto, al ejecutarse por primera vez en cualquier computadora, la aplicación crea esta carpeta raíz.
2.  **`usuarios.json`**: Almacenado en la raíz local. Contiene el PIN Maestro y el registro encriptado/seguro de todos los capturistas de esa computadora.
3.  **`MonitoreoCaptura\PC-XX\auditoria.json`**: Almacena el historial permanente y trazable del desempeño de la computadora.

_(Nota: La "Ruta del Servidor de Red" es totalmente configurable desde la pantalla de Login mediante el botón de Ajustes)._

## 📦 Despliegue y Distribución (Build)

Para facilitar la distribución en las computadoras de los capturistas, el sistema está configurado para publicarse como **Self-Contained Single File** (Ejecutable de Archivo Único Autocontenido).

Esto significa que **NO es necesario instalar el entorno de .NET 8** en las computadoras de destino.

Para compilar la versión de distribución, puedes utilizar el script incluido:

- `Generar_Ejecutable.bat`

El script generará una carpeta `Compartir` en la raíz del proyecto. El archivo **`CapturaNotarias.exe`** generado dentro de esa carpeta es completamente portable; solo tienes que enviarlo a las computadoras de la red y ejecutarlo.
