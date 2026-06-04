using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    public class RegistroAuditoria
    {
        public string? FechaHora { get; set; }
        public string? Usuario { get; set; }
        public string? NombreCompleto { get; set; }
        public string? Turno { get; set; } // "Matutino", "Vespertino", "Nocturno"
        public string? PC { get; set; }
        public string? IP { get; set; }
        public string? Notaria { get; set; }
        public string? Accion { get; set; }
        public string? ArchivoOriginal { get; set; }
        public string? Detalles { get; set; }
        public int Paginas { get; set; } = 1; // Para guardar el total de imágenes (páginas) del PDF
        public string? LugarTrabajo { get; set; }
        public bool? Enviado { get; set; }
        public string? RutaLocal { get; set; }

        [JsonIgnore]
        public string? RutaJsonOrigen { get; set; }
    }

    public class ArchivoAuditoriaJson
    {
        public List<RegistroAuditoria> Registros { get; set; } = new List<RegistroAuditoria>();
    }

    public static class ModuloAuditoria
    {
        private static readonly object _lockJson = new object();
        private static bool _migracionRealizada = false;

        private static bool EsperarArchivoListo(string ruta, int timeoutSegundos = 300)
        {
            for (int i = 0; i < timeoutSegundos; i++)
            {
                try
                {
                    if (File.Exists(ruta))
                    {
                        var info = new FileInfo(ruta);
                        if (info.Length > 0)
                        {
                            // Intentar abrir con acceso exclusivo (sin compartir) para confirmar que se terminó de escribir
                            using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                            {
                                return true;
                            }
                        }
                    }
                }
                catch (IOException)
                {
                    // Sigue bloqueado por el escáner (se está escribiendo)
                }
                catch { }

                System.Threading.Thread.Sleep(1000);
            }
            return false;
        }

        private static int ContarPaginasConRegex(string rutaCompleta)
        {
            try
            {
                using (var fs = new FileStream(rutaCompleta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, System.Text.Encoding.ASCII))
                {
                    string contenido = sr.ReadToEnd();
                    var matches = System.Text.RegularExpressions.Regex.Matches(
                        contenido, 
                        @"/Type\s*/Pages[\s\S]*?/Count\s*(\d+)|/Count\s*(\d+)[\s\S]*?/Type\s*/Pages", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    );

                    int maxPaginas = 0;
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        string val = !string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[2].Value;
                        if (int.TryParse(val, out int paginas) && paginas > maxPaginas)
                        {
                            maxPaginas = paginas;
                        }
                    }

                    if (maxPaginas > 0) return maxPaginas;
                }
            }
            catch { }
            return 0;
        }

        public static int ObtenerPaginasPdf(string rutaCompleta)
        {
            if (string.IsNullOrEmpty(rutaCompleta))
                return 1;

            // Esperar a que el archivo deje de estar bloqueado por el escáner
            if (!EsperarArchivoListo(rutaCompleta, 300))
            {
                // Si pasaron 5 minutos y no se liberó, procedemos con lo que haya
                if (!File.Exists(rutaCompleta)) return 1;
            }

            // 1. Intentar con método rápido Regex
            int paginasRegex = ContarPaginasConRegex(rutaCompleta);
            if (paginasRegex > 0)
            {
                return paginasRegex;
            }

            // 2. Fallback a PDFsharp en modo ReadOnly (más rápido que Import)
            try
            {
                using (var fs = new FileStream(rutaCompleta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var document = PdfSharp.Pdf.IO.PdfReader.Open(fs, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
                    {
                        return document.PageCount;
                    }
                }
            }
            catch
            {
                // Último fallback
                return 1;
            }
        }

        public static string RegistrarAccion(string notaria, string archivo, string rutaCompleta, string detalles = "")
        {
            try
            {
                // Si no hay un usuario activo logueado en la aplicación, ignoramos el registro
                if (string.IsNullOrEmpty(ModuloConfiguracion.UsuarioActual))
                {
                    return "NO_USER";
                }

                string pcNombre = string.IsNullOrEmpty(ModuloConfiguracion.NombrePC) ? Environment.MachineName : ModuloConfiguracion.NombrePC;

                // Si el nombre del archivo empieza con "PC" seguido de dígitos (ej: PC02...),
                // validamos que coincida con el número de esta PC para evitar registrar capturas de otros equipos en red.
                string archivoUpper = archivo.ToUpper();
                var coincidenciaPc = System.Text.RegularExpressions.Regex.Match(archivoUpper, @"^PC(\d+)");
                if (coincidenciaPc.Success)
                {
                    string prefijoArchivo = coincidenciaPc.Value; // Ej: "PC02"
                    string pcNombreNorm = pcNombre.Replace("-", "").Replace(" ", "").ToUpper(); // Ej: "PC-02" -> "PC02"
                    if (pcNombreNorm != prefijoArchivo && !pcNombreNorm.Contains(prefijoArchivo) && !prefijoArchivo.Contains(pcNombreNorm))
                    {
                        // No pertenece a este equipo, ignoramos el evento de red silenciosamente
                        return "PC_MISMATCH";
                    }
                }

                string localIp = "";
                try
                {
                    var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                    foreach (var ip in host.AddressList)
                    {
                        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            localIp = ip.ToString();
                            break;
                        }
                    }
                }
                catch {}

                // Contar páginas del PDF de forma segura
                int paginas = ObtenerPaginasPdf(rutaCompleta);

                lock (_lockJson)
                {
                    // Carpeta local para guardar las auditorías de captura (siempre AppData local)
                    string rutaJson = ObtenerRutaJsonLocal();
                    ArchivoAuditoriaJson auditoria;

                    if (File.Exists(rutaJson))
                    {
                        try
                        {
                            // Leemos con FileShare.ReadWrite para evitar colisiones
                            string json = "";
                            using (FileStream fs = new FileStream(rutaJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (StreamReader sr = new StreamReader(fs))
                            {
                                json = sr.ReadToEnd();
                            }

                            auditoria = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json) ?? new ArchivoAuditoriaJson();
                        }
                        catch
                        {
                            // Si está corrupto, empezamos uno nuevo para no detener la captura
                            auditoria = new ArchivoAuditoriaJson();
                        }
                    }
                    else
                    {
                        auditoria = new ArchivoAuditoriaJson();
                    }

                    // Evitar registrar duplicados del mismo archivo en el mismo día para esta PC
                    string hoy = DateTime.Now.ToString("yyyy-MM-dd");
                    bool yaExiste = auditoria.Registros.Any(r => 
                        string.Equals(r.ArchivoOriginal, archivo, StringComparison.OrdinalIgnoreCase) && 
                        string.Equals(r.PC, pcNombre, StringComparison.OrdinalIgnoreCase) && 
                        r.Accion == "Capturado" &&
                        r.FechaHora != null && r.FechaHora.StartsWith(hoy));

                    if (yaExiste)
                    {
                        return "DUPLICATE";
                    }

                    // Inyectamos el nuevo registro
                    var nuevoRegistro = new RegistroAuditoria
                    {
                        FechaHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Usuario = ModuloConfiguracion.UsuarioActual,
                        NombreCompleto = ModuloConfiguracion.NombreCompletoActual,
                        Turno = ModuloConfiguracion.TurnoActual,
                        PC = pcNombre,
                        IP = localIp,
                        Notaria = notaria,
                        Accion = "Capturado",
                        ArchivoOriginal = archivo,
                        Detalles = detalles,
                        Paginas = paginas,
                        LugarTrabajo = ModuloConfiguracion.LugarTrabajo,
                        Enviado = false,
                        RutaLocal = rutaCompleta
                    };
                    auditoria.Registros.Add(nuevoRegistro);

                    string jsonFinal = JsonConvert.SerializeObject(auditoria, Formatting.Indented);
                    
                    // Intentamos guardar con reintentos silenciosos
                    bool guardado = false;
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            File.WriteAllText(rutaJson, jsonFinal);
                            guardado = true;
                            break;
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(500);
                        }
                    }

                    // Replicar al servidor de red (mejor esfuerzo, sin afectar resultado)
                    if (guardado)
                    {
                        try { ReplicarRegistroAlServidor(nuevoRegistro); } catch { }
                    }

                    return guardado ? "OK" : "ERROR";
                }
            }
            catch
            {
                return "ERROR";
            }
        }

        /// <summary>
        /// Recorre los registros del día del usuario actual, vuelve a contar las páginas
        /// de cada PDF desde el archivo físico y actualiza el JSON local y del servidor.
        /// Retorna (registrosActualizados, totalPaginasNuevo).
        /// </summary>
        public static (int actualizados, int totalPaginas) RecontarPaginasDelDia()
        {
            int actualizados = 0;
            int totalPaginas = 0;

            try
            {
                string pcNombre = string.IsNullOrEmpty(ModuloConfiguracion.NombrePC) ? Environment.MachineName : ModuloConfiguracion.NombrePC;
                string hoy = DateTime.Now.ToString("yyyy-MM-dd");

                lock (_lockJson)
                {
                    string rutaJson = ObtenerRutaJsonLocal();
                    if (!File.Exists(rutaJson)) return (0, 0);

                    ArchivoAuditoriaJson auditoria;
                    try
                    {
                        string json = "";
                        using (FileStream fs = new FileStream(rutaJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (StreamReader sr = new StreamReader(fs))
                        {
                            json = sr.ReadToEnd();
                        }
                        auditoria = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json) ?? new ArchivoAuditoriaJson();
                    }
                    catch
                    {
                        return (0, 0);
                    }

                    // Filtrar registros del día, del usuario actual y de esta PC
                    var registrosHoy = auditoria.Registros.Where(r =>
                        r.FechaHora != null && r.FechaHora.StartsWith(hoy) &&
                        string.Equals(r.PC, pcNombre, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(r.Usuario, ModuloConfiguracion.UsuarioActual, StringComparison.OrdinalIgnoreCase) &&
                        r.Accion == "Capturado").ToList();

                    foreach (var reg in registrosHoy)
                    {
                        string rutaArchivo = reg.RutaLocal ?? "";

                        if (!string.IsNullOrEmpty(rutaArchivo) && File.Exists(rutaArchivo))
                        {
                            int paginasNuevas = ObtenerPaginasPdf(rutaArchivo);
                            if (paginasNuevas != reg.Paginas)
                            {
                                reg.Paginas = paginasNuevas;
                                actualizados++;
                            }
                        }
                        totalPaginas += reg.Paginas;
                    }

                    if (actualizados > 0)
                    {
                        // Guardar JSON local actualizado
                        try
                        {
                            string jsonFinal = JsonConvert.SerializeObject(auditoria, Formatting.Indented);
                            File.WriteAllText(rutaJson, jsonFinal);
                        }
                        catch { }

                        // Intentar actualizar también el servidor de red
                        try
                        {
                            string? rutaServidor = ObtenerRutaJsonServidor();
                            if (rutaServidor != null && File.Exists(rutaServidor))
                            {
                                string jsonServ = "";
                                using (FileStream fs = new FileStream(rutaServidor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                using (StreamReader sr = new StreamReader(fs))
                                {
                                    jsonServ = sr.ReadToEnd();
                                }
                                var audServ = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(jsonServ);
                                if (audServ != null)
                                {
                                    foreach (var regLocal in registrosHoy)
                                    {
                                        var regServ = audServ.Registros.FirstOrDefault(r =>
                                            string.Equals(r.ArchivoOriginal, regLocal.ArchivoOriginal, StringComparison.OrdinalIgnoreCase) &&
                                            string.Equals(r.PC, regLocal.PC, StringComparison.OrdinalIgnoreCase) &&
                                            r.FechaHora == regLocal.FechaHora);
                                        if (regServ != null)
                                        {
                                            regServ.Paginas = regLocal.Paginas;
                                        }
                                    }
                                    string jsonServFinal = JsonConvert.SerializeObject(audServ, Formatting.Indented);
                                    File.WriteAllText(rutaServidor, jsonServFinal);
                                }
                            }
                        }
                        catch { }
                    }

                    return (actualizados, totalPaginas);
                }
            }
            catch
            {
                return (actualizados, totalPaginas);
            }
        }

        private static bool ExisteDirectorioConTimeout(string ruta, int timeoutMs = 1500)
        {
            try
            {
                var tarea = System.Threading.Tasks.Task.Run(() => Directory.Exists(ruta));
                if (tarea.Wait(timeoutMs))
                {
                    return tarea.Result;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static void MigrarAuditoriasLocalesAlServidor(string rutaServidor)
        {
            try
            {
                string carpetaLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CapturaNotarias");
                string rutaLocal = Path.Combine(carpetaLocal, "auditoria_local.json");

                if (File.Exists(rutaLocal))
                {
                    string jsonLocal = "";
                    using (FileStream fs = new FileStream(rutaLocal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        jsonLocal = sr.ReadToEnd();
                    }

                    var objLocal = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(jsonLocal);
                    if (objLocal != null && objLocal.Registros != null && objLocal.Registros.Count > 0)
                    {
                        var objServidor = new ArchivoAuditoriaJson();
                        if (File.Exists(rutaServidor))
                        {
                            try
                            {
                                string jsonServidor = "";
                                using (FileStream fs = new FileStream(rutaServidor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                using (StreamReader sr = new StreamReader(fs))
                                {
                                    jsonServidor = sr.ReadToEnd();
                                }
                                var obj = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(jsonServidor);
                                if (obj != null) objServidor = obj;
                            }
                            catch { }
                        }

                        bool huboCambios = false;
                        foreach (var reg in objLocal.Registros)
                        {
                            bool yaExiste = objServidor.Registros.Any(r =>
                                r.FechaHora == reg.FechaHora &&
                                r.Usuario == reg.Usuario &&
                                r.ArchivoOriginal == reg.ArchivoOriginal &&
                                r.Accion == reg.Accion);

                            if (!yaExiste)
                            {
                                objServidor.Registros.Add(reg);
                                huboCambios = true;
                            }
                        }

                        if (huboCambios || !File.Exists(rutaServidor))
                        {
                            string jsonFinal = JsonConvert.SerializeObject(objServidor, Formatting.Indented);
                            using (FileStream fs = new FileStream(rutaServidor, FileMode.Create, FileAccess.Write, FileShare.None))
                            using (StreamWriter sw = new StreamWriter(fs))
                            {
                                sw.Write(jsonFinal);
                            }
                        }
                    }

                    string rutaRespaldo = Path.Combine(carpetaLocal, "auditoria_local_migrada.json");
                    if (File.Exists(rutaRespaldo))
                    {
                        File.Delete(rutaRespaldo);
                    }
                    File.Move(rutaLocal, rutaRespaldo);
                }
            }
            catch { }
        }

        public static string ObtenerRutaJsonLocal()
        {
            // Siempre usar AppData local como fuente de verdad para garantizar
            // que nunca se pierdan registros por desconexión del servidor
            string carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CapturaNotarias");
            if (!Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }
            string rutaLocal = Path.Combine(carpeta, "auditoria_local.json");

            // Migración única: si no existe archivo local pero sí hay datos en el servidor,
            // copiarlos para no perder el historial de las versiones anteriores
            if (!_migracionRealizada)
            {
                _migracionRealizada = true;
                if (!File.Exists(rutaLocal))
                {
                    try
                    {
                        string? rutaServidor = ObtenerRutaJsonServidor();
                        if (rutaServidor != null && File.Exists(rutaServidor))
                        {
                            File.Copy(rutaServidor, rutaLocal);
                        }
                    }
                    catch { }
                }
            }

            return rutaLocal;
        }

        public static string? ObtenerRutaJsonServidor()
        {
            try
            {
                if (!string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria) && !string.IsNullOrEmpty(ModuloConfiguracion.NombrePC))
                {
                    if (ExisteDirectorioConTimeout(ModuloConfiguracion.RutaServidorAuditoria, 1500))
                    {
                        string rutaMonitoreo = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "MonitoreoCaptura", ModuloConfiguracion.NombrePC);
                        if (!Directory.Exists(rutaMonitoreo))
                        {
                            Directory.CreateDirectory(rutaMonitoreo);
                        }
                        string rutaServidor = Path.Combine(rutaMonitoreo, "auditoria.json");

                        // Migración automática de registros antiguos locales a la red
                        MigrarAuditoriasLocalesAlServidor(rutaServidor);

                        return rutaServidor;
                    }
                }
            }
            catch { }
            return null;
        }

        private static void ReplicarRegistroAlServidor(RegistroAuditoria registro)
        {
            try
            {
                string? rutaServidor = ObtenerRutaJsonServidor();
                if (rutaServidor == null) return;

                ArchivoAuditoriaJson auditoriaServidor;
                if (File.Exists(rutaServidor))
                {
                    string json = "";
                    using (FileStream fs = new FileStream(rutaServidor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        json = sr.ReadToEnd();
                    }
                    auditoriaServidor = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json) ?? new ArchivoAuditoriaJson();
                }
                else
                {
                    auditoriaServidor = new ArchivoAuditoriaJson();
                }

                // Verificar que no exista ya en el servidor para evitar duplicados
                string hoy = registro.FechaHora?.Substring(0, 10) ?? DateTime.Now.ToString("yyyy-MM-dd");
                bool yaExiste = auditoriaServidor.Registros.Any(r =>
                    string.Equals(r.ArchivoOriginal, registro.ArchivoOriginal, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.PC, registro.PC, StringComparison.OrdinalIgnoreCase) &&
                    r.Accion == "Capturado" &&
                    r.FechaHora != null && r.FechaHora.StartsWith(hoy));

                if (!yaExiste)
                {
                    auditoriaServidor.Registros.Add(registro);
                    string jsonFinal = JsonConvert.SerializeObject(auditoriaServidor, Formatting.Indented);
                    File.WriteAllText(rutaServidor, jsonFinal);
                }
            }
            catch { } // Mejor esfuerzo, ignorar fallos silenciosamente
        }

        public static int ObtenerContadorDelDia(string usuario)
        {
            try
            {
                string hoy = DateTime.Now.ToString("yyyy-MM-dd");
                string pcNombre = string.IsNullOrEmpty(ModuloConfiguracion.NombrePC) ? Environment.MachineName : ModuloConfiguracion.NombrePC;
                string rutaJson = ObtenerRutaJsonLocal();

                lock (_lockJson)
                {
                    if (File.Exists(rutaJson))
                    {
                        string json = "";
                        using (FileStream fs = new FileStream(rutaJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (StreamReader sr = new StreamReader(fs))
                        {
                            json = sr.ReadToEnd();
                        }
                        var auditoria = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json);
                        if (auditoria != null && auditoria.Registros != null)
                        {
                            return auditoria.Registros.Count(r =>
                                r.FechaHora != null && r.FechaHora.StartsWith(hoy) &&
                                string.Equals(r.PC, pcNombre, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(r.Usuario, usuario, StringComparison.OrdinalIgnoreCase) &&
                                r.Accion == "Capturado");
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        public static List<RegistroAuditoria> ObtenerLogsNoEnviadosTodos()
        {
            var result = new List<RegistroAuditoria>();
            
            // 1. Cargar desde la carpeta del servidor (MonitoreoCaptura)
            try
            {
                if (!string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria))
                {
                    string rutaMonitoreoRaiz = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "MonitoreoCaptura");
                    if (ExisteDirectorioConTimeout(rutaMonitoreoRaiz, 1500))
                    {
                        foreach (string carpetaPC in Directory.GetDirectories(rutaMonitoreoRaiz))
                        {
                            string nombrePC = Path.GetFileName(carpetaPC);
                            if (nombrePC.StartsWith("PC-", StringComparison.OrdinalIgnoreCase) || nombrePC.StartsWith("PC", StringComparison.OrdinalIgnoreCase))
                            {
                                string rutaJson = Path.Combine(carpetaPC, "auditoria.json");
                                if (File.Exists(rutaJson))
                                {
                                    try
                                    {
                                        string json = "";
                                        using (FileStream fs = new FileStream(rutaJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                        using (StreamReader sr = new StreamReader(fs))
                                        {
                                            json = sr.ReadToEnd();
                                        }
                                        var obj = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json);
                                        if (obj != null && obj.Registros != null)
                                        {
                                            foreach (var reg in obj.Registros.Where(r => r.Enviado != true))
                                            {
                                                reg.RutaJsonOrigen = rutaJson;
                                                result.Add(reg);
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. Cargar desde el archivo local y fusionar
            try
            {
                string rutaJsonLocal = ObtenerRutaJsonLocal();
                lock (_lockJson)
                {
                    if (File.Exists(rutaJsonLocal))
                    {
                        try
                        {
                            string jsonLocal = "";
                            using (FileStream fs = new FileStream(rutaJsonLocal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (StreamReader sr = new StreamReader(fs))
                            {
                                jsonLocal = sr.ReadToEnd();
                            }
                            var objLocal = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(jsonLocal);
                            if (objLocal != null && objLocal.Registros != null)
                            {
                                foreach (var regLocal in objLocal.Registros.Where(r => r.Enviado != true))
                                {
                                    // Evitar duplicados (por archivo original, PC y hora de registro)
                                    bool yaExiste = result.Any(r =>
                                        string.Equals(r.ArchivoOriginal, regLocal.ArchivoOriginal, StringComparison.OrdinalIgnoreCase) &&
                                        string.Equals(r.PC, regLocal.PC, StringComparison.OrdinalIgnoreCase) &&
                                        r.FechaHora == regLocal.FechaHora);

                                    if (!yaExiste)
                                    {
                                        regLocal.RutaJsonOrigen = rutaJsonLocal;
                                        result.Add(regLocal);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return result.OrderBy(r => r.FechaHora).ToList();
        }

        public static List<RegistroAuditoria> ObtenerRegistrosTodos()
        {
            var todos = new List<RegistroAuditoria>();
            try
            {
                bool cargadoDesdeServidor = false;

                // Si la ruta del servidor está configurada, intentamos cargar los registros de todas las PCs
                if (!string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria))
                {
                    string rutaMonitoreoRaiz = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "MonitoreoCaptura");
                    if (ExisteDirectorioConTimeout(rutaMonitoreoRaiz, 1500))
                    {
                        foreach (string carpetaPC in Directory.GetDirectories(rutaMonitoreoRaiz))
                        {
                            string nombrePC = Path.GetFileName(carpetaPC);
                            if (nombrePC.StartsWith("PC-", StringComparison.OrdinalIgnoreCase))
                            {
                                string rutaJson = Path.Combine(carpetaPC, "auditoria.json");
                                if (File.Exists(rutaJson))
                                {
                                    try
                                    {
                                        string json = "";
                                        using (FileStream fs = new FileStream(rutaJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                        using (StreamReader sr = new StreamReader(fs))
                                        {
                                            json = sr.ReadToEnd();
                                        }
                                        var obj = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json);
                                        if (obj != null && obj.Registros != null)
                                        {
                                            todos.AddRange(obj.Registros);
                                            cargadoDesdeServidor = true;
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }

                // Siempre cargar los registros locales y fusionar para cubrir registros
                // que no se hayan podido replicar al servidor por desconexión
                string rutaJsonLocal = ObtenerRutaJsonLocal();
                lock (_lockJson)
                {
                    if (File.Exists(rutaJsonLocal))
                    {
                        try
                        {
                            string jsonLocal = "";
                            using (FileStream fs = new FileStream(rutaJsonLocal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (StreamReader sr = new StreamReader(fs))
                            {
                                jsonLocal = sr.ReadToEnd();
                            }
                            var objLocal = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(jsonLocal);
                            if (objLocal != null && objLocal.Registros != null)
                            {
                                if (!cargadoDesdeServidor)
                                {
                                    // Sin servidor, los registros locales son todo lo que tenemos
                                    todos.AddRange(objLocal.Registros);
                                }
                                else
                                {
                                    // Con servidor, fusionar solo registros locales que no estén ya en el servidor
                                    // (identificados por ArchivoOriginal + PC + FechaHora exacta)
                                    foreach (var regLocal in objLocal.Registros)
                                    {
                                        bool yaExisteEnServidor = todos.Any(r =>
                                            string.Equals(r.ArchivoOriginal, regLocal.ArchivoOriginal, StringComparison.OrdinalIgnoreCase) &&
                                            string.Equals(r.PC, regLocal.PC, StringComparison.OrdinalIgnoreCase) &&
                                            r.FechaHora == regLocal.FechaHora);

                                        if (!yaExisteEnServidor)
                                        {
                                            todos.Add(regLocal);
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // Ordenar por fecha de forma descendente (el más nuevo arriba)
            return todos.OrderBy(r => r.FechaHora).ToList();
        }

        public static void ExportarExcel(List<RegistroAuditoria> datos)
        {
            if (datos == null || datos.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Filtrar registros inválidos (sin usuario) para evitar filas vacías en el reporte Excel
            datos = datos.Where(r => !string.IsNullOrEmpty(r.Usuario) || !string.IsNullOrEmpty(r.NombreCompleto)).ToList();

            // 1. Agrupar todos los datos por Fecha (YYYY-MM-DD)
            var registrosPorFecha = new Dictionary<string, List<RegistroAuditoria>>();
            foreach (var reg in datos)
            {
                string fecha = "Sin Fecha";
                if (!string.IsNullOrEmpty(reg.FechaHora) && reg.FechaHora.Length >= 10)
                {
                    fecha = reg.FechaHora.Substring(0, 10);
                }
                if (!registrosPorFecha.ContainsKey(fecha))
                {
                    registrosPorFecha[fecha] = new List<RegistroAuditoria>();
                }
                registrosPorFecha[fecha].Add(reg);
            }

            // 2. Para cada fecha, deduplicar los registros de forma global por archivo original
            // para que no se dupliquen entre diferentes PCs o usuarios que vigilan la misma carpeta.
            var registrosDeduplicados = new List<RegistroAuditoria>();
            foreach (var fecha in registrosPorFecha.Keys)
            {
                var grupoArchivos = registrosPorFecha[fecha]
                    .GroupBy(r => r.ArchivoOriginal ?? "Desconocido", StringComparer.OrdinalIgnoreCase);

                foreach (var grupo in grupoArchivos)
                {
                    if (grupo.Count() == 1)
                    {
                        registrosDeduplicados.Add(grupo.First());
                    }
                    else
                    {
                        // Seleccionar el registro óptimo basándonos en el prefijo del archivo o la hora de registro
                        string nombreArchivo = grupo.Key;
                        var coincidenciaPc = System.Text.RegularExpressions.Regex.Match(nombreArchivo, @"^PC(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        
                        RegistroAuditoria? seleccionado = null;
                        if (coincidenciaPc.Success)
                        {
                            string prefijoPC = coincidenciaPc.Value.ToUpper(); // Ej: "PC12" o "PC04"
                            seleccionado = grupo.FirstOrDefault(r => {
                                if (string.IsNullOrEmpty(r.PC)) return false;
                                string pcNormalizada = r.PC.Replace("-", "").Replace(" ", "").ToUpper(); // Ej: "PC-04" -> "PC04"
                                return pcNormalizada == prefijoPC || pcNormalizada.Contains(prefijoPC) || prefijoPC.Contains(pcNormalizada);
                            });
                        }

                        // Si no hay coincidencia de prefijo con ninguna PC del grupo, o el archivo no tiene formato PCXX (ej. 0001.pdf),
                        // nos quedamos con el registro que ocurrió primero (la hora más temprana).
                        if (seleccionado == null)
                        {
                            seleccionado = grupo
                                .OrderBy(r => string.IsNullOrEmpty(r.FechaHora) ? "9999-12-31" : r.FechaHora)
                                .First();
                        }

                        // Conservar el mayor número de páginas si en otros registros se leyó de forma completa
                        int maximoPaginas = grupo.Max(r => r.Paginas);
                        if (maximoPaginas > seleccionado.Paginas)
                        {
                            seleccionado.Paginas = maximoPaginas;
                        }

                        registrosDeduplicados.Add(seleccionado);
                    }
                }
            }

            // 3. Agrupar los registros deduplicados por Fecha -> PC -> IP -> Usuario -> Turno para su procesamiento
            var registrosAgrupados = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, List<RegistroAuditoria>>>>>>();

            foreach (var reg in registrosDeduplicados)
            {
                string fecha = "Sin Fecha";
                if (!string.IsNullOrEmpty(reg.FechaHora) && reg.FechaHora.Length >= 10)
                {
                    fecha = reg.FechaHora.Substring(0, 10);
                }

                if (!registrosAgrupados.ContainsKey(fecha))
                {
                    registrosAgrupados[fecha] = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, List<RegistroAuditoria>>>>>();
                }

                string pc = string.IsNullOrEmpty(reg.PC) ? "Desconocido" : reg.PC;
                if (!registrosAgrupados[fecha].ContainsKey(pc))
                {
                    registrosAgrupados[fecha][pc] = new Dictionary<string, Dictionary<string, Dictionary<string, List<RegistroAuditoria>>>>();
                }

                string ip = string.IsNullOrEmpty(reg.IP) ? "Desconocido" : reg.IP;
                if (!registrosAgrupados[fecha][pc].ContainsKey(ip))
                {
                    registrosAgrupados[fecha][pc][ip] = new Dictionary<string, Dictionary<string, List<RegistroAuditoria>>>();
                }

                string usuario = string.IsNullOrEmpty(reg.NombreCompleto) ? (string.IsNullOrEmpty(reg.Usuario) ? "Desconocido" : reg.Usuario) : reg.NombreCompleto;
                if (!registrosAgrupados[fecha][pc][ip].ContainsKey(usuario))
                {
                    registrosAgrupados[fecha][pc][ip][usuario] = new Dictionary<string, List<RegistroAuditoria>>();
                }

                string turno = string.IsNullOrEmpty(reg.Turno) ? "Matutino" : reg.Turno;
                if (!registrosAgrupados[fecha][pc][ip][usuario].ContainsKey(turno))
                {
                    registrosAgrupados[fecha][pc][ip][usuario][turno] = new List<RegistroAuditoria>();
                }

                registrosAgrupados[fecha][pc][ip][usuario][turno].Add(reg);
            }

            // 4. Crear el diccionario final con los totales ya procesados y deduplicados
            var datosPorFecha = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, (int pdfs, int paginas, string lugar)>>>>>();

            foreach (var fecha in registrosAgrupados.Keys)
            {
                datosPorFecha[fecha] = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, (int pdfs, int paginas, string lugar)>>>>();
                foreach (var pc in registrosAgrupados[fecha].Keys)
                {
                    datosPorFecha[fecha][pc] = new Dictionary<string, Dictionary<string, Dictionary<string, (int pdfs, int paginas, string lugar)>>>();
                    foreach (var ip in registrosAgrupados[fecha][pc].Keys)
                    {
                        datosPorFecha[fecha][pc][ip] = new Dictionary<string, Dictionary<string, (int pdfs, int paginas, string lugar)>>();
                        foreach (var usuario in registrosAgrupados[fecha][pc][ip].Keys)
                        {
                            datosPorFecha[fecha][pc][ip][usuario] = new Dictionary<string, (int pdfs, int paginas, string lugar)>();
                            foreach (var turno in registrosAgrupados[fecha][pc][ip][usuario].Keys)
                            {
                                var listaRegs = registrosAgrupados[fecha][pc][ip][usuario][turno];
                                
                                int totalPdfs = listaRegs.Count;
                                int totalPaginas = listaRegs.Sum(r => r.Paginas > 0 ? r.Paginas : 1);
                                string lugar = listaRegs.FirstOrDefault(r => !string.IsNullOrEmpty(r.LugarTrabajo))?.LugarTrabajo ?? "";

                                datosPorFecha[fecha][pc][ip][usuario][turno] = (totalPdfs, totalPaginas, lugar);
                            }
                        }
                    }
                }
            }

            try
            {
                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    // Crear una hoja por cada fecha (ordenadas descendente)
                    foreach (var fecha in datosPorFecha.Keys.OrderByDescending(k => k))
                    {
                        string sheetName = fecha.Replace("-", "_");
                        // ClosedXML worksheets names cannot exceed 31 chars
                        if (sheetName.Length > 31) sheetName = sheetName.Substring(0, 31);
                        
                        var worksheet = workbook.Worksheets.Add(sheetName);

                        // Cabeceras
                        worksheet.Cell(1, 1).Value = "PC";
                        worksheet.Cell(1, 2).Value = "Lugar de Trabajo";
                        worksheet.Cell(1, 3).Value = "IP";
                        worksheet.Cell(1, 4).Value = "Usuario";
                        worksheet.Cell(1, 5).Value = "Turno";
                        worksheet.Cell(1, 6).Value = "Capturas (PDFs)";
                        worksheet.Cell(1, 7).Value = "Total de Imágenes";

                        var listaFilas = new List<(string PC, string Lugar, string IP, string Usuario, string Turno, int Pdfs, int Paginas)>();
                        var pcsDeLaFecha = datosPorFecha[fecha];
                        foreach (var pc in pcsDeLaFecha.Keys)
                        {
                            var ipsDeLaPc = pcsDeLaFecha[pc];
                            foreach (var ip in ipsDeLaPc.Keys)
                            {
                                var usuariosDeLaIp = ipsDeLaPc[ip];
                                foreach (var usuario in usuariosDeLaIp.Keys)
                                {
                                    var turnosDelUsuario = usuariosDeLaIp[usuario];
                                    foreach (var turno in turnosDelUsuario.Keys)
                                    {
                                        var stats = turnosDelUsuario[turno];
                                        listaFilas.Add((pc, stats.lugar, ip, usuario, turno, stats.pdfs, stats.paginas));
                                    }
                                }
                            }
                        }

                        // Ordenar por turno: Matutino -> Vespertino -> Nocturno -> Otros
                        var filasOrdenadas = listaFilas.OrderBy(r => {
                            if (r.Turno.Equals("Matutino", StringComparison.OrdinalIgnoreCase)) return 1;
                            if (r.Turno.Equals("Vespertino", StringComparison.OrdinalIgnoreCase)) return 2;
                            if (r.Turno.Equals("Nocturno", StringComparison.OrdinalIgnoreCase)) return 3;
                            return 4;
                        }).ToList();

                        int rowIdx = 2;
                        foreach (var fila in filasOrdenadas)
                        {
                            worksheet.Cell(rowIdx, 1).Value = fila.PC;
                            worksheet.Cell(rowIdx, 2).Value = fila.Lugar;
                            worksheet.Cell(rowIdx, 3).Value = fila.IP;
                            worksheet.Cell(rowIdx, 4).Value = fila.Usuario;
                            worksheet.Cell(rowIdx, 5).Value = fila.Turno;
                            worksheet.Cell(rowIdx, 6).Value = fila.Pdfs;
                            worksheet.Cell(rowIdx, 7).Value = fila.Paginas;

                            // Pintamos la fila según el turno con tonos pastel muy elegantes
                            string colorHtml = "#F2F2F2"; // Gris para cualquier otro
                            if (fila.Turno.Equals("Matutino", StringComparison.OrdinalIgnoreCase))
                            {
                                colorHtml = "#FFF2CC"; // Amarillo/Naranja pastel suave
                            }
                            else if (fila.Turno.Equals("Vespertino", StringComparison.OrdinalIgnoreCase))
                            {
                                colorHtml = "#E2EFDA"; // Verde claro pastel suave
                            }
                            else if (fila.Turno.Equals("Nocturno", StringComparison.OrdinalIgnoreCase))
                            {
                                colorHtml = "#DDEBF7"; // Azul pastel suave
                            }

                            var rangoFila = worksheet.Range(rowIdx, 1, rowIdx, 7);
                            rangoFila.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml(colorHtml);
                            rangoFila.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                            rangoFila.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                            rangoFila.Style.Border.OutsideBorderColor = ClosedXML.Excel.XLColor.FromHtml("#D9D9D9");
                            rangoFila.Style.Border.InsideBorderColor = ClosedXML.Excel.XLColor.FromHtml("#D9D9D9");

                            rowIdx++;
                        }

                        // Estilo Cabeceras
                        var rangoHeader = worksheet.Range(1, 1, 1, 7);
                        rangoHeader.Style.Font.Bold = true;
                        rangoHeader.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#4F81BD");
                        rangoHeader.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                        worksheet.Columns().AdjustToContents();
                    }

                    // Guardar
                    using (var sfd = new SaveFileDialog())
                    {
                        sfd.Filter = "Libro de Excel (*.xlsx)|*.xlsx";
                        sfd.FileName = "Reporte_Diario_Captura_" + DateTime.Now.ToString("yyyyMMdd_HHmm");

                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            workbook.SaveAs(sfd.FileName);
                            MessageBox.Show("Reporte generado con éxito.", "Resultado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                            }
                            catch {}
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar Excel: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool EnviarLogsAlServidorCentralHttp(List<RegistroAuditoria> registros)
        {
            try
            {
                return System.Threading.Tasks.Task.Run(async () =>
                {
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(15);
                        var payload = new ArchivoAuditoriaJson { Registros = registros };
                        string json = JsonConvert.SerializeObject(payload);
                        var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(ModuloConfiguracion.UrlApi, content).ConfigureAwait(false);
                        return response.IsSuccessStatusCode;
                    }
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al enviar logs vía HTTP: " + ex.Message);
                return false;
            }
        }

        private static string ResolverRutaLocalEnServidor(string rutaOriginal)
        {
            if (string.IsNullOrEmpty(rutaOriginal)) return rutaOriginal;
            
            // Si el archivo ya existe tal cual, lo usamos
            if (File.Exists(rutaOriginal)) return rutaOriginal;
            
            // Si no existe, probamos reemplazando la unidad de red por la ruta del servidor local
            if (!string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria))
            {
                int colonIndex = rutaOriginal.IndexOf(':');
                if (colonIndex > 0)
                {
                    string rutaSinUnidad = rutaOriginal.Substring(colonIndex + 1).TrimStart('\\', '/');
                    
                    // Intento 1: Directo combinando con RutaServidorAuditoria
                    string rutaIntento1 = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, rutaSinUnidad);
                    if (File.Exists(rutaIntento1)) return rutaIntento1;
                    
                    // Intento 2: Si no contenía "REGISTROS" en RutaServidorAuditoria pero sí en el resto, o al revés
                    if (!ModuloConfiguracion.RutaServidorAuditoria.EndsWith("REGISTROS", StringComparison.OrdinalIgnoreCase) &&
                        !rutaSinUnidad.StartsWith("REGISTROS", StringComparison.OrdinalIgnoreCase))
                    {
                        string rutaIntento2 = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "REGISTROS", rutaSinUnidad);
                        if (File.Exists(rutaIntento2)) return rutaIntento2;
                    }
                    
                    // Intento 3: Si rutaSinUnidad empieza con REGISTROS, pero RutaServidorAuditoria ya termina en REGISTROS
                    if (ModuloConfiguracion.RutaServidorAuditoria.EndsWith("REGISTROS", StringComparison.OrdinalIgnoreCase) &&
                        rutaSinUnidad.StartsWith("REGISTROS", StringComparison.OrdinalIgnoreCase))
                    {
                        string sinRegistrosRepetido = rutaSinUnidad.Substring("REGISTROS".Length).TrimStart('\\', '/');
                        string rutaIntento3 = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, sinRegistrosRepetido);
                        if (File.Exists(rutaIntento3)) return rutaIntento3;
                    }
                }
            }
            
            return rutaOriginal;
        }

        private static string ObtenerRutaRelativaDestino(string rutaLocal, string rutaOriginalLog)
        {
            // Preferimos usar rutaOriginalLog porque contiene la estructura desde el cliente (ej: Y:\REGISTROS\...)
            string rutaDeTrabajo = !string.IsNullOrEmpty(rutaOriginalLog) ? rutaOriginalLog : rutaLocal;
            if (string.IsNullOrEmpty(rutaDeTrabajo)) return "";

            string rutaLimpia = rutaDeTrabajo;

            // 1. Si empieza con una letra de unidad (ej: Y:\ o C:\), quitamos la raíz
            string root = Path.GetPathRoot(rutaDeTrabajo) ?? "";
            if (!string.IsNullOrEmpty(root))
            {
                rutaLimpia = rutaDeTrabajo.Substring(root.Length);
            }

            // 2. Si la ruta del servidor de auditoría (ej: C:\NOTARIAS) está configurada,
            // y la ruta limpia comienza con la parte final de esa ruta (ej: "NOTARIAS\"), la removemos.
            if (!string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria))
            {
                string nombreCarpetaServidor = Path.GetFileName(ModuloConfiguracion.RutaServidorAuditoria.TrimEnd('\\', '/'));
                if (!string.IsNullOrEmpty(nombreCarpetaServidor))
                {
                    string prefijoAEliminar = nombreCarpetaServidor + "\\";
                    if (rutaLimpia.StartsWith(prefijoAEliminar, StringComparison.OrdinalIgnoreCase))
                    {
                        rutaLimpia = rutaLimpia.Substring(prefijoAEliminar.Length);
                    }
                }
            }

            // 3. Quitar el nombre del archivo al final para quedarnos solo con la estructura de carpetas
            string? nombreArchivo = Path.GetFileName(rutaDeTrabajo);
            if (!string.IsNullOrEmpty(nombreArchivo) && rutaLimpia.EndsWith(nombreArchivo, StringComparison.OrdinalIgnoreCase))
            {
                rutaLimpia = rutaLimpia.Substring(0, rutaLimpia.Length - nombreArchivo.Length);
            }

            return rutaLimpia.Trim('\\', '/');
        }

        public static void EnviarAuditoriasAlServidorCentral(bool silencioso = false)
        {
            // Si es en segundo plano (silencioso), respetamos la opción de desactivado
            if (silencioso && !ModuloConfiguracion.ActivarEnvioAuditoria)
            {
                return;
            }

            try
            {
                string rutaJsonLocal = ObtenerRutaJsonLocal();

                // Cargar registros de ambas rutas (Local AppData y Servidor) para consolidar todo lo pendiente de enviar
                var registrosPendientes = new List<RegistroAuditoria>();
                var rutasLeidas = new List<string>();

                // 1. Ruta AppData Local
                string carpetaLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CapturaNotarias");
                string rutaLocalJson = Path.Combine(carpetaLocal, "auditoria_local.json");
                if (File.Exists(rutaLocalJson))
                {
                    rutasLeidas.Add(rutaLocalJson);
                }

                // 2. Ruta Servidor (si está configurada y accesible)
                if (!string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria))
                {
                    if (ExisteDirectorioConTimeout(ModuloConfiguracion.RutaServidorAuditoria, 1500))
                    {
                        string rutaMonitoreoRaiz = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "MonitoreoCaptura");
                        if (Directory.Exists(rutaMonitoreoRaiz))
                        {
                            foreach (string carpetaPC in Directory.GetDirectories(rutaMonitoreoRaiz))
                            {
                                string nombrePC = Path.GetFileName(carpetaPC);
                                if (nombrePC.StartsWith("PC-", StringComparison.OrdinalIgnoreCase) || nombrePC.Equals(ModuloConfiguracion.NombrePC, StringComparison.OrdinalIgnoreCase))
                                {
                                    string rutaJson = Path.Combine(carpetaPC, "auditoria.json");
                                    if (File.Exists(rutaJson))
                                    {
                                        rutasLeidas.Add(rutaJson);
                                    }
                                }
                            }
                        }
                    }
                }

                if (rutasLeidas.Count == 0)
                {
                    if (!silencioso)
                        MessageBox.Show("No se encontraron archivos de auditoría para sincronizar.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Leer registros de todos los archivos encontrados
                var registrosDeArchivos = new Dictionary<string, ArchivoAuditoriaJson>();
                foreach (var ruta in rutasLeidas)
                {
                    try
                    {
                        string json = "";
                        using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (StreamReader sr = new StreamReader(fs))
                        {
                            json = sr.ReadToEnd();
                        }
                        var obj = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json);
                        if (obj != null && obj.Registros != null)
                        {
                            registrosDeArchivos[ruta] = obj;
                            foreach (var reg in obj.Registros)
                            {
                                // Asignar la ruta de origen para poder actualizar el estado de "Enviado" después
                                reg.RutaJsonOrigen = ruta;

                                // Evitar duplicados en la lista de envío
                                bool yaAgregado = registrosPendientes.Any(r =>
                                    r.FechaHora == reg.FechaHora &&
                                    r.Usuario == reg.Usuario &&
                                    r.ArchivoOriginal == reg.ArchivoOriginal &&
                                    r.Accion == reg.Accion);

                                if (!yaAgregado && reg.Enviado != true)
                                {
                                    registrosPendientes.Add(reg);
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (registrosPendientes.Count == 0)
                {
                    if (!silencioso)
                        MessageBox.Show("No hay registros pendientes de sincronizar.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Sincronización Selectiva si no es silencioso
                if (!silencioso)
                {
                    using (var frm = new FormSyncParcial(registrosPendientes))
                    {
                        if (frm.ShowDialog() != DialogResult.OK)
                        {
                            return;
                        }
                        registrosPendientes = frm.RegistrosSeleccionados;
                    }
                }

                // Cargar config para fallback de ruta vigilada
                var config = ModuloConfiguracion.CargarConfiguracion();
                string ultimaRuta = config?.UltimaRutaVigilada ?? "";

                Form? formularioProgreso = null;
                Label? etiquetaProgreso = null;
                ProgressBar? barraArchivos = null;
                ProgressBar? barraAuditoria = null;

                if (!silencioso)
                {
                    formularioProgreso = new Form()
                    {
                        ClientSize = new Size(420, 100),
                        Text = "Sincronización de Archivos y Auditoría",
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        StartPosition = FormStartPosition.CenterScreen,
                        ControlBox = false
                    };
                    etiquetaProgreso = new Label() { Left = 30, Top = 20, Width = 360, Height = 25, Text = "Iniciando transferencia..." };
                    
                    barraArchivos = new ProgressBar() 
                    { 
                        Left = 30, 
                        Top = 50, 
                        Width = 360, 
                        Height = 23, 
                        Style = ProgressBarStyle.Continuous, 
                        Minimum = 0, 
                        Maximum = registrosPendientes.Count, 
                        Value = 0,
                        Visible = true 
                    };
                    
                    barraAuditoria = new ProgressBar() 
                    { 
                        Left = 30, 
                        Top = 50, 
                        Width = 360, 
                        Height = 23, 
                        Style = ProgressBarStyle.Marquee, 
                        Visible = false 
                    };

                    formularioProgreso.Controls.Add(etiquetaProgreso);
                    formularioProgreso.Controls.Add(barraArchivos);
                    formularioProgreso.Controls.Add(barraAuditoria);
                    formularioProgreso.Show();
                    formularioProgreso.Refresh();
                }

                // 2. Fase de copia de archivos y envío HTTP (Fuera de lock para no colgar ni bloquear capturas)
                var logsAEnviar = new System.Collections.Generic.List<RegistroAuditoria>();
                bool huboErroresDeCopia = false;
                
                int indice = 0;
                foreach (var log in registrosPendientes)
                {
                    indice++;
                    if (!silencioso && etiquetaProgreso != null && formularioProgreso != null && barraArchivos != null)
                    {
                        etiquetaProgreso.Text = string.Format("Copiando archivo {0} de {1}...", indice, registrosPendientes.Count);
                        barraArchivos.Value = indice;
                        formularioProgreso.Refresh();
                        Application.DoEvents();
                    }

                    string rutaLocal = log.RutaLocal ?? "";
                    if (string.IsNullOrEmpty(rutaLocal))
                    {
                        if (log.Detalles != null && log.Detalles.StartsWith("PDF Escaneado en ", StringComparison.OrdinalIgnoreCase))
                        {
                            rutaLocal = log.Detalles.Substring("PDF Escaneado en ".Length);
                        }
                    }

                    if (string.IsNullOrEmpty(rutaLocal) && !string.IsNullOrEmpty(ultimaRuta))
                    {
                        rutaLocal = Path.Combine(ultimaRuta, log.ArchivoOriginal ?? "");
                    }

                    // El servidor de destino central real (VPN) para guardar los PDFs
                    string baseServidor = @"\\172.40.5.84\ssdirec\NOTARIAS";

                    // El servidor local del escáner (de donde obtenemos los archivos originales)
                    string rutaAuditoriaLocal = !string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria)
                        ? ModuloConfiguracion.RutaServidorAuditoria
                        : @"\\192.168.1.10\NOTARIAS";

                    // Construir el destino final limpio conservando la estructura de carpetas (ej. NOTARIA 53\VOLUMEN 24)
                    string destinoArchivo = "";
                    if (!string.IsNullOrEmpty(rutaLocal) && rutaLocal.Length >= 3 && rutaLocal[1] == ':' && rutaLocal[2] == '\\')
                    {
                        string rutaSinUnidad = rutaLocal.Substring(3);
                        string folderBase = Path.GetFileName(baseServidor);
                        string[] segmentos = rutaSinUnidad.Split('\\');
                        string primerSegmento = segmentos.Length > 0 ? segmentos[0] : "";

                        if (string.Equals(folderBase, primerSegmento, StringComparison.OrdinalIgnoreCase))
                        {
                            string subrutaLimpia = string.Join("\\", segmentos, 1, segmentos.Length - 1);
                            destinoArchivo = Path.Combine(baseServidor, subrutaLimpia);
                        }
                        else
                        {
                            destinoArchivo = Path.Combine(baseServidor, rutaSinUnidad);
                        }
                    }
                    else
                    {
                        string destinoCarpeta = Path.Combine(baseServidor, log.Notaria ?? "General");
                        destinoArchivo = !string.IsNullOrEmpty(log.ArchivoOriginal) ? Path.Combine(destinoCarpeta, log.ArchivoOriginal) : "";
                    }

                    // Resolver la ruta local de origen (rutaLocalResolved) usando la ruta local del escáner (rutaAuditoriaLocal)
                    // si estamos en el servidor y la ruta tiene una unidad mapeada (como Z:\) que el servidor no puede acceder directamente.
                    string rutaLocalResolved = rutaLocal;
                    bool existeLocal = !string.IsNullOrEmpty(rutaLocalResolved) && File.Exists(rutaLocalResolved);

                    if (!existeLocal && !string.IsNullOrEmpty(rutaLocal) && rutaLocal.Length >= 3 && rutaLocal[1] == ':' && rutaLocal[2] == '\\')
                    {
                        string rutaSinUnidad = rutaLocal.Substring(3);

                        // Posibilidad A: Carpeta con segmento duplicado usando la ruta de origen local (ej. Z:\NOTARIAS\NOTARIA 53... -> C:\NOTARIAS\NOTARIAS\NOTARIA 53...)
                        string rutaA = Path.Combine(rutaAuditoriaLocal, rutaSinUnidad);

                        // Posibilidad B: Carpeta sin duplicar segmento usando la ruta de origen local (ej. C:\NOTARIAS\NOTARIA 53...)
                        string folderBase = Path.GetFileName(rutaAuditoriaLocal);
                        string[] segmentos = rutaSinUnidad.Split('\\');
                        string primerSegmento = segmentos.Length > 0 ? segmentos[0] : "";
                        string rutaB = "";
                        
                        if (string.Equals(folderBase, primerSegmento, StringComparison.OrdinalIgnoreCase))
                        {
                            string subrutaLimpia = string.Join("\\", segmentos, 1, segmentos.Length - 1);
                            rutaB = Path.Combine(rutaAuditoriaLocal, subrutaLimpia);
                        }
                        else
                        {
                            string parentBase = Path.GetDirectoryName(rutaAuditoriaLocal) ?? "";
                            if (!string.IsNullOrEmpty(parentBase))
                            {
                                rutaB = Path.Combine(parentBase, rutaSinUnidad);
                            }
                        }

                        if (File.Exists(rutaA))
                        {
                            rutaLocalResolved = rutaA;
                            existeLocal = true;
                        }
                        else if (!string.IsNullOrEmpty(rutaB) && File.Exists(rutaB))
                        {
                            rutaLocalResolved = rutaB;
                            existeLocal = true;
                        }
                    }

                    bool existeDestino = !string.IsNullOrEmpty(destinoArchivo) && File.Exists(destinoArchivo);

                    // Si origen y destino coinciden físicamente, consideramos que el archivo ya está en el destino.
                    if (existeLocal && !string.IsNullOrEmpty(destinoArchivo) && string.Equals(Path.GetFullPath(rutaLocalResolved), Path.GetFullPath(destinoArchivo), StringComparison.OrdinalIgnoreCase))
                    {
                        existeDestino = true;
                    }

                    // Determinar si es captura de PDF o un log sin archivo (Login, Logout, etc.)
                    bool esCaptura = !string.IsNullOrEmpty(log.ArchivoOriginal);

                    if (esCaptura)
                    {
                        if (existeDestino)
                        {
                            // El archivo ya está en el servidor de destino (copiado previamente o guardado directo en red)
                            logsAEnviar.Add(log);
                        }
                        else if (existeLocal)
                        {
                            // El archivo está local en esta PC y no se ha copiado al servidor aún
                            try
                            {
                                string destinoCarpeta = Path.GetDirectoryName(destinoArchivo) ?? "";
                                if (!Directory.Exists(destinoCarpeta))
                                {
                                    Directory.CreateDirectory(destinoCarpeta);
                                }

                                // Copiar el archivo al servidor central
                                File.Copy(rutaLocalResolved, destinoArchivo, true);

                                // Si se copió con éxito, intentar borrarlo localmente
                                try
                                {
                                    File.Delete(rutaLocalResolved);
                                }
                                catch (Exception exDel)
                                {
                                    System.Diagnostics.Debug.WriteLine("No se pudo eliminar el archivo local: " + exDel.Message);
                                }

                                logsAEnviar.Add(log);
                            }
                            catch (Exception exCopy)
                            {
                                System.Diagnostics.Debug.WriteLine("Error al copiar archivo a ssdirec: " + exCopy.Message);
                                huboErroresDeCopia = true;
                            }
                        }
                        else
                        {
                            // No existe ni localmente ni en el destino.
                            // ¿Este registro pertenece a esta PC?
                            string pcActual = string.IsNullOrEmpty(ModuloConfiguracion.NombrePC) ? Environment.MachineName : ModuloConfiguracion.NombrePC;
                            bool esDeEstaPC = string.Equals(log.PC, pcActual, StringComparison.OrdinalIgnoreCase);

                            if (esDeEstaPC)
                            {
                                // Si es de esta PC y no existe en ningún lado, se asume perdido/borrado manual.
                                // Se agrega para no dejar el registro trabado.
                                logsAEnviar.Add(log);
                            }
                            else
                            {
                                // Si es de otra PC, significa que la otra PC no ha copiado su archivo a ssdirec todavía.
                                // NO lo agregamos a logsAEnviar para que no se marque como enviado y podamos esperar a que la otra PC lo suba.
                                System.Diagnostics.Debug.WriteLine(string.Format("El archivo {0} de la PC {1} aún no existe en ssdirec. Se pospone envío.", log.ArchivoOriginal, log.PC));
                            }
                        }
                    }
                    else
                    {
                        // Registros sin archivo (ej. Login/Logout/etc.)
                        logsAEnviar.Add(log);
                    }
                }

                if (logsAEnviar.Count == 0)
                {
                    if (!silencioso && formularioProgreso != null)
                    {
                        formularioProgreso.Close();
                    }
                    if (!silencioso)
                    {
                        if (huboErroresDeCopia)
                        {
                            string detalleRutas = "";
                            if (registrosPendientes.Count > 0)
                            {
                                var primerLog = registrosPendientes[0];
                                string rutaLoc = primerLog.RutaLocal ?? "";
                                if (string.IsNullOrEmpty(rutaLoc) && !string.IsNullOrEmpty(ultimaRuta))
                                {
                                    rutaLoc = Path.Combine(ultimaRuta, primerLog.ArchivoOriginal ?? "");
                                }

                                string baseServ = @"\\172.40.5.84\ssdirec\NOTARIAS";
                                string rutaAudLoc = !string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria)
                                    ? ModuloConfiguracion.RutaServidorAuditoria
                                    : @"\\192.168.1.10\NOTARIAS";

                                // Destino
                                string destArch = "";
                                if (!string.IsNullOrEmpty(rutaLoc) && rutaLoc.Length >= 3 && rutaLoc[1] == ':' && rutaLoc[2] == '\\')
                                {
                                    string rutaSinUnidad = rutaLoc.Substring(3);
                                    string folderBase = Path.GetFileName(baseServ);
                                    string[] segmentos = rutaSinUnidad.Split('\\');
                                    string primerSegmento = segmentos.Length > 0 ? segmentos[0] : "";

                                    if (string.Equals(folderBase, primerSegmento, StringComparison.OrdinalIgnoreCase))
                                    {
                                        string subrutaLimpia = string.Join("\\", segmentos, 1, segmentos.Length - 1);
                                        destArch = Path.Combine(baseServ, subrutaLimpia);
                                    }
                                    else
                                    {
                                        destArch = Path.Combine(baseServ, rutaSinUnidad);
                                    }
                                }
                                else
                                {
                                    string destCarp = Path.Combine(baseServ, primerLog.Notaria ?? "General");
                                    destArch = !string.IsNullOrEmpty(primerLog.ArchivoOriginal) ? Path.Combine(destCarp, primerLog.ArchivoOriginal) : "";
                                }

                                // Origen
                                string rutaLocRes = rutaLoc;
                                bool existeLoc = !string.IsNullOrEmpty(rutaLocRes) && File.Exists(rutaLocRes);
                                string rutaA = "";
                                string rutaB = "";

                                if (!existeLoc && !string.IsNullOrEmpty(rutaLoc) && rutaLoc.Length >= 3 && rutaLoc[1] == ':' && rutaLoc[2] == '\\')
                                {
                                    string rutaSinUnidad = rutaLoc.Substring(3);
                                    rutaA = Path.Combine(rutaAudLoc, rutaSinUnidad);

                                    string folderBase = Path.GetFileName(rutaAudLoc);
                                    string[] segmentos = rutaSinUnidad.Split('\\');
                                    string primerSegmento = segmentos.Length > 0 ? segmentos[0] : "";
                                    
                                    if (string.Equals(folderBase, primerSegmento, StringComparison.OrdinalIgnoreCase))
                                    {
                                        string subrutaLimpia = string.Join("\\", segmentos, 1, segmentos.Length - 1);
                                        rutaB = Path.Combine(rutaAudLoc, subrutaLimpia);
                                    }
                                    else
                                    {
                                        string parentBase = Path.GetDirectoryName(rutaAudLoc) ?? "";
                                        if (!string.IsNullOrEmpty(parentBase))
                                        {
                                            rutaB = Path.Combine(parentBase, rutaSinUnidad);
                                        }
                                    }

                                    if (File.Exists(rutaA))
                                    {
                                        rutaLocRes = rutaA;
                                        existeLoc = true;
                                    }
                                    else if (!string.IsNullOrEmpty(rutaB) && File.Exists(rutaB))
                                    {
                                        rutaLocRes = rutaB;
                                        existeLoc = true;
                                    }
                                }

                                bool existeDest = !string.IsNullOrEmpty(destArch) && File.Exists(destArch);

                                detalleRutas = string.Format(
                                    "\n\nDetalles de búsqueda (primer archivo):\n" +
                                    "- Archivo original: {0}\n" +
                                    "- Ruta local en log: {1}\n" +
                                    "- Carpeta compartida origen: {2}\n" +
                                    "- Carpeta destino (VPN): {3}\n" +
                                    "- Origen resuelto: {4}\n" +
                                    "- Destino resuelto: {5}\n" +
                                    "- ¿Existe origen?: {6}\n" +
                                    "- ¿Existe destino?: {7}\n" +
                                    "- Intentó buscar origen duplicado en: {8}\n" +
                                    "- Intentó buscar origen limpio en: {9}",
                                    primerLog.ArchivoOriginal,
                                    rutaLoc,
                                    rutaAudLoc,
                                    baseServ,
                                    rutaLocRes,
                                    destArch,
                                    existeLoc ? "SÍ" : "NO",
                                    existeDest ? "SÍ" : "NO",
                                    string.IsNullOrEmpty(rutaA) ? "N/A" : rutaA,
                                    string.IsNullOrEmpty(rutaB) ? "N/A" : rutaB
                                );
                            }

                            MessageBox.Show("No se pudieron transferir los archivos al servidor central ssdirec. Verifique que la ruta de red de destino esté accesible y que su conexión de red sea estable." + detalleRutas, "Error de Sincronización", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            MessageBox.Show("No hay registros pendientes de esta PC con archivos locales disponibles para sincronizar.", "Información de Sincronización", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    return;
                }

                if (!silencioso && etiquetaProgreso != null && formularioProgreso != null && barraArchivos != null && barraAuditoria != null)
                {
                    barraArchivos.Visible = false;
                    barraAuditoria.Visible = true;
                    etiquetaProgreso.Text = "Enviando registros al servidor central...";
                    formularioProgreso.Refresh();
                    Application.DoEvents();
                }

                bool exito = EnviarLogsAlServidorCentralHttp(logsAEnviar);

                if (!silencioso && formularioProgreso != null)
                {
                    formularioProgreso.Close();
                }

                if (exito)
                {
                    // 3. Fase de actualización y escritura segura
                    // Agrupamos los logs enviados exitosamente por su archivo JSON de origen
                    var logsPorOrigen = logsAEnviar
                        .Where(l => !string.IsNullOrEmpty(l.RutaJsonOrigen))
                        .GroupBy(l => l.RutaJsonOrigen!);

                    foreach (var grupo in logsPorOrigen)
                    {
                        string rutaJson = grupo.Key;
                        bool esLocal = (rutaJson == rutaJsonLocal);

                        Action guardar = () =>
                        {
                            if (File.Exists(rutaJson))
                            {
                                try
                                {
                                    string jsonActual = "";
                                    using (FileStream fs = new FileStream(rutaJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                    using (StreamReader sr = new StreamReader(fs))
                                    {
                                        jsonActual = sr.ReadToEnd();
                                    }

                                    var auditoriaActual = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(jsonActual);
                                    if (auditoriaActual?.Registros != null)
                                    {
                                        foreach (var logEnviado in grupo)
                                        {
                                            var reg = auditoriaActual.Registros.FirstOrDefault(r =>
                                                string.Equals(r.ArchivoOriginal, logEnviado.ArchivoOriginal, StringComparison.OrdinalIgnoreCase) &&
                                                string.Equals(r.PC, logEnviado.PC, StringComparison.OrdinalIgnoreCase) &&
                                                r.FechaHora == logEnviado.FechaHora);

                                            if (reg != null)
                                            {
                                                reg.Enviado = true;
                                            }
                                        }

                                        string jsonActualizado = JsonConvert.SerializeObject(auditoriaActual, Formatting.Indented);

                                        for (int i = 0; i < 3; i++)
                                        {
                                            try
                                            {
                                                File.WriteAllText(rutaJson, jsonActualizado);
                                                break;
                                            }
                                            catch
                                            {
                                                System.Threading.Thread.Sleep(500);
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }
                        };

                        if (esLocal)
                        {
                            lock (_lockJson)
                            {
                                guardar();
                            }
                        }
                        else
                        {
                            guardar();
                        }
                    }

                    // Escribir JSON consolidado en ssdirec para SyncAuditorias.php (L42)
                    try
                    {
                        string rutaJsonCentral = @"\\172.40.5.84\ssdirec\NOTARIAS\auditoria.json";
                        string? directorioCentral = Path.GetDirectoryName(rutaJsonCentral);
                        if (!string.IsNullOrEmpty(directorioCentral) && ExisteDirectorioConTimeout(directorioCentral, 1500))
                        {
                            var todosLosRegistros = ObtenerRegistrosTodos();
                            var datosAEnviar = new { Registros = todosLosRegistros };
                            string jsonConsolidado = JsonConvert.SerializeObject(datosAEnviar, Formatting.Indented);
                            File.WriteAllText(rutaJsonCentral, jsonConsolidado);
                        }
                    }
                    catch (Exception exCentralJson)
                    {
                        System.Diagnostics.Debug.WriteLine("No se pudo escribir el JSON consolidado en ssdirec: " + exCentralJson.Message);
                    }

                    if (!silencioso)
                    {
                        MessageBox.Show(string.Format("Sincronización completada con éxito. Se enviaron {0} registros y sus archivos correspondientes.", logsAEnviar.Count), "Resultado de Sincronización", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    if (!silencioso)
                    {
                        MessageBox.Show("No se pudo conectar con el servidor central de auditoría o el servidor devolvió un error. Los registros permanecen guardados localmente para reintentar más tarde.", "Error de Sincronización", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!silencioso)
                    MessageBox.Show("Ocurrió un error general durante la sincronización: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
public static List<DiagnosticoPC> ObtenerDiagnosticoPCs()
        {
            List<DiagnosticoPC> lista = new List<DiagnosticoPC>();

            // 1. Diagnóstico de la conexión HTTP al Servidor API
            DiagnosticoPC diagApi = new DiagnosticoPC { PC = "Servidor Central API" };
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    // Hacemos un GET de prueba al host principal
                    Uri uri = new Uri(ModuloConfiguracion.UrlApi);
                    string urlPrueba = uri.GetLeftPart(UriPartial.Authority);
                    var task = client.GetAsync(urlPrueba);
                    task.Wait();
                    var response = task.Result;
                    
                    diagApi.Estado = "🟢 Conectado";
                    diagApi.Detalles = "Conexión exitosa a la API central.";
                    diagApi.UltimaModificacion = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    diagApi.EsCorrecto = true;
                }
            }
            catch (Exception ex)
            {
                diagApi.Estado = "🔴 Desconectado";
                string det = ex.InnerException?.Message ?? ex.Message;
                diagApi.Detalles = det.Length > 80 ? det.Substring(0, 80) + "..." : det;
                diagApi.UltimaModificacion = "-";
                diagApi.EsCorrecto = false;
            }
            lista.Add(diagApi);

            // 2. Diagnóstico del archivo de auditoría local
            DiagnosticoPC diagLocal = new DiagnosticoPC { PC = "Auditoría Local (PC)" };
            try
            {
                string rutaJson = ObtenerRutaJsonLocal();
                if (File.Exists(rutaJson))
                {
                    DateTime ultimaMod = File.GetLastWriteTime(rutaJson);
                    diagLocal.UltimaModificacion = ultimaMod.ToString("yyyy-MM-dd HH:mm:ss");
                    
                    string json = "";
                    using (FileStream fs = new FileStream(rutaJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        json = sr.ReadToEnd();
                    }
                    var obj = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json);
                    int total = obj?.Registros?.Count ?? 0;
                    int pendientes = obj?.Registros?.Count(r => r.Enviado != true) ?? 0;

                    diagLocal.Estado = "🟢 Conectado";
                    diagLocal.Detalles = string.Format("Legible. Total logs: {0} | Pendientes: {1}", total, pendientes);
                    diagLocal.EsCorrecto = true;
                }
                else
                {
                    diagLocal.Estado = "⚠️ Sin archivo";
                    diagLocal.Detalles = "No se ha generado el archivo de auditoría local aún.";
                    diagLocal.UltimaModificacion = "-";
                    diagLocal.EsCorrecto = true;
                }
            }
            catch (Exception ex)
            {
                diagLocal.Estado = "🔴 Error de lectura";
                diagLocal.Detalles = ex.Message;
                diagLocal.UltimaModificacion = "-";
                diagLocal.EsCorrecto = false;
            }
            lista.Add(diagLocal);

            return lista;
        }
    }

    public class DiagnosticoPC
    {
        public string PC { get; set; } = "";
        public string Estado { get; set; } = "";
        public string Detalles { get; set; } = "";
        public string UltimaModificacion { get; set; } = "";
        public bool EsCorrecto { get; set; } = false;
    }
}
