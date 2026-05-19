using System;
using System.IO;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    public class ConfiguracionApp
    {
        public string? RutaServidorAuditoria { get; set; }
        public string? UltimaRutaVigilada { get; set; }
    }

    public static class ModuloConfiguracion
    {
        public static string UsuarioActual = "";
        public static string NombreCompletoActual = "";
        
        // El servidor donde caen los logs de auditoría general
        public static string RutaServidorAuditoria = @"C:\ServidorLocal"; 

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
                    return JsonConvert.DeserializeObject<ConfiguracionApp>(json) ?? new ConfiguracionApp();
                }
                catch { }
            }
            return new ConfiguracionApp();
        }

        public static void GuardarConfiguracion(ConfiguracionApp config)
        {
            try
            {
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ObtenerArchivoConfig(), json);
            }
            catch { }
        }
    }
}
