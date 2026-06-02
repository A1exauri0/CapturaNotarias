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