using System;
using System.IO;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    public class ConfiguracionApp
    {
        public string? RutaServidorAuditoria { get; set; }
        public string? UltimaRutaVigilada { get; set; }
        public string? NombrePC { get; set; }
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

                    ActivarEnvioAuditoria = config.ActivarEnvioAuditoria;

                    return config;
                }
                catch { }
            }
            return new ConfiguracionApp();
        }

        public static void GuardarConfiguracion(ConfiguracionApp config)
        {
            try
            {
                if (!string.IsNullOrEmpty(config.RutaServidorAuditoria))
                    RutaServidorAuditoria = config.RutaServidorAuditoria;
                
                if (!string.IsNullOrEmpty(config.NombrePC))
                    NombrePC = config.NombrePC;

                ActivarEnvioAuditoria = config.ActivarEnvioAuditoria;

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ObtenerArchivoConfig(), json);
            }
            catch { }
        }
    }
}
