using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    /// <summary>
    /// Exporta los registros de SQLite local a la carpeta de red compartida.
    /// Cada PC escribe SOLO en su propia carpeta: \\192.168.1.10\...\MonitoreoCaptura\PC-XX\
    /// </summary>
    public static class ServicioExportacionRed
    {
        private static bool ExisteDirectorioConTimeout(string ruta, int timeoutMs = 1500)
        {
            try
            {
                var tarea = System.Threading.Tasks.Task.Run(() => Directory.Exists(ruta));
                return tarea.Wait(timeoutMs) && tarea.Result;
            }
            catch { return false; }
        }

        /// <summary>
        /// Exporta todos los registros locales al JSON de red de esta PC.
        /// Antes de exportar, sincroniza el estado "enviado" desde la red para no sobreescribirlo.
        /// </summary>
        public static void ExportarARedLocal()
        {
            try
            {
                if (string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria) ||
                    string.IsNullOrEmpty(ModuloConfiguracion.NombrePC))
                    return;

                if (!ExisteDirectorioConTimeout(ModuloConfiguracion.RutaServidorAuditoria))
                    return;

                string rutaMonitoreo = Path.Combine(
                    ModuloConfiguracion.RutaServidorAuditoria,
                    "MonitoreoCaptura",
                    ModuloConfiguracion.NombrePC);

                if (!Directory.Exists(rutaMonitoreo))
                    Directory.CreateDirectory(rutaMonitoreo);

                string rutaJsonRed = Path.Combine(rutaMonitoreo, "auditoria.json");

                // 1. Si ya existe un JSON en red, leer el estado "enviado" y sincronizarlo al SQLite local
                if (File.Exists(rutaJsonRed))
                {
                    try
                    {
                        string jsonExistente = "";
                        using (var fs = new FileStream(rutaJsonRed, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var sr = new StreamReader(fs, System.Text.Encoding.UTF8))
                        {
                            jsonExistente = sr.ReadToEnd();
                        }

                        var auditoriaExistente = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(jsonExistente);
                        if (auditoriaExistente?.Registros != null)
                        {
                            // Sincronizar registros marcados como "enviado=true" por la PC servidor
                            RepositorioAuditoria.SincronizarEstadoEnviadoDesdeRed(auditoriaExistente.Registros);
                        }
                    }
                    catch { }
                }

                // 2. Leer todos los registros locales (ya con el estado enviado actualizado)
                var registrosLocales = RepositorioAuditoria.ObtenerRegistrosLocales();

                // 3. Escribir el JSON limpio a la red
                var auditoria = new ArchivoAuditoriaJson { Registros = registrosLocales };
                string jsonFinal = JsonConvert.SerializeObject(auditoria, Formatting.Indented);

                // Escribir con reintentos silenciosos
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        File.WriteAllText(rutaJsonRed, jsonFinal);
                        break;
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al exportar a red: " + ex.Message);
            }
        }
    }
}
