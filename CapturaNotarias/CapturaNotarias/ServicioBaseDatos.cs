using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace CapturaNotarias
{
    /// <summary>
    /// Servicio centralizado para manejar la base de datos SQLite local.
    /// Reemplaza los archivos JSON como almacenamiento principal.
    /// </summary>
    public static class ServicioBaseDatos
    {
        private static string? _rutaBd;
        private static bool _inicializada = false;
        private static readonly object _lockInicio = new object();

        /// <summary>
        /// Obtiene la ruta al archivo .db en AppData del usuario.
        /// </summary>
        public static string ObtenerRutaBd()
        {
            if (_rutaBd == null)
            {
                string carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CapturaNotarias");
                if (!Directory.Exists(carpeta))
                    Directory.CreateDirectory(carpeta);
                _rutaBd = Path.Combine(carpeta, "captura_notarias.db");
            }
            return _rutaBd;
        }

        /// <summary>
        /// Crea y retorna una conexión abierta a la base de datos con WAL habilitado.
        /// El llamador es responsable de cerrarla con using.
        /// </summary>
        public static SqliteConnection ObtenerConexion()
        {
            string cadena = $"Data Source={ObtenerRutaBd()};Mode=ReadWriteCreate;Cache=Shared";
            var conexion = new SqliteConnection(cadena);
            conexion.Open();

            // WAL permite lecturas concurrentes mientras se escribe
            // busy_timeout evita errores "database is locked" si hay concurrencia
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
                cmd.ExecuteNonQuery();
            }

            return conexion;
        }

        /// <summary>
        /// Crea las tablas e índices si no existen. Se ejecuta una sola vez al iniciar la app.
        /// </summary>
        public static void InicializarBd()
        {
            if (_inicializada) return;

            lock (_lockInicio)
            {
                if (_inicializada) return;

                using (var conexion = ObtenerConexion())
                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS registros_auditoria (
                            id                INTEGER PRIMARY KEY AUTOINCREMENT,
                            fecha_hora        TEXT NOT NULL,
                            fecha             TEXT NOT NULL,
                            usuario           TEXT,
                            nombre_completo   TEXT,
                            turno             TEXT,
                            pc                TEXT COLLATE NOCASE,
                            ip                TEXT,
                            notaria           TEXT,
                            accion            TEXT DEFAULT 'Capturado',
                            archivo_original  TEXT COLLATE NOCASE,
                            detalles          TEXT,
                            paginas           INTEGER DEFAULT 0,
                            lugar_trabajo     TEXT,
                            enviado           INTEGER DEFAULT 0,
                            exportado_red     INTEGER DEFAULT 0,
                            ruta_local        TEXT,
                            exportado_en      TEXT
                        );

                        CREATE INDEX IF NOT EXISTS idx_fecha 
                            ON registros_auditoria(fecha);
                        CREATE INDEX IF NOT EXISTS idx_pc_usuario_fecha 
                            ON registros_auditoria(pc, usuario, fecha);
                        CREATE INDEX IF NOT EXISTS idx_enviado 
                            ON registros_auditoria(enviado);
                        CREATE INDEX IF NOT EXISTS idx_exportado_red 
                            ON registros_auditoria(exportado_red);
                        CREATE INDEX IF NOT EXISTS idx_paginas_pendientes
                            ON registros_auditoria(paginas) WHERE paginas = 0;
                        CREATE UNIQUE INDEX IF NOT EXISTS idx_unico_captura 
                            ON registros_auditoria(archivo_original, pc, fecha)
                            WHERE accion = 'Capturado';
                    ";
                    cmd.ExecuteNonQuery();

                    // Intentar agregar la columna exportado_en si la base de datos ya existía previamente
                    try
                    {
                        using (var cmdAlter = conexion.CreateCommand())
                        {
                            cmdAlter.CommandText = "ALTER TABLE registros_auditoria ADD COLUMN exportado_en TEXT;";
                            cmdAlter.ExecuteNonQuery();
                        }
                    }
                    catch { }

                    // Rellenar retroactivamente exportado_en usando la fecha_hora para registros ya exportados históricamente
                    try
                    {
                        using (var cmdUpdate = conexion.CreateCommand())
                        {
                            cmdUpdate.CommandText = "UPDATE registros_auditoria SET exportado_en = fecha_hora WHERE exportado_red = 1 AND (exportado_en IS NULL OR exportado_en = '');";
                            cmdUpdate.ExecuteNonQuery();
                        }
                    }
                    catch { }
                }

                _inicializada = true;
            }
        }

        /// <summary>
        /// Escanea y limpia registros con Mojibake de la base de datos local.
        /// </summary>
        public static void RepararMojibakeBd()
        {
            try
            {
                using (var conexion = ObtenerConexion())
                {
                    // 1. Obtener los IDs de registros que contengan 'Ã' o '¢' en campos clave
                    var idsAReparar = new System.Collections.Generic.List<long>();
                    using (var cmd = conexion.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT id FROM registros_auditoria 
                            WHERE usuario LIKE '%Ã%' OR usuario LIKE '%¢%'
                               OR nombre_completo LIKE '%Ã%' OR nombre_completo LIKE '%¢%'
                               OR notaria LIKE '%Ã%' OR notaria LIKE '%¢%'
                               OR archivo_original LIKE '%Ã%' OR archivo_original LIKE '%¢%'
                               OR detalles LIKE '%Ã%' OR detalles LIKE '%¢%'
                               OR lugar_trabajo LIKE '%Ã%' OR lugar_trabajo LIKE '%¢%'
                               OR ruta_local LIKE '%Ã%' OR ruta_local LIKE '%¢%'";
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                idsAReparar.Add(reader.GetInt64(0));
                            }
                        }
                    }

                    if (idsAReparar.Count == 0) return;

                    // 2. Cargar, limpiar y actualizar cada registro en una transacción
                    using (var transaccion = conexion.BeginTransaction())
                    {
                        using (var cmdSelect = conexion.CreateCommand())
                        {
                            cmdSelect.CommandText = "SELECT usuario, nombre_completo, notaria, archivo_original, detalles, lugar_trabajo, ruta_local FROM registros_auditoria WHERE id = @id";
                            var paramIdSel = cmdSelect.CreateParameter();
                            paramIdSel.ParameterName = "@id";
                            cmdSelect.Parameters.Add(paramIdSel);

                            using (var cmdUpdate = conexion.CreateCommand())
                            {
                                cmdUpdate.CommandText = @"
                                    UPDATE registros_auditoria SET 
                                        usuario = @usuario,
                                        nombre_completo = @nombre_completo,
                                        notaria = @notaria,
                                        archivo_original = @archivo_original,
                                        detalles = @detalles,
                                        lugar_trabajo = @lugar_trabajo,
                                        ruta_local = @ruta_local
                                    WHERE id = @id";

                                var pUsuario = cmdUpdate.CreateParameter(); pUsuario.ParameterName = "@usuario"; cmdUpdate.Parameters.Add(pUsuario);
                                var pNombre = cmdUpdate.CreateParameter(); pNombre.ParameterName = "@nombre_completo"; cmdUpdate.Parameters.Add(pNombre);
                                var pNotaria = cmdUpdate.CreateParameter(); pNotaria.ParameterName = "@notaria"; cmdUpdate.Parameters.Add(pNotaria);
                                var pArchivo = cmdUpdate.CreateParameter(); pArchivo.ParameterName = "@archivo_original"; cmdUpdate.Parameters.Add(pArchivo);
                                var pDetalles = cmdUpdate.CreateParameter(); pDetalles.ParameterName = "@detalles"; cmdUpdate.Parameters.Add(pDetalles);
                                var pLugar = cmdUpdate.CreateParameter(); pLugar.ParameterName = "@lugar_trabajo"; cmdUpdate.Parameters.Add(pLugar);
                                var pRuta = cmdUpdate.CreateParameter(); pRuta.ParameterName = "@ruta_local"; cmdUpdate.Parameters.Add(pRuta);
                                var pId = cmdUpdate.CreateParameter(); pId.ParameterName = "@id"; cmdUpdate.Parameters.Add(pId);

                                foreach (long id in idsAReparar)
                                {
                                    paramIdSel.Value = id;
                                    string usuario = "", nombre = "", notaria = "", archivo = "", detalles = "", lugar = "", ruta = "";

                                    using (var reader = cmdSelect.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            usuario = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                            nombre = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                            notaria = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                            archivo = reader.IsDBNull(3) ? "" : reader.GetString(3);
                                            detalles = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                            lugar = reader.IsDBNull(5) ? "" : reader.GetString(5);
                                            ruta = reader.IsDBNull(6) ? "" : reader.GetString(6);
                                        }
                                    }

                                    pUsuario.Value = ModuloAuditoria.LimpiarMojibake(usuario) ?? (object)DBNull.Value;
                                    pNombre.Value = ModuloAuditoria.LimpiarMojibake(nombre) ?? (object)DBNull.Value;
                                    pNotaria.Value = ModuloAuditoria.LimpiarMojibake(notaria) ?? (object)DBNull.Value;
                                    pArchivo.Value = ModuloAuditoria.LimpiarMojibake(archivo) ?? (object)DBNull.Value;
                                    pDetalles.Value = ModuloAuditoria.LimpiarMojibake(detalles) ?? (object)DBNull.Value;
                                    pLugar.Value = ModuloAuditoria.LimpiarMojibake(lugar) ?? (object)DBNull.Value;
                                    pRuta.Value = ModuloAuditoria.LimpiarMojibake(ruta) ?? (object)DBNull.Value;
                                    pId.Value = id;

                                    cmdUpdate.ExecuteNonQuery();
                                }
                            }
                        }
                        transaccion.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al reparar base de datos: " + ex.Message);
            }
        }
    }
}
