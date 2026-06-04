using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CapturaNotarias
{
    public class FormCaptura : Form
    {
        private Label lblUsuario = null!;
        private Label lblRuta = null!;
        private Label lblContador = null!;
        private Button btnCambiarRuta = null!;
        private Button btnCerrarSesion = null!;
        private FileSystemWatcher? watcher;
        private int contadorSesion = 0;
        private ConfiguracionApp configLocal;
        private string notariaActual = "General"; // Podría extraerse de la ruta
        private System.Windows.Forms.Timer? temporizadorSincronizacion;

        public FormCaptura()
        {
            configLocal = ModuloConfiguracion.CargarConfiguracion();
            InitializeComponent();
            ConfigurarWatcher(configLocal.UltimaRutaVigilada);
            IniciarTemporizadorSincronizacion();
        }

        private void InitializeComponent()
        {
            this.Text = "Captura de PDFs";
            this.Size = new Size(420, 240);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MinimizeBox = true;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.Manual;
            // Ubicar en la esquina inferior derecha
            Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 800, 600);
            this.Location = new Point(workingArea.Width - this.Width - 15, workingArea.Height - this.Height - 15);
            this.TopMost = true; // Siempre visible
            this.BackColor = Color.WhiteSmoke;

            lblUsuario = new Label() { Text = "Usuario: " + ModuloConfiguracion.UsuarioActual, AutoSize = true, Location = new Point(20, 20), Font = new Font("Arial", 11, FontStyle.Bold) };
            lblRuta = new Label() { Text = "Vigilando: Ninguna ruta", AutoSize = false, Location = new Point(20, 50), Size = new Size(380, 45), Font = new Font("Arial", 9.5F), ForeColor = Color.DimGray };
            lblContador = new Label() { Text = "Capturados hoy: 0", AutoSize = true, Location = new Point(20, 105), Font = new Font("Arial", 13, FontStyle.Bold), ForeColor = Color.MediumSeaGreen };

            btnCambiarRuta = new Button() { Text = "📂 Elegir Carpeta", Location = new Point(20, 145), Size = new Size(170, 38), FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10.5F) };
            btnCerrarSesion = new Button() { Text = "Cerrar Sesión", Location = new Point(210, 145), Size = new Size(170, 38), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Firebrick, Font = new Font("Arial", 10.5F) };

            btnCambiarRuta.Click += BtnCambiarRuta_Click;
            btnCerrarSesion.Click += BtnCerrarSesion_Click;

            this.Controls.Add(lblUsuario);
            this.Controls.Add(lblRuta);
            this.Controls.Add(lblContador);
            this.Controls.Add(btnCambiarRuta);
            this.Controls.Add(btnCerrarSesion);
        }

        private void BtnCambiarRuta_Click(object? sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Selecciona la carpeta donde el escáner guardará los PDFs";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    ConfigurarWatcher(fbd.SelectedPath);
                }
            }
        }

        private void ConfigurarWatcher(string? ruta)
        {
            if (string.IsNullOrEmpty(ruta) || !Directory.Exists(ruta)) return;

            // Extraer posible nombre de notaria de la ruta y conservar subcarpetas (ej. NOTARIA 76\VOLUMEN 26)
            string[] partes = ruta.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 2)
            {
                for (int i = 0; i < partes.Length; i++)
                {
                    string p = partes[i];
                    string pUpper = p.ToUpper();
                    if (pUpper.Contains("NOTARIA") && pUpper != "NOTARIAS")
                    {
                        // Obtener esta carpeta y todas las subcarpetas subsecuentes
                        var subpartes = new System.Collections.Generic.List<string>();
                        for (int j = i; j < partes.Length; j++)
                        {
                            subpartes.Add(partes[j]);
                        }
                        notariaActual = string.Join(Path.DirectorySeparatorChar.ToString(), subpartes);
                        break;
                    }
                }
            }

            lblRuta.Text = "Vigilando: " + ruta;
            configLocal.UltimaRutaVigilada = ruta;
            ModuloConfiguracion.GuardarConfiguracion(configLocal);

            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            watcher = new FileSystemWatcher(ruta);
            watcher.InternalBufferSize = 65536; // Aumentar buffer a 64KB para evitar pérdida de eventos
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            watcher.Filter = "*.pdf";
            watcher.Created += Watcher_Created;
            watcher.Renamed += Watcher_Renamed;
            watcher.EnableRaisingEvents = true;
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            Watcher_Created(sender, e);
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            // Este evento ocurre en un hilo en segundo plano
            string archivo = e.Name ?? "Desconocido";

            // Mostrar retroalimentación inmediata al usuario en la UI
            this.Invoke((MethodInvoker)delegate
            {
                lblRuta.Text = "Procesando: " + archivo + "... (Contando páginas)";
                lblRuta.ForeColor = Color.DarkOrange;
            });

            string resultado = ModuloAuditoria.RegistrarAccion(notariaActual, archivo, e.FullPath, "PDF Escaneado en " + e.FullPath);

            this.Invoke((MethodInvoker)delegate
            {
                // Restaurar la ruta vigilada
                lblRuta.Text = "Vigilando: " + configLocal.UltimaRutaVigilada;
                lblRuta.ForeColor = Color.DimGray;

                if (resultado == "OK")
                {
                    contadorSesion++;
                    lblContador.Text = "Capturados hoy: " + contadorSesion;
                }
                else if (resultado == "PC_MISMATCH")
                {
                    MessageBox.Show(
                        $"Se detectó el archivo '{archivo}', pero NO se registró en tu cuenta porque el nombre del archivo no coincide con esta PC ({ModuloConfiguracion.NombrePC ?? "Sin nombre"}).\n\n" +
                        "Verifica que el nombre de la PC configurado o el prefijo de archivo asignado en tu escáner coincidan.",
                        "Archivo No Registrado (PC No Coincide)",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
                else if (resultado == "NO_USER")
                {
                    MessageBox.Show(
                        $"Se detectó el archivo '{archivo}', pero no hay ningún usuario con sesión activa en esta aplicación.",
                        "Sin Sesión Activa",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
                else if (resultado == "ERROR")
                {
                    MessageBox.Show(
                        $"Se detectó el archivo '{archivo}', pero ocurrió un error al intentar guardar el registro de auditoría en la red.",
                        "Error al Registrar Auditoría",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            });
        }

        private void BtnCerrarSesion_Click(object? sender, EventArgs e)
        {
            if (watcher != null) watcher.EnableRaisingEvents = false;
            ModuloConfiguracion.UsuarioActual = "";
            ModuloConfiguracion.NombreCompletoActual = "";
            
            // Volver al login
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void IniciarTemporizadorSincronizacion()
        {
            temporizadorSincronizacion = new System.Windows.Forms.Timer();
            // 1 hora = 60 minutos * 60 segundos * 1000 milisegundos = 3,600,000 milisegundos
            temporizadorSincronizacion.Interval = 3600000;
            temporizadorSincronizacion.Tick += TemporizadorSincronizacion_Tick;
            temporizadorSincronizacion.Start();

            // Ejecutar una primera sincronización silenciosa en segundo plano al iniciar
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    ModuloAuditoria.EnviarAuditoriasAlServidorCentral(true);
                }
                catch { }
            });
        }

        private void TemporizadorSincronizacion_Tick(object? sender, EventArgs e)
        {
            // Ejecutar en segundo plano para no congelar la interfaz de usuario
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    ModuloAuditoria.EnviarAuditoriasAlServidorCentral(true);
                }
                catch { }
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Detener y liberar el temporizador al cerrar el formulario
            if (temporizadorSincronizacion != null)
            {
                temporizadorSincronizacion.Stop();
                temporizadorSincronizacion.Dispose();
            }

            // Apagar el watcher al cerrar
            if (watcher != null) watcher.EnableRaisingEvents = false;

            // Limpiar la sesión para obligar a iniciar sesión nuevamente
            ModuloConfiguracion.UsuarioActual = "";
            ModuloConfiguracion.NombreCompletoActual = "";

            base.OnFormClosing(e);
        }
    }
}
