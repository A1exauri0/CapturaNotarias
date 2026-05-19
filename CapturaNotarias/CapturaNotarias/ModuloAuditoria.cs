using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    public class RegistroAuditoria
    {
        public string? FechaHora { get; set; }
        public string? Usuario { get; set; }
        public string? NombreCompleto { get; set; }
        public string? PC { get; set; }
        public string? Notaria { get; set; }
        public string? Accion { get; set; }
        public string? ArchivoOriginal { get; set; }
        public string? Detalles { get; set; }
    }

    public class ArchivoAuditoriaJson
    {
        public List<RegistroAuditoria> Registros { get; set; } = new List<RegistroAuditoria>();
    }

    public static class ModuloAuditoria
    {
        public static void RegistrarAccion(string notaria, string accion, string archivo, string detalles = "")
        {
            try
            {
                string pcNombre = Environment.MachineName;
                // Carpeta centralizada en el servidor para guardar las auditorías de captura
                string rutaCarpetaPC = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "MonitoreoCaptura", pcNombre);
                
                if (!Directory.Exists(rutaCarpetaPC))
                    Directory.CreateDirectory(rutaCarpetaPC);

                string rutaJson = Path.Combine(rutaCarpetaPC, "auditoria.json");
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

                // Inyectamos el nuevo registro
                auditoria.Registros.Add(new RegistroAuditoria
                {
                    FechaHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Usuario = ModuloConfiguracion.UsuarioActual,
                    NombreCompleto = ModuloConfiguracion.NombreCompletoActual,
                    PC = pcNombre,
                    Notaria = notaria,
                    Accion = accion,
                    ArchivoOriginal = archivo,
                    Detalles = detalles
                });

                string jsonFinal = JsonConvert.SerializeObject(auditoria, Formatting.Indented);
                
                // Intentamos guardar con reintentos silenciosos
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        File.WriteAllText(rutaJson, jsonFinal);
                        break;
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                }
            }
            catch
            {
                // En segundo plano no interrumpimos con MsgBox
            }
        }
    }
}
