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

        public FormCaptura()
        {
            configLocal = ModuloConfiguracion.CargarConfiguracion();
            InitializeComponent();
            ConfigurarWatcher(configLocal.UltimaRutaVigilada);
        }

        private void InitializeComponent()
        {
            this.Text = "Captura de PDFs";
            this.Size = new Size(300, 180);
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.StartPosition = FormStartPosition.Manual;
            // Ubicar en la esquina inferior derecha
            Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 800, 600);
            this.Location = new Point(workingArea.Width - this.Width - 10, workingArea.Height - this.Height - 10);
            this.TopMost = true; // Siempre visible
            this.BackColor = Color.WhiteSmoke;

            lblUsuario = new Label() { Text = "Usuario: " + ModuloConfiguracion.UsuarioActual, AutoSize = true, Location = new Point(10, 10), Font = new Font("Arial", 9, FontStyle.Bold) };
            lblRuta = new Label() { Text = "Vigilando: Ninguna ruta", AutoSize = false, Location = new Point(10, 35), Size = new Size(260, 30), Font = new Font("Arial", 8), ForeColor = Color.DimGray };
            lblContador = new Label() { Text = "Capturados hoy: 0", AutoSize = true, Location = new Point(10, 70), Font = new Font("Arial", 10, FontStyle.Bold), ForeColor = Color.MediumSeaGreen };

            btnCambiarRuta = new Button() { Text = "📂 Elegir Carpeta", Location = new Point(10, 100), Size = new Size(120, 30), FlatStyle = FlatStyle.Flat };
            btnCerrarSesion = new Button() { Text = "Cerrar Sesión", Location = new Point(140, 100), Size = new Size(130, 30), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Firebrick };

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

            // Extraer posible nombre de notaria de la ruta (Ej. C:\NOTARIAS\NOTARIA 71\...)
            string[] partes = ruta.Split(Path.DirectorySeparatorChar);
            if (partes.Length >= 2)
            {
                // Un intento básico de extraer "NOTARIA X"
                foreach (var p in partes)
                {
                    if (p.ToUpper().Contains("NOTARIA"))
                    {
                        notariaActual = p;
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
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            watcher.Filter = "*.pdf";
            watcher.Created += Watcher_Created;
            watcher.EnableRaisingEvents = true;
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            // Este evento ocurre en un hilo en segundo plano
            this.Invoke((MethodInvoker)delegate
            {
                contadorSesion++;
                lblContador.Text = "Capturados hoy: " + contadorSesion;
            });

            // Registrar en la auditoría asíncronamente
            ModuloAuditoria.RegistrarAccion(notariaActual, "Capturado", e.Name ?? "Desconocido", "PDF Escaneado en " + e.FullPath);
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && ModuloConfiguracion.UsuarioActual != "")
            {
                // Prevenir cierre accidental de la ventana por la 'X' si no han cerrado sesión
                e.Cancel = true;
                MessageBox.Show("Por favor, usa el botón 'Cerrar Sesión' para salir.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            base.OnFormClosing(e);
        }
    }
}
