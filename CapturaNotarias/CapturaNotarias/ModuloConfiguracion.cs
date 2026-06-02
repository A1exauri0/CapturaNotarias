using System;
using System.IO;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    public class ConfiguracionApp
    {
        public string? RutaServidorAuditoria { get; set; } = @"\\192.168.1.10\NOTARIAS";
        public string? UltimaRutaVigilada { get; set; }
        public string? NombrePC { get; set; }
        public string? LugarTrabajo { get; set; }
        public string? UrlApi { get; set; } = "http://localhost:8000/api/digitalizacion/registrar";
        public bool ActivarEnvioAuditoria { get; set; } = false;
    }

    public static class ModuloConfiguracion
    {
        public static string UsuarioActual = "";
        public static string NombreCompletoActual = "";
        public static string TurnoActual = "";
        
        // El servidor donde caen los logs de auditoría general
        public static string RutaServidorAuditoria = @"\\192.168.1.10\NOTARIAS"; 
        public static string NombrePC = "";
        public static string LugarTrabajo = "";
        public static string UrlApi = "http://localhost:8000/api/digitalizacion/registrar";
        public static bool ActivarEnvioAuditoria = false;

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
                    
                    if (!string.IsNullOrEmpty(config.RutaServidorAuditoria))
                        RutaServidorAuditoria = config.RutaServidorAuditoria;
                    
                    if (!string.IsNullOrEmpty(config.NombrePC))
                        NombrePC = config.NombrePC;

                    if (!string.IsNullOrEmpty(config.LugarTrabajo))
                        LugarTrabajo = config.LugarTrabajo;

                    if (!string.IsNullOrEmpty(config.UrlApi))
                    {
                        if (config.UrlApi.EndsWith("/api/registrar"))
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
