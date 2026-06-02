using System;
using System.Windows.Forms;

namespace CapturaNotarias
{
    public class FormLugarTrabajo : Form
    {
        private ComboBox cmbLugarTrabajo = null!;
        private Button btnGuardar = null!;

        public FormLugarTrabajo()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Lugar de Trabajo";
            this.Size = new System.Drawing.Size(420, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;

            var fontGeneral = new System.Drawing.Font("Arial", 11F, System.Drawing.FontStyle.Regular);

            Label lblInstruccion = new Label()
            {
                Text = "Seleccione el lugar de trabajo para este equipo:",
                Location = new System.Drawing.Point(20, 18),
                Size = new System.Drawing.Size(360, 25),
                Font = fontGeneral
            };

            cmbLugarTrabajo = new ComboBox()
            {
                Location = new System.Drawing.Point(20, 48),
                Width = 360,
                Font = fontGeneral,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbLugarTrabajo.Items.Add("5 de mayo");
            cmbLugarTrabajo.Items.Add("IREC");
            
            // Cargar valor actual si existe
            ConfiguracionApp config = ModuloConfiguracion.CargarConfiguracion();
            if (!string.IsNullOrEmpty(config.LugarTrabajo))
            {
                cmbLugarTrabajo.SelectedItem = config.LugarTrabajo;
            }
            else
            {
                cmbLugarTrabajo.SelectedIndex = 0; // Seleccionar "5 de mayo" por defecto
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
            this.Controls.Add(cmbLugarTrabajo);
            this.Controls.Add(btnGuardar);

            this.AcceptButton = btnGuardar;
        }

        private void BtnGuardar_Click(object? sender, EventArgs e)
        {
            string lugar = cmbLugarTrabajo.SelectedItem?.ToString() ?? "";

            if (string.IsNullOrEmpty(lugar))
            {
                MessageBox.Show("Por favor, seleccione un lugar de trabajo.", "Campo Requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbLugarTrabajo.Focus();
                return;
            }

            try
            {
                ConfiguracionApp config = ModuloConfiguracion.CargarConfiguracion();
                config.LugarTrabajo = lugar;
                ModuloConfiguracion.GuardarConfiguracion(config);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al establecer el lugar de trabajo: " + ex.Message, "Error de Configuración", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
