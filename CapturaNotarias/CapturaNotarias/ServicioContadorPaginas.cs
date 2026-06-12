using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CapturaNotarias
{
    /// <summary>
    /// Cola asíncrona para contar páginas de PDFs en background.
    /// El watcher registra con paginas=0 y encola aquí. El worker actualiza después.
    /// </summary>
    public static class ServicioContadorPaginas
    {
        private static readonly ConcurrentQueue<(long id, string rutaArchivo)> _cola = new();
        private static CancellationTokenSource? _tokenCancelacion;
        private static Task? _trabajador;
        private static bool _ejecutando = false;

        /// <summary>
        /// Referencia al formulario activo para actualizar el indicador de conexión.
        /// </summary>
        public static FormCaptura? FormularioActivo { get; set; }

        /// <summary>
        /// Inicia el worker en background. Se llama una vez al iniciar la app.
        /// </summary>
        public static void Iniciar()
        {
            if (_ejecutando) return;
            _ejecutando = true;
            _tokenCancelacion = new CancellationTokenSource();
            _trabajador = Task.Run(() => ProcesarCola(_tokenCancelacion.Token));
        }

        /// <summary>
        /// Detiene el worker. Se llama al cerrar la app.
        /// </summary>
        public static void Detener()
        {
            _ejecutando = false;
            _tokenCancelacion?.Cancel();
        }

        /// <summary>
        /// Agrega un PDF a la cola para conteo de páginas.
        /// </summary>
        public static void Encolar(long idRegistro, string rutaArchivo)
        {
            if (idRegistro > 0 && !string.IsNullOrEmpty(rutaArchivo))
            {
                _cola.Enqueue((idRegistro, rutaArchivo));
            }
        }

        /// <summary>
        /// Cuenta páginas de forma síncrona (para reconteo manual).
        /// No espera a que el archivo se estabilice.
        /// </summary>
        public static int ContarPaginasSincrono(string rutaCompleta)
        {
            if (string.IsNullOrEmpty(rutaCompleta) || !File.Exists(rutaCompleta))
                return 0;

            // Intentar con PDFsharp primero (más confiable que regex)
            int paginas = ContarConPdfSharp(rutaCompleta);
            if (paginas > 0) return paginas;

            // Fallback a regex para PDFs que PDFsharp no pueda abrir
            paginas = ContarConRegex(rutaCompleta);
            if (paginas > 0) return paginas;

            return 1; // Mínimo 1 página si el archivo existe
        }

        // ── Worker en background ──

        private static async Task ProcesarCola(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_cola.TryDequeue(out var item))
                    {
                        await ContarConReintentos(item.id, item.rutaArchivo, token);
                    }
                    else
                    {
                        // También verificar si hay registros pendientes en la BD
                        // (por si se encolaron antes de que el worker iniciara)
                        var pendientes = RepositorioAuditoria.ObtenerPendientesPaginas();
                        foreach (var (id, ruta) in pendientes)
                        {
                            if (token.IsCancellationRequested) break;
                            await ContarConReintentos(id, ruta, token);
                        }

                        // Esperar antes de volver a buscar
                        await Task.Delay(2000, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Si algo falla, esperamos un poco y seguimos
                    try { await Task.Delay(1000, token); } catch { break; }
                }
            }
        }

        private static async Task ContarConReintentos(long id, string ruta, CancellationToken token)
        {
            // Máximo 5 intentos con espera incremental
            for (int intento = 0; intento < 5; intento++)
            {
                if (token.IsCancellationRequested) return;

                // Esperar a que el archivo se estabilice (el escáner termine de escribir)
                bool listo = await EsperarArchivoListo(ruta, token);
                if (!listo)
                {
                    // El archivo no se estabilizó, esperar e intentar de nuevo
                    try { await Task.Delay(3000 * (intento + 1), token); } catch { return; }
                    continue;
                }

                int paginas = ContarPaginasSincrono(ruta);
                if (paginas > 0)
                {
                    RepositorioAuditoria.ActualizarPaginas(id, paginas);

                    // Enviar al servidor local en tiempo real (solo si NO somos el servidor)
                    await EnviarAlServidorLocal(id);
                    return;
                }

                // Si no se pudo contar, esperar e intentar de nuevo
                try { await Task.Delay(2000 * (intento + 1), token); } catch { return; }
            }

            // Después de todos los intentos, asignar 1 página como fallback
            RepositorioAuditoria.ActualizarPaginas(id, 1);
            await EnviarAlServidorLocal(id);
        }

        /// <summary>
        /// Envía el registro completo al servidor HTTP local después de contar páginas.
        /// Si esta PC ES el servidor, no necesita enviarse a sí misma.
        /// </summary>
        private static async Task EnviarAlServidorLocal(long idRegistro)
        {
            // Si esta PC es el servidor, ya tiene el registro en su propia BD
            if (ModuloConfiguracion.EsServidor) return;

            bool exito = false;
            try
            {
                // Intentar enviar los registros no exportados al servidor
                var pendientes = RepositorioAuditoria.ObtenerRegistrosNoExportados();
                if (pendientes.Count > 0)
                {
                    exito = await ClienteHttpLocal.EnviarLoteAsync(pendientes);
                    if (exito)
                    {
                        RepositorioAuditoria.MarcarTodosComoExportadoRed();
                    }
                }
                else
                {
                    // No hay pendientes, pero verificamos conexión con un ping
                    exito = await ClienteHttpLocal.PingAsync();
                }
            }
            catch
            {
                exito = false;
            }

            // Actualizar indicador visual en el formulario
            FormularioActivo?.ActualizarEstadoConexion(exito);
        }

        /// <summary>
        /// Espera a que el archivo deje de ser escrito (tamaño estable por 2 segundos).
        /// Versión asíncrona que no bloquea hilos.
        /// </summary>
        private static async Task<bool> EsperarArchivoListo(string ruta, CancellationToken token, int timeoutSegundos = 30)
        {
            // Tiempo de gracia inicial para que el escáner comience a escribir
            try { await Task.Delay(1000, token); } catch { return false; }

            long ultimoTamano = -1;
            int vecesIgual = 0;

            for (int i = 0; i < timeoutSegundos * 2; i++)
            {
                if (token.IsCancellationRequested) return false;

                try
                {
                    if (File.Exists(ruta))
                    {
                        var info = new FileInfo(ruta);
                        long tamanoActual = info.Length;

                        if (tamanoActual > 0)
                        {
                            if (tamanoActual == ultimoTamano)
                            {
                                vecesIgual++;
                                // Si el tamaño no cambia por 2 segundos (4 iteraciones), el archivo está listo
                                if (vecesIgual >= 4)
                                {
                                    // Verificar que se puede abrir para lectura
                                    try
                                    {
                                        using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                        {
                                            return true;
                                        }
                                    }
                                    catch
                                    {
                                        vecesIgual = 0; // El archivo sigue bloqueado, seguir esperando
                                    }
                                }
                            }
                            else
                            {
                                ultimoTamano = tamanoActual;
                                vecesIgual = 0;
                            }
                        }
                    }
                }
                catch
                {
                    vecesIgual = 0;
                }

                try { await Task.Delay(500, token); } catch { return false; }
            }

            return false;
        }

        // ── Métodos de conteo ──

        private static int ContarConPdfSharp(string rutaCompleta)
        {
            try
            {
                using (var fs = new FileStream(rutaCompleta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var documento = PdfSharp.Pdf.IO.PdfReader.Open(fs, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
                    {
                        return documento.PageCount;
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        private static int ContarConRegex(string rutaCompleta)
        {
            try
            {
                using (var fs = new FileStream(rutaCompleta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, System.Text.Encoding.ASCII))
                {
                    string contenido = sr.ReadToEnd();
                    var coincidencias = System.Text.RegularExpressions.Regex.Matches(
                        contenido,
                        @"/Type\s*/Pages[\s\S]*?/Count\s*(\d+)|/Count\s*(\d+)[\s\S]*?/Type\s*/Pages",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    );

                    int maxPaginas = 0;
                    foreach (System.Text.RegularExpressions.Match coincidencia in coincidencias)
                    {
                        string val = !string.IsNullOrEmpty(coincidencia.Groups[1].Value)
                            ? coincidencia.Groups[1].Value
                            : coincidencia.Groups[2].Value;
                        if (int.TryParse(val, out int paginas) && paginas > maxPaginas)
                        {
                            maxPaginas = paginas;
                        }
                    }

                    return maxPaginas;
                }
            }
            catch { }
            return 0;
        }
    }
}
