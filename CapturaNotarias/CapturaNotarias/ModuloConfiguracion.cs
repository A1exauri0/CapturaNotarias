using System;
using System.IO;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    public class ConfiguracionApp
    {
        private string? _rutaServidorAuditoria;
        public string? RutaServidorAuditoria
        {
            get
            {
                if (string.IsNullOrEmpty(_rutaServidorAuditoria))
                {
                    return @"\\192.168.1.10\" + (TipoCaptura ?? "NOTARIAS");
                }
                return _rutaServidorAuditoria;
            }
            set
            {
                _rutaServidorAuditoria = value;
            }
        }
        public string? UltimaRutaVigilada { get; set; }
        public string? NombrePC { get; set; }
        public string? LugarTrabajo { get; set; }
        public string? UrlApi { get; set; } = "https://app.astronmx.cloud/api/digitalizacion/registrar";
        public bool ActivarEnvioAuditoria { get; set; } = false;
        public string? TipoCaptura { get; set; }
    }

    public static class ModuloConfiguracion
    {
        public static string UsuarioActual = "";
        public static string NombreCompletoActual = "";
        public static string TurnoActual = "";
        public static string TipoCaptura = "";
        
        // El servidor donde caen los logs de auditoría general
        private static string? _rutaServidorAuditoria;
        public static string RutaServidorAuditoria
        {
            get
            {
                if (string.IsNullOrEmpty(_rutaServidorAuditoria))
                {
                    return @"\\192.168.1.10\" + (string.IsNullOrEmpty(TipoCaptura) ? "NOTARIAS" : TipoCaptura);
                }
                return _rutaServidorAuditoria;
            }
            set
            {
                _rutaServidorAuditoria = value;
            }
        }
        public static string NombrePC = "";
        public static string LugarTrabajo = "";
        public static string UrlApi = "https://app.astronmx.cloud/api/digitalizacion/registrar";
        public static bool ActivarEnvioAuditoria = false;

        // Configuración fija del servidor HTTP local integrado
        public const string IP_SERVIDOR = "192.168.1.10";
        public const int PUERTO_SERVIDOR = 5050;

        // Se auto-detecta al cargar la configuración: si la IP local es la del servidor, actúa como servidor
        public static bool EsServidor { get; private set; } = false;

        /// <summary>
        /// Detecta si esta PC es el servidor comparando su IP local con la IP fija del servidor.
        /// </summary>
        public static void DetectarSiEsServidor()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        ip.ToString() == IP_SERVIDOR)
                    {
                        EsServidor = true;
                        return;
                    }
                }
            }
            catch { }
            EsServidor = false;
        }

        // Obtener ruta local donde guardaremos las preferencias del usuario (Servidor y ultima ruta)
        private static string ObtenerArchivoConfig()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CapturaNotarias");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "config.json");
        }

        public static ConfiguracionApp CargarConfiguracion()
        {
            string archivo = ObtenerArchivoConfig();
            if (File.Exists(archivo))
            {
                try
                {
                    string json = File.ReadAllText(archivo);
                    var config = JsonConvert.DeserializeObject<ConfiguracionApp>(json) ?? new ConfiguracionApp();
                    
                    if (!string.IsNullOrEmpty(config.TipoCaptura))
                    {
                        TipoCaptura = config.TipoCaptura;
                    }
                    else
                    {
                        TipoCaptura = "NOTARIAS";
                    }

                    if (!string.IsNullOrEmpty(config.RutaServidorAuditoria))
                        RutaServidorAuditoria = config.RutaServidorAuditoria;
                    
                    if (!string.IsNullOrEmpty(config.NombrePC))
                        NombrePC = config.NombrePC;

                    if (!string.IsNullOrEmpty(config.LugarTrabajo))
                        LugarTrabajo = config.LugarTrabajo;

                    if (!string.IsNullOrEmpty(config.UrlApi))
                    {
                        if (config.UrlApi.Contains("localhost:8000"))
                        {
                            config.UrlApi = "https://app.astronmx.cloud/api/digitalizacion/registrar";
                            try { File.WriteAllText(archivo, JsonConvert.SerializeObject(config, Formatting.Indented)); } catch {}
                        }
                        else if (config.UrlApi.EndsWith("/api/registrar"))
                        {
                            config.UrlApi = config.UrlApi.Substring(0, config.UrlApi.Length - "/api/registrar".Length) + "/api/digitalizacion/registrar";
                            try { File.WriteAllText(archivo, JsonConvert.SerializeObject(config, Formatting.Indented)); } catch {}
                        }
                        else if (config.UrlApi.EndsWith("/api/notarias/registrar"))
                        {
                            config.UrlApi = config.UrlApi.Substring(0, config.UrlApi.Length - "/api/notarias/registrar".Length) + "/api/digitalizacion/registrar";
                            try { File.WriteAllText(archivo, JsonConvert.SerializeObject(config, Formatting.Indented)); } catch {}
                        }
                        UrlApi = config.UrlApi;
                    }

                    ActivarEnvioAuditoria = config.ActivarEnvioAuditoria;

                    // Auto-detectar si esta PC es el servidor
                    DetectarSiEsServidor();

                    return config;
                }
                catch { }
            }

            // Si el archivo no existe, creamos la configuración por defecto y la guardamos
            var configDefecto = new ConfiguracionApp();
            GuardarConfiguracion(configDefecto);
            return configDefecto;
        }

        public static void GuardarConfiguracion(ConfiguracionApp config)
        {
            try
            {
                if (!string.IsNullOrEmpty(config.TipoCaptura))
                    TipoCaptura = config.TipoCaptura;

                if (!string.IsNullOrEmpty(config.RutaServidorAuditoria))
                    RutaServidorAuditoria = config.RutaServidorAuditoria;
                
                if (!string.IsNullOrEmpty(config.NombrePC))
                    NombrePC = config.NombrePC;

                if (!string.IsNullOrEmpty(config.LugarTrabajo))
                    LugarTrabajo = config.LugarTrabajo;

                if (!string.IsNullOrEmpty(config.UrlApi))
                    UrlApi = config.UrlApi;

                ActivarEnvioAuditoria = config.ActivarEnvioAuditoria;

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ObtenerArchivoConfig(), json);
            }
            catch { }
        }
    }
}
