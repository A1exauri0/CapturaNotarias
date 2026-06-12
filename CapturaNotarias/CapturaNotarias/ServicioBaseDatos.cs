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
                            ruta_local        TEXT
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
                }

                _inicializada = true;
            }
        }
    }
}
