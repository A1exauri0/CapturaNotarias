namespace CapturaNotarias
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Cargar configuración local al arrancar
            ConfiguracionApp config = ModuloConfiguracion.CargarConfiguracion();

            // Inicializar base de datos SQLite (crea tablas si no existen)
            ServicioBaseDatos.InicializarBd();

            // Reparar registros con Mojibake en la base de datos SQLite si los hay
            ServicioBaseDatos.RepararMojibakeBd();

            // Migrar datos JSON existentes a SQLite (solo la primera vez)
            MigradorJsonASqlite.EjecutarSiNecesario();

            // Iniciar el worker de conteo de páginas en background
            ServicioContadorPaginas.Iniciar();

            // Si esta PC está configurada como servidor, iniciar el receptor HTTP
            if (ModuloConfiguracion.EsServidor)
            {
                ServidorHttpLocal.Iniciar();
            }

            // Si no se ha asignado tipo de captura a esta PC, abrir el formulario para pedirlo
            if (string.IsNullOrEmpty(config.TipoCaptura))
            {
                using (FormTipoCaptura frm = new FormTipoCaptura())
                {
                    if (frm.ShowDialog() != DialogResult.OK)
                    {
                        return; // Salir del programa si no seleccionó el tipo de captura
                    }
                }
                // Recargar configuración tras guardar el tipo de captura
                config = ModuloConfiguracion.CargarConfiguracion();
            }

            // Si no se ha asignado número a esta PC, abrir el formulario para pedirlo
            if (string.IsNullOrEmpty(config.NombrePC))
            {
                using (FormNombrePC frm = new FormNombrePC())
                {
                    if (frm.ShowDialog() != DialogResult.OK)
                    {
                        return; // Salir del programa si no ingresó el nombre del PC
                    }
                }
                // Recargar configuración tras guardar el nombre de la PC
                config = ModuloConfiguracion.CargarConfiguracion();
            }

            // Si no se ha asignado lugar de trabajo a esta PC, abrir el formulario para pedirlo
            if (string.IsNullOrEmpty(config.LugarTrabajo))
            {
                using (FormLugarTrabajo frm = new FormLugarTrabajo())
                {
                    if (frm.ShowDialog() != DialogResult.OK)
                    {
                        return; // Salir del programa si no seleccionó el lugar de trabajo
                    }
                }
            }

            Application.Run(new FormLogin());
        }
    }
}