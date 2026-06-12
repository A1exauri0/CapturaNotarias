using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace CapturaNotarias
{
    /// <summary>
    /// Operaciones CRUD sobre la tabla registros_auditoria en SQLite.
    /// Reemplaza toda la lógica de lectura/escritura de JSON.
    /// </summary>
    public static class RepositorioAuditoria
    {
        /// <summary>
        /// Inserta un registro de captura. Retorna "OK", "DUPLICATE", "PC_MISMATCH", "NO_USER" o "ERROR".
        /// </summary>
        public static string InsertarRegistro(string notaria, string archivo, string rutaCompleta, string detalles, int paginas = 0)
        {
            try
            {
                // Si no hay un usuario activo, ignoramos el registro
                if (string.IsNullOrEmpty(ModuloConfiguracion.UsuarioActual))
                    return "NO_USER";

                string pcNombre = string.IsNullOrEmpty(ModuloConfiguracion.NombrePC)
                    ? Environment.MachineName
                    : ModuloConfiguracion.NombrePC;

                // Validar que el archivo pertenezca a esta PC (por prefijo PCXX)
                string archivoUpper = archivo.ToUpper();
                var coincidenciaPc = System.Text.RegularExpressions.Regex.Match(archivoUpper, @"^PC(\d+)");
                if (coincidenciaPc.Success)
                {
                    string prefijoArchivo = coincidenciaPc.Value;
                    string pcNombreNorm = pcNombre.Replace("-", "").Replace(" ", "").ToUpper();
                    if (pcNombreNorm != prefijoArchivo &&
                        !pcNombreNorm.Contains(prefijoArchivo) &&
                        !prefijoArchivo.Contains(pcNombreNorm))
                    {
                        return "PC_MISMATCH";
                    }
                }

                // Obtener IP local
                string localIp = ObtenerIpLocal();

                // Resolver la notaría si viene vacía o genérica
                string notariaResuelta = notaria;
                if (string.IsNullOrEmpty(notariaResuelta) ||
                    notariaResuelta.ToUpper() == "NOTARIAS" ||
                    notariaResuelta.ToUpper() == "GENERAL")
                {
                    string? extraida = ModuloAuditoria.ExtraerNotaria(rutaCompleta)
                                    ?? ModuloAuditoria.ExtraerNotaria(detalles);
                    if (!string.IsNullOrEmpty(extraida))
                        notariaResuelta = extraida;
                }

                string ahora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string hoy = DateTime.Now.ToString("yyyy-MM-dd");

                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var cmd = conexion.CreateCommand())
                {
                    // INSERT OR IGNORE: si el UNIQUE INDEX detecta duplicado, no inserta y retorna 0 filas
                    cmd.CommandText = @"
                        INSERT OR IGNORE INTO registros_auditoria 
                        (fecha_hora, fecha, usuario, nombre_completo, turno, pc, ip, notaria, 
                         accion, archivo_original, detalles, paginas, lugar_trabajo, enviado, exportado_red, ruta_local)
                        VALUES 
                        (@fecha_hora, @fecha, @usuario, @nombre_completo, @turno, @pc, @ip, @notaria,
                         'Capturado', @archivo_original, @detalles, @paginas, @lugar_trabajo, 0, 0, @ruta_local)";

                    cmd.Parameters.AddWithValue("@fecha_hora", ahora);
                    cmd.Parameters.AddWithValue("@fecha", hoy);
                    cmd.Parameters.AddWithValue("@usuario", ModuloConfiguracion.UsuarioActual);
                    cmd.Parameters.AddWithValue("@nombre_completo", ModuloConfiguracion.NombreCompletoActual);
                    cmd.Parameters.AddWithValue("@turno", ModuloConfiguracion.TurnoActual);
                    cmd.Parameters.AddWithValue("@pc", pcNombre);
                    cmd.Parameters.AddWithValue("@ip", localIp);
                    cmd.Parameters.AddWithValue("@notaria", notariaResuelta);
                    cmd.Parameters.AddWithValue("@archivo_original", archivo);
                    cmd.Parameters.AddWithValue("@detalles", detalles);
                    cmd.Parameters.AddWithValue("@paginas", paginas);
                    cmd.Parameters.AddWithValue("@lugar_trabajo", ModuloConfiguracion.LugarTrabajo);
                    cmd.Parameters.AddWithValue("@ruta_local", rutaCompleta);

                    int filasAfectadas = cmd.ExecuteNonQuery();

                    if (filasAfectadas == 0)
                        return "DUPLICATE";

                    return "OK";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al insertar registro: " + ex.Message);
                return "ERROR";
            }
        }

        /// <summary>
        /// Obtiene el ID del último registro insertado para esta PC/archivo/fecha.
        /// Se usa para encolar el conteo de páginas.
        /// </summary>
        public static long ObtenerUltimoIdInsertado(string archivoOriginal, string pc)
        {
            try
            {
                string hoy = DateTime.Now.ToString("yyyy-MM-dd");
                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT id FROM registros_auditoria 
                        WHERE archivo_original = @archivo AND pc = @pc AND fecha = @fecha AND accion = 'Capturado'
                        ORDER BY id DESC LIMIT 1";
                    cmd.Parameters.AddWithValue("@archivo", archivoOriginal);
                    cmd.Parameters.AddWithValue("@pc", pc);
                    cmd.Parameters.AddWithValue("@fecha", hoy);

                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? (long)resultado : -1;
                }
            }
            catch { return -1; }
        }

        /// <summary>
        /// Cuenta las capturas del día para un usuario en esta PC.
        /// </summary>
        public static int ObtenerContadorDelDia(string usuario)
        {
            try
            {
                string hoy = DateTime.Now.ToString("yyyy-MM-dd");
                string pcNombre = string.IsNullOrEmpty(ModuloConfiguracion.NombrePC)
                    ? Environment.MachineName
                    : ModuloConfiguracion.NombrePC;

                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COUNT(*) FROM registros_auditoria 
                        WHERE fecha = @fecha AND pc = @pc AND usuario = @usuario AND accion = 'Capturado'";
                    cmd.Parameters.AddWithValue("@fecha", hoy);
                    cmd.Parameters.AddWithValue("@pc", pcNombre);
                    cmd.Parameters.AddWithValue("@usuario", usuario);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch { return 0; }
        }

        /// <summary>
        /// Actualiza el conteo de páginas de un registro específico.
        /// </summary>
        public static void ActualizarPaginas(long idRegistro, int paginas)
        {
            try
            {
                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText = "UPDATE registros_auditoria SET paginas = @paginas, exportado_red = 0 WHERE id = @id";
                    cmd.Parameters.AddWithValue("@paginas", paginas);
                    cmd.Parameters.AddWithValue("@id", idRegistro);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al actualizar páginas: " + ex.Message);
            }
        }

        /// <summary>
        /// Obtiene registros con paginas=0 para reconteo en background.
        /// </summary>
        public static List<(long id, string rutaLocal)> ObtenerPendientesPaginas()
        {
            var resultado = new List<(long, string)>();
            try
            {
                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT id, ruta_local FROM registros_auditoria 
                        WHERE paginas = 0 AND ruta_local IS NOT NULL AND ruta_local != ''
                        ORDER BY id ASC LIMIT 50";

                    using (var lector = cmd.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            resultado.Add((lector.GetInt64(0), lector.GetString(1)));
                        }
                    }
                }
            }
            catch { }
            return resultado;
        }

        /// <summary>
        /// Recuenta páginas de todos los registros del día del usuario actual.
        /// Retorna (registros actualizados, total de páginas).
        /// </summary>
        public static (int actualizados, int totalPaginas) RecontarPaginasDelDia()
        {
            int actualizados = 0;
            int totalPaginas = 0;

            try
            {
                string hoy = DateTime.Now.ToString("yyyy-MM-dd");
                string pcNombre = string.IsNullOrEmpty(ModuloConfiguracion.NombrePC)
                    ? Environment.MachineName : ModuloConfiguracion.NombrePC;

                // Obtener registros del día
                var registros = new List<(long id, string rutaLocal, int paginasActuales)>();

                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                {
                    using (var cmdSelect = conexion.CreateCommand())
                    {
                        cmdSelect.CommandText = @"
                            SELECT id, ruta_local, paginas FROM registros_auditoria
                            WHERE fecha = @fecha AND pc = @pc AND usuario = @usuario AND accion = 'Capturado'";
                        cmdSelect.Parameters.AddWithValue("@fecha", hoy);
                        cmdSelect.Parameters.AddWithValue("@pc", pcNombre);
                        cmdSelect.Parameters.AddWithValue("@usuario", ModuloConfiguracion.UsuarioActual);

                        using (var lector = cmdSelect.ExecuteReader())
                        {
                            while (lector.Read())
                            {
                                string ruta = lector.IsDBNull(1) ? "" : lector.GetString(1);
                                registros.Add((lector.GetInt64(0), ruta, lector.GetInt32(2)));
                            }
                        }
                    }

                    // Recontar cada uno
                    foreach (var (id, rutaLocal, paginasActuales) in registros)
                    {
                        if (!string.IsNullOrEmpty(rutaLocal) && File.Exists(rutaLocal))
                        {
                            int paginasNuevas = ServicioContadorPaginas.ContarPaginasSincrono(rutaLocal);
                            if (paginasNuevas > 0 && paginasNuevas != paginasActuales)
                            {
                                ActualizarPaginas(id, paginasNuevas);
                                totalPaginas += paginasNuevas;
                                actualizados++;
                            }
                            else
                            {
                                totalPaginas += paginasActuales > 0 ? paginasActuales : 1;
                            }
                        }
                        else
                        {
                            totalPaginas += paginasActuales > 0 ? paginasActuales : 1;
                        }
                    }
                }
            }
            catch { }
            return (actualizados, totalPaginas);
        }

        /// <summary>
        /// Obtiene TODOS los registros locales (esta PC). Para auditoría y Excel.
        /// </summary>
        public static List<RegistroAuditoria> ObtenerRegistrosLocales()
        {
            var lista = new List<RegistroAuditoria>();
            try
            {
                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM registros_auditoria ORDER BY fecha_hora ASC";
                    using (var lector = cmd.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            lista.Add(MapearRegistro(lector));
                        }
                    }
                }
            }
            catch { }
            return lista;
        }

        /// <summary>
        /// Obtiene registros locales no exportados a la red.
        /// </summary>
        public static List<RegistroAuditoria> ObtenerRegistrosNoExportados()
        {
            var lista = new List<RegistroAuditoria>();
            try
            {
                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT * FROM registros_auditoria 
                        WHERE exportado_red = 0 
                        ORDER BY fecha_hora ASC";
                    using (var lector = cmd.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            lista.Add(MapearRegistro(lector));
                        }
                    }
                }
            }
            catch { }
            return lista;
        }

        /// <summary>
        /// Marca registros como exportados a la carpeta de red.
        /// </summary>
        public static void MarcarComoExportadoRed(List<long> ids)
        {
            if (ids == null || ids.Count == 0) return;
            try
            {
                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var transaccion = conexion.BeginTransaction())
                {
                    using (var cmd = conexion.CreateCommand())
                    {
                        cmd.CommandText = "UPDATE registros_auditoria SET exportado_red = 1 WHERE id = @id";
                        var paramId = cmd.Parameters.Add("@id", SqliteType.Integer);

                        foreach (var id in ids)
                        {
                            paramId.Value = id;
                            cmd.ExecuteNonQuery();
                        }
                    }
                    transaccion.Commit();
                }
            }
            catch { }
        }

        /// <summary>
        /// Marca TODOS los registros no exportados como exportados.
        /// Se usa después de enviar exitosamente al servidor HTTP local.
        /// </summary>
        public static void MarcarTodosComoExportadoRed()
        {
            try
            {
                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText = "UPDATE registros_auditoria SET exportado_red = 1 WHERE exportado_red = 0";
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        /// <summary>
        /// Sincroniza el estado "enviado" desde los JSONs de la red de vuelta al SQLite local.
        /// Se llama antes de exportar para no sobreescribir el trabajo del servidor.
        /// </summary>
        public static void SincronizarEstadoEnviadoDesdeRed(List<RegistroAuditoria> registrosRed)
        {
            try
            {
                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var transaccion = conexion.BeginTransaction())
                {
                    using (var cmd = conexion.CreateCommand())
                    {
                        cmd.CommandText = @"
                            UPDATE registros_auditoria SET enviado = 1 
                            WHERE archivo_original = @archivo AND pc = @pc AND fecha_hora = @fechaHora AND enviado = 0";

                        var pArchivo = cmd.Parameters.Add("@archivo", SqliteType.Text);
                        var pPc = cmd.Parameters.Add("@pc", SqliteType.Text);
                        var pFecha = cmd.Parameters.Add("@fechaHora", SqliteType.Text);

                        foreach (var reg in registrosRed.Where(r => r.Enviado == true))
                        {
                            pArchivo.Value = reg.ArchivoOriginal ?? "";
                            pPc.Value = reg.PC ?? "";
                            pFecha.Value = reg.FechaHora ?? "";
                            cmd.ExecuteNonQuery();
                        }
                    }
                    transaccion.Commit();
                }
            }
            catch { }
        }

        /// <summary>
        /// Inserta un registro desde la migración JSON (sin validaciones de PC/usuario activo).
        /// </summary>
        public static bool InsertarRegistroMigracion(RegistroAuditoria reg)
        {
            try
            {
                string fecha = "";
                if (!string.IsNullOrEmpty(reg.FechaHora) && reg.FechaHora.Length >= 10)
                    fecha = reg.FechaHora.Substring(0, 10);

                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR IGNORE INTO registros_auditoria 
                        (fecha_hora, fecha, usuario, nombre_completo, turno, pc, ip, notaria, 
                         accion, archivo_original, detalles, paginas, lugar_trabajo, enviado, exportado_red, ruta_local)
                        VALUES 
                        (@fecha_hora, @fecha, @usuario, @nombre_completo, @turno, @pc, @ip, @notaria,
                         @accion, @archivo_original, @detalles, @paginas, @lugar_trabajo, @enviado, 0, @ruta_local)";

                    cmd.Parameters.AddWithValue("@fecha_hora", reg.FechaHora ?? "");
                    cmd.Parameters.AddWithValue("@fecha", fecha);
                    cmd.Parameters.AddWithValue("@usuario", reg.Usuario ?? "");
                    cmd.Parameters.AddWithValue("@nombre_completo", reg.NombreCompleto ?? "");
                    cmd.Parameters.AddWithValue("@turno", reg.Turno ?? "");
                    cmd.Parameters.AddWithValue("@pc", reg.PC ?? "");
                    cmd.Parameters.AddWithValue("@ip", reg.IP ?? "");
                    cmd.Parameters.AddWithValue("@notaria", reg.Notaria ?? "");
                    cmd.Parameters.AddWithValue("@accion", reg.Accion ?? "Capturado");
                    cmd.Parameters.AddWithValue("@archivo_original", reg.ArchivoOriginal ?? "");
                    cmd.Parameters.AddWithValue("@detalles", reg.Detalles ?? "");
                    cmd.Parameters.AddWithValue("@paginas", reg.Paginas > 0 ? reg.Paginas : 1);
                    cmd.Parameters.AddWithValue("@lugar_trabajo", reg.LugarTrabajo ?? "");
                    cmd.Parameters.AddWithValue("@enviado", reg.Enviado == true ? 1 : 0);
                    cmd.Parameters.AddWithValue("@ruta_local", reg.RutaLocal ?? "");

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// Inserta un lote de registros desde la migración JSON o sincronización en una sola transacción.
        /// </summary>
        public static void InsertarRegistrosMigracionBatch(List<RegistroAuditoria> registros)
        {
            if (registros == null || registros.Count == 0) return;
            try
            {
                using (var conexion = ServicioBaseDatos.ObtenerConexion())
                using (var transaccion = conexion.BeginTransaction())
                {
                    using (var cmd = conexion.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT OR IGNORE INTO registros_auditoria 
                            (fecha_hora, fecha, usuario, nombre_completo, turno, pc, ip, notaria, 
                             accion, archivo_original, detalles, paginas, lugar_trabajo, enviado, exportado_red, ruta_local)
                            VALUES 
                            (@fecha_hora, @fecha, @usuario, @nombre_completo, @turno, @pc, @ip, @notaria,
                             @accion, @archivo_original, @detalles, @paginas, @lugar_trabajo, @enviado, 0, @ruta_local)";

                        var pFechaHora = cmd.Parameters.Add("@fecha_hora", SqliteType.Text);
                        var pFecha = cmd.Parameters.Add("@fecha", SqliteType.Text);
                        var pUsuario = cmd.Parameters.Add("@usuario", SqliteType.Text);
                        var pNombre = cmd.Parameters.Add("@nombre_completo", SqliteType.Text);
                        var pTurno = cmd.Parameters.Add("@turno", SqliteType.Text);
                        var pPc = cmd.Parameters.Add("@pc", SqliteType.Text);
                        var pIp = cmd.Parameters.Add("@ip", SqliteType.Text);
                        var pNotaria = cmd.Parameters.Add("@notaria", SqliteType.Text);
                        var pAccion = cmd.Parameters.Add("@accion", SqliteType.Text);
                        var pArchivo = cmd.Parameters.Add("@archivo_original", SqliteType.Text);
                        var pDetalles = cmd.Parameters.Add("@detalles", SqliteType.Text);
                        var pPaginas = cmd.Parameters.Add("@paginas", SqliteType.Integer);
                        var pLugar = cmd.Parameters.Add("@lugar_trabajo", SqliteType.Text);
                        var pEnviado = cmd.Parameters.Add("@enviado", SqliteType.Integer);
                        var pRuta = cmd.Parameters.Add("@ruta_local", SqliteType.Text);

                        foreach (var reg in registros)
                        {
                            if (string.IsNullOrEmpty(reg.FechaHora)) continue;

                            string fecha = reg.FechaHora.Length >= 10 ? reg.FechaHora.Substring(0, 10) : "";

                            pFechaHora.Value = reg.FechaHora ?? "";
                            pFecha.Value = fecha;
                            pUsuario.Value = reg.Usuario ?? "";
                            pNombre.Value = reg.NombreCompleto ?? "";
                            pTurno.Value = reg.Turno ?? "";
                            pPc.Value = reg.PC ?? "";
                            pIp.Value = reg.IP ?? "";
                            pNotaria.Value = reg.Notaria ?? "";
                            pAccion.Value = reg.Accion ?? "Capturado";
                            pArchivo.Value = reg.ArchivoOriginal ?? "";
                            pDetalles.Value = reg.Detalles ?? "";
                            pPaginas.Value = reg.Paginas > 0 ? reg.Paginas : 1;
                            pLugar.Value = reg.LugarTrabajo ?? "";
                            pEnviado.Value = reg.Enviado == true ? 1 : 0;
                            pRuta.Value = reg.RutaLocal ?? "";

                            cmd.ExecuteNonQuery();
                        }
                    }
                    transaccion.Commit();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en inserción batch: " + ex.Message);
            }
        }

        // ── Utilidades internas ──

        private static string ObtenerIpLocal()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            catch { }
            return "";
        }

        private static RegistroAuditoria MapearRegistro(SqliteDataReader lector)
        {
            return new RegistroAuditoria
            {
                FechaHora = lector.IsDBNull(lector.GetOrdinal("fecha_hora")) ? null : lector.GetString(lector.GetOrdinal("fecha_hora")),
                Usuario = lector.IsDBNull(lector.GetOrdinal("usuario")) ? null : lector.GetString(lector.GetOrdinal("usuario")),
                NombreCompleto = lector.IsDBNull(lector.GetOrdinal("nombre_completo")) ? null : lector.GetString(lector.GetOrdinal("nombre_completo")),
                Turno = lector.IsDBNull(lector.GetOrdinal("turno")) ? null : lector.GetString(lector.GetOrdinal("turno")),
                PC = lector.IsDBNull(lector.GetOrdinal("pc")) ? null : lector.GetString(lector.GetOrdinal("pc")),
                IP = lector.IsDBNull(lector.GetOrdinal("ip")) ? null : lector.GetString(lector.GetOrdinal("ip")),
                Notaria = lector.IsDBNull(lector.GetOrdinal("notaria")) ? null : lector.GetString(lector.GetOrdinal("notaria")),
                Accion = lector.IsDBNull(lector.GetOrdinal("accion")) ? null : lector.GetString(lector.GetOrdinal("accion")),
                ArchivoOriginal = lector.IsDBNull(lector.GetOrdinal("archivo_original")) ? null : lector.GetString(lector.GetOrdinal("archivo_original")),
                Detalles = lector.IsDBNull(lector.GetOrdinal("detalles")) ? null : lector.GetString(lector.GetOrdinal("detalles")),
                Paginas = lector.IsDBNull(lector.GetOrdinal("paginas")) ? 1 : lector.GetInt32(lector.GetOrdinal("paginas")),
                LugarTrabajo = lector.IsDBNull(lector.GetOrdinal("lugar_trabajo")) ? null : lector.GetString(lector.GetOrdinal("lugar_trabajo")),
                Enviado = !lector.IsDBNull(lector.GetOrdinal("enviado")) && lector.GetInt32(lector.GetOrdinal("enviado")) == 1,
                RutaLocal = lector.IsDBNull(lector.GetOrdinal("ruta_local")) ? null : lector.GetString(lector.GetOrdinal("ruta_local")),
            };
        }
    }
}
