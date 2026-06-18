using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    /// <summary>
    /// Migración única de archivos JSON existentes a SQLite.
    /// Se ejecuta automáticamente la primera vez que la app inicia con la nueva versión.
    /// </summary>
    public static class MigradorJsonASqlite
    {
        private static readonly string _archivoMarcador = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CapturaNotarias", ".migracion_completada");

        /// <summary>
        /// Verifica si ya se hizo la migración. Si no, la ejecuta.
        /// </summary>
        public static void EjecutarSiNecesario()
        {
            // Si el marcador existe, la migración ya se hizo
            if (File.Exists(_archivoMarcador))
                return;

            int totalMigrados = 0;
            int totalDuplicados = 0;

            // 1. Migrar el JSON local de AppData
            string carpetaLocal = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CapturaNotarias");
            string rutaJsonLocal = Path.Combine(carpetaLocal, "auditoria_local.json");

            if (File.Exists(rutaJsonLocal))
            {
                var (migrados, duplicados) = MigrarArchivoJson(rutaJsonLocal);
                totalMigrados += migrados;
                totalDuplicados += duplicados;

                // Renombrar como respaldo (no borrar)
                try
                {
                    string respaldo = rutaJsonLocal + ".migrado";
                    if (File.Exists(respaldo)) File.Delete(respaldo);
                    File.Move(rutaJsonLocal, respaldo);
                }
                catch { }
            }

            // 2. Migrar el JSON del servidor de red (si es accesible)
            try
            {
                if (!string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria) &&
                    !string.IsNullOrEmpty(ModuloConfiguracion.NombrePC))
                {
                    string rutaMonitoreo = Path.Combine(
                        ModuloConfiguracion.RutaServidorAuditoria,
                        "MonitoreoCaptura",
                        ModuloConfiguracion.NombrePC);

                    string rutaJsonServidor = Path.Combine(rutaMonitoreo, "auditoria.json");

                    if (File.Exists(rutaJsonServidor))
                    {
                        var (migrados, duplicados) = MigrarArchivoJson(rutaJsonServidor);
                        totalMigrados += migrados;
                        totalDuplicados += duplicados;
                        // No renombramos el del servidor — el servidor puede necesitarlo
                    }
                }
            }
            catch { }

            // 3. Migrar el JSON de la raíz del proyecto si existe (auditoria.json en la raíz)
            try
            {
                string rutaRaiz = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auditoria.json");
                if (File.Exists(rutaRaiz))
                {
                    var (migrados, duplicados) = MigrarArchivoJson(rutaRaiz);
                    totalMigrados += migrados;
                    totalDuplicados += duplicados;
                }
            }
            catch { }

            // Crear el archivo marcador para no repetir la migración
            try
            {
                File.WriteAllText(_archivoMarcador, 
                    $"Migración completada: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"Registros migrados: {totalMigrados}\n" +
                    $"Duplicados omitidos: {totalDuplicados}");
            }
            catch { }

            System.Diagnostics.Debug.WriteLine(
                $"Migración JSON→SQLite completada: {totalMigrados} migrados, {totalDuplicados} duplicados omitidos");
        }

        /// <summary>
        /// Lee un archivo JSON de auditoría e inserta sus registros en SQLite.
        /// </summary>
        private static (int migrados, int duplicados) MigrarArchivoJson(string rutaJson)
        {
            int migrados = 0;
            int duplicados = 0;

            try
            {
                string json = "";
                using (var fs = new FileStream(rutaJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, System.Text.Encoding.UTF8))
                {
                    json = sr.ReadToEnd();
                }

                var auditoria = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json);
                if (auditoria?.Registros == null || auditoria.Registros.Count == 0)
                    return (0, 0);

                foreach (var registro in auditoria.Registros)
                {
                    // Saltar registros sin datos mínimos
                    if (string.IsNullOrEmpty(registro.FechaHora))
                        continue;

                    bool insertado = RepositorioAuditoria.InsertarRegistroMigracion(registro);
                    if (insertado)
                        migrados++;
                    else
                        duplicados++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al migrar JSON: " + ex.Message);
            }

            return (migrados, duplicados);
        }
    }
}
