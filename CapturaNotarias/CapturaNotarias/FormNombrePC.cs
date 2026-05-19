using System;
using System.IO;
using System.Windows.Forms;

namespace CapturaNotarias
{
    public class FormNombrePC : Form
    {
        private TextBox txtNombrePC = null!;
        private Button btnGuardar = null!;

        public FormNombrePC()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Identificación de Equipo";
            this.Size = new System.Drawing.Size(320, 160);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;

            var fontGeneral = new System.Drawing.Font("Arial", 10F, System.Drawing.FontStyle.Regular);

            Label lblInstruccion = new Label()
            {
                Text = "Ingrese el NÚMERO asignado a esta PC:",
                Location = new System.Drawing.Point(15, 12),
                Size = new System.Drawing.Size(290, 20),
                Font = fontGeneral
            };

            txtNombrePC = new TextBox()
            {
                Location = new System.Drawing.Point(15, 42),
                Width = 270,
                Font = fontGeneral
            };
            txtNombrePC.KeyPress += TxtNombrePC_KeyPress;

            btnGuardar = new Button()
            {
                Text = "Guardar",
                Location = new System.Drawing.Point(185, 78),
                Size = new System.Drawing.Size(100, 30),
                FlatStyle = FlatStyle.Flat
            };
            btnGuardar.Click += BtnGuardar_Click;

            this.Controls.Add(lblInstruccion);
            this.Controls.Add(txtNombrePC);
            this.Controls.Add(btnGuardar);

            this.AcceptButton = btnGuardar;
        }

        private void TxtNombrePC_KeyPress(object? sender, KeyPressEventArgs e)
        {
            // Solo permitir números y tecla de retroceso (BackSpace)
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back)
            {
                e.Handled = true;
            }
        }

        private void BtnGuardar_Click(object? sender, EventArgs e)
        {
            string nombre = txtNombrePC.Text.Trim();

            if (string.IsNullOrEmpty(nombre))
            {
                MessageBox.Show("Por favor, ingrese un número para identificar esta PC.", "Campo Requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNombrePC.Focus();
                return;
            }

            // Limpiar caracteres no válidos de nombre de archivo por seguridad
            foreach (char car in Path.GetInvalidFileNameChars())
            {
                nombre = nombre.Replace(car, '_');
            }

            try
            {
                // Formatear a dos dígitos (ej: 1 -> PC-01, 12 -> PC-12) para que se mantenga ordenado
                int num;
                string nombreFinal;
                if (int.TryParse(nombre, out num))
                {
                    nombreFinal = "PC-" + num.ToString("D2");
                }
                else
                {
                    nombreFinal = "PC-" + nombre;
                }

                // Cargar config actual y asignarlo
                ConfiguracionApp config = ModuloConfiguracion.CargarConfiguracion();
                config.NombrePC = nombreFinal;

                // Si ya existe la ruta en el servidor (en la carpeta compartida)
                if (!string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria))
                {
                    string rutaMonitoreo = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "MonitoreoCaptura", nombreFinal);
                    if (Directory.Exists(rutaMonitoreo))
                    {
                        MessageBox.Show($"El número de equipo '{nombreFinal}' ya está registrado en el servidor.\nPor favor, asigne un número diferente para evitar duplicados.", "Identidad Duplicada", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        txtNombrePC.Focus();
                        return;
                    }

                    // Crear la carpeta del equipo de una vez
                    Directory.CreateDirectory(rutaMonitoreo);
                }

                ModuloConfiguracion.GuardarConfiguracion(config);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al establecer el nombre de la PC: " + ex.Message, "Error de Configuración", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
