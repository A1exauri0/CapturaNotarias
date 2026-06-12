using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    /// <summary>
    /// Servidor HTTP local integrado en la app de WinForms.
    /// Se activa solo si la PC está configurada como "EsServidor = true".
    /// Recibe registros de auditoría de las PCs cliente vía POST.
    /// </summary>
    public static class ServidorHttpLocal
    {
        private static HttpListener? _listener;
        private static CancellationTokenSource? _tokenCancelacion;
        private static Task? _hiloServidor;
        private static bool _ejecutando = false;

        /// <summary>
        /// Inicia el servidor HTTP en el puerto configurado.
        /// Solo se llama si EsServidor = true.
        /// </summary>
        public static void Iniciar()
        {
            if (_ejecutando) return;

            try
            {
                int puerto = ModuloConfiguracion.PUERTO_SERVIDOR;
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{puerto}/");
                _listener.Start();

                _ejecutando = true;
                _tokenCancelacion = new CancellationTokenSource();
                _hiloServidor = Task.Run(() => EscucharPeticiones(_tokenCancelacion.Token));

                System.Diagnostics.Debug.WriteLine($"Servidor HTTP local iniciado en puerto {puerto}");
            }
            catch (HttpListenerException)
            {
                // Si falla por permisos, intentar con localhost solamente
                try
                {
                    int puerto = ModuloConfiguracion.PUERTO_SERVIDOR;
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{puerto}/");
                    _listener.Prefixes.Add($"http://127.0.0.1:{puerto}/");
                    // También intentar con la IP local de la máquina
                    string ipLocal = ObtenerIpLocal();
                    if (!string.IsNullOrEmpty(ipLocal))
                        _listener.Prefixes.Add($"http://{ipLocal}:{puerto}/");

                    _listener.Start();
                    _ejecutando = true;
                    _tokenCancelacion = new CancellationTokenSource();
                    _hiloServidor = Task.Run(() => EscucharPeticiones(_tokenCancelacion.Token));
                }
                catch (Exception exInterna)
                {
                    System.Diagnostics.Debug.WriteLine("No se pudo iniciar el servidor HTTP: " + exInterna.Message);
                }
            }
        }

        /// <summary>
        /// Detiene el servidor HTTP.
        /// </summary>
        public static void Detener()
        {
            _ejecutando = false;
            _tokenCancelacion?.Cancel();
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
        }

        /// <summary>
        /// Verifica si el servidor está activo.
        /// </summary>
        public static bool EstaActivo => _ejecutando && _listener != null && _listener.IsListening;

        // ── Bucle principal de escucha ──

        private static async Task EscucharPeticiones(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    // Esperar una petición entrante
                    var contexto = await _listener.GetContextAsync().ConfigureAwait(false);

                    // Procesar cada petición en su propio hilo para no bloquear
                    _ = Task.Run(() => ProcesarPeticion(contexto), token);
                }
                catch (HttpListenerException)
                {
                    // El listener fue cerrado, salir del bucle
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    // Error inesperado, esperar un momento y continuar
                    try { await Task.Delay(500, token); } catch { break; }
                }
            }
        }

        private static void ProcesarPeticion(HttpListenerContext contexto)
        {
            try
            {
                var peticion = contexto.Request;
                var respuesta = contexto.Response;

                // Determinar la ruta solicitada
                string ruta = peticion.Url?.AbsolutePath?.ToLower() ?? "";

                // Encabezados CORS para permitir cualquier origen
                respuesta.Headers.Add("Access-Control-Allow-Origin", "*");
                respuesta.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
                respuesta.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                // Manejar preflight de CORS
                if (peticion.HttpMethod == "OPTIONS")
                {
                    respuesta.StatusCode = 200;
                    respuesta.Close();
                    return;
                }

                // ── Rutas disponibles ──

                if (ruta == "/api/auditoria" && peticion.HttpMethod == "POST")
                {
                    ProcesarRegistroAuditoria(peticion, respuesta);
                }
                else if (ruta == "/api/auditoria/lote" && peticion.HttpMethod == "POST")
                {
                    ProcesarLoteAuditoria(peticion, respuesta);
                }
                else if (ruta == "/api/ping" && peticion.HttpMethod == "GET")
                {
                    // Endpoint de salud para verificar que el servidor responde
                    ResponderJson(respuesta, 200, new { ok = true, mensaje = "Servidor activo" });
                }
                else
                {
                    ResponderJson(respuesta, 404, new { error = "Ruta no encontrada" });
                }
            }
            catch (Exception ex)
            {
                try
                {
                    ResponderJson(contexto.Response, 500, new { error = "Error interno: " + ex.Message });
                }
                catch { }
            }
        }

        // ── Procesadores de endpoints ──

        /// <summary>
        /// Recibe un solo registro de auditoría y lo inserta en la BD local del servidor.
        /// </summary>
        private static void ProcesarRegistroAuditoria(HttpListenerRequest peticion, HttpListenerResponse respuesta)
        {
            string cuerpo = LeerCuerpo(peticion);
            if (string.IsNullOrEmpty(cuerpo))
            {
                ResponderJson(respuesta, 400, new { error = "Cuerpo vacío" });
                return;
            }

            var registro = JsonConvert.DeserializeObject<RegistroAuditoria>(cuerpo);
            if (registro == null || string.IsNullOrEmpty(registro.ArchivoOriginal))
            {
                ResponderJson(respuesta, 400, new { error = "Registro inválido" });
                return;
            }

            // Insertar directamente en la BD SQLite del servidor
            bool insertado = RepositorioAuditoria.InsertarRegistroMigracion(registro);

            if (insertado)
            {
                ResponderJson(respuesta, 200, new { ok = true, mensaje = "Registro recibido" });
            }
            else
            {
                // Si InsertarRegistroMigracion retorna false, probablemente es duplicado (UNIQUE INDEX)
                ResponderJson(respuesta, 200, new { ok = true, mensaje = "Registro duplicado, ignorado" });
            }
        }

        /// <summary>
        /// Recibe un lote de registros (para reintentos acumulados).
        /// </summary>
        private static void ProcesarLoteAuditoria(HttpListenerRequest peticion, HttpListenerResponse respuesta)
        {
            string cuerpo = LeerCuerpo(peticion);
            if (string.IsNullOrEmpty(cuerpo))
            {
                ResponderJson(respuesta, 400, new { error = "Cuerpo vacío" });
                return;
            }

            var lote = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(cuerpo);
            if (lote?.Registros == null || lote.Registros.Count == 0)
            {
                ResponderJson(respuesta, 400, new { error = "Lote vacío" });
                return;
            }

            int insertados = 0;
            int duplicados = 0;
            foreach (var registro in lote.Registros)
            {
                bool ok = RepositorioAuditoria.InsertarRegistroMigracion(registro);
                if (ok) insertados++;
                else duplicados++;
            }

            ResponderJson(respuesta, 200, new
            {
                ok = true,
                insertados,
                duplicados,
                mensaje = $"Lote procesado: {insertados} nuevos, {duplicados} duplicados"
            });
        }

        // ── Utilidades ──

        private static string LeerCuerpo(HttpListenerRequest peticion)
        {
            using (var lector = new StreamReader(peticion.InputStream, peticion.ContentEncoding))
            {
                return lector.ReadToEnd();
            }
        }

        private static void ResponderJson(HttpListenerResponse respuesta, int codigoEstatus, object datos)
        {
            string json = JsonConvert.SerializeObject(datos);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            respuesta.StatusCode = codigoEstatus;
            respuesta.ContentType = "application/json";
            respuesta.ContentLength64 = buffer.Length;
            respuesta.OutputStream.Write(buffer, 0, buffer.Length);
            respuesta.Close();
        }

        private static string ObtenerIpLocal()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            catch { }
            return "";
        }
    }
}
