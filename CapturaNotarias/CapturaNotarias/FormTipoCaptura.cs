using System;
using System.Windows.Forms;

namespace CapturaNotarias
{
    public class FormTipoCaptura : Form
    {
        private ComboBox cmbTipoCaptura = null!;
        private Button btnGuardar = null!;

        public FormTipoCaptura()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Tipo de Captura";
            this.Size = new System.Drawing.Size(420, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;

            var fontGeneral = new System.Drawing.Font("Arial", 11F, System.Drawing.FontStyle.Regular);

            Label lblInstruccion = new Label()
            {
                Text = "Seleccione el tipo de captura para este equipo:",
                Location = new System.Drawing.Point(20, 18),
                Size = new System.Drawing.Size(360, 25),
                Font = fontGeneral
            };

            cmbTipoCaptura = new ComboBox()
            {
                Location = new System.Drawing.Point(20, 48),
                Width = 360,
                Font = fontGeneral,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbTipoCaptura.Items.Add("NOTARIAS");
            cmbTipoCaptura.Items.Add("LIBROS");
            cmbTipoCaptura.Items.Add("NOMINAS");

            ConfiguracionApp config = ModuloConfiguracion.CargarConfiguracion();
            if (!string.IsNullOrEmpty(config.TipoCaptura))
            {
                cmbTipoCaptura.SelectedItem = config.TipoCaptura;
            }
            else
            {
                cmbTipoCaptura.SelectedIndex = 0; // Seleccionar NOTARIAS por defecto
            }

            btnGuardar = new Button()
            {
                Text = "Guardar",
                Location = new System.Drawing.Point(280, 105),
                Size = new System.Drawing.Size(100, 32),
                FlatStyle = FlatStyle.Flat
            };
            btnGuardar.Click += BtnGuardar_Click;

            this.Controls.Add(lblInstruccion);
            this.Controls.Add(cmbTipoCaptura);
            this.Controls.Add(btnGuardar);

            this.AcceptButton = btnGuardar;
        }

        private void BtnGuardar_Click(object? sender, EventArgs e)
        {
            string tipo = cmbTipoCaptura.SelectedItem?.ToString() ?? "";

            if (string.IsNullOrEmpty(tipo))
            {
                MessageBox.Show("Por favor, seleccione un tipo de captura.", "Campo Requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbTipoCaptura.Focus();
                return;
            }

            try
            {
                ConfiguracionApp config = ModuloConfiguracion.CargarConfiguracion();
                config.TipoCaptura = tipo;
                
                // Si la ruta del servidor local es la predeterminada para el tipo anterior, o está vacía, se actualiza automáticamente
                string rutaActual = config.RutaServidorAuditoria ?? "";
                if (string.IsNullOrEmpty(rutaActual) ||
                    rutaActual.Equals(@"\\192.168.1.10\NOTARIAS", StringComparison.OrdinalIgnoreCase) ||
                    rutaActual.Equals(@"\\192.168.1.10\LIBROS", StringComparison.OrdinalIgnoreCase) ||
                    rutaActual.Equals(@"\\192.168.1.10\NOMINAS", StringComparison.OrdinalIgnoreCase))
                {
                    config.RutaServidorAuditoria = @"\\192.168.1.10\" + tipo;
                }
                
                ModuloConfiguracion.GuardarConfiguracion(config);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al establecer el tipo de captura: " + ex.Message, "Error de Configuración", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
