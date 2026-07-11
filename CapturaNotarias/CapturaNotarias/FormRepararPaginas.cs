using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    public class FormRepararPaginas : Form
    {
        private TextBox txtPin;
        private ComboBox cboPC;
        private Button btnAceptar;
        private Button btnCancelar;

        public string PcAFiltrar { get; private set; } = "";

        public FormRepararPaginas()
        {
            this.Text = "Reparar Páginas de Capturas";
            this.Size = new Size(380, 260);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var fuenteGeneral = new Font("Arial", 11F, FontStyle.Regular);
            var fuenteNegrita = new Font("Arial", 11F, FontStyle.Bold);

            // PIN Maestro
            Label lblPin = new Label()
            {
                Text = "PIN Maestro:",
                Font = fuenteNegrita,
                Location = new Point(30, 30),
                Size = new Size(130, 25)
            };
            txtPin = new TextBox()
            {
                Font = fuenteGeneral,
                PasswordChar = '*',
                MaxLength = 8,
                Location = new Point(170, 27),
                Size = new Size(170, 25)
            };

            // ComboBox de PCs
            Label lblPC = new Label()
            {
                Text = "PC a reparar:",
                Font = fuenteNegrita,
                Location = new Point(30, 80),
                Size = new Size(130, 25)
            };
            cboPC = new ComboBox()
            {
                Font = fuenteGeneral,
                DropDownStyle = ComboBoxStyle.DropDown,
                Location = new Point(170, 77),
                Size = new Size(170, 25)
            };

            // Botón de Aceptar
            btnAceptar = new Button()
            {
                Text = "🔄 Reparar",
                Font = fuenteNegrita,
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(50, 150),
                Size = new Size(120, 35)
            };
            btnAceptar.Click += BtnAceptar_Click;

            // Botón de Cancelar
            btnCancelar = new Button()
            {
                Text = "Cancelar",
                Font = fuenteGeneral,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(200, 150),
                Size = new Size(120, 35)
            };
            btnCancelar.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(lblPin);
            this.Controls.Add(txtPin);
            this.Controls.Add(lblPC);
            this.Controls.Add(cboPC);
            this.Controls.Add(btnAceptar);
            this.Controls.Add(btnCancelar);

            this.AcceptButton = btnAceptar;
            this.CancelButton = btnCancelar;

            // Cargar PCs
            CargarPcs();
        }

        private void CargarPcs()
        {
            cboPC.Items.Clear();
            cboPC.Items.Add("Todas");

            try
            {
                var pcs = RepositorioAuditoria.ObtenerPcsUnicas();
                foreach (var pc in pcs)
                {
                    cboPC.Items.Add(pc);
                }
            }
            catch { }

            // Seleccionar por defecto la PC local si existe en la lista, sino "Todas"
            string pcLocal = string.IsNullOrEmpty(ModuloConfiguracion.NombrePC)
                ? Environment.MachineName : ModuloConfiguracion.NombrePC;

            if (cboPC.Items.Contains(pcLocal))
            {
                cboPC.SelectedItem = pcLocal;
            }
            else
            {
                cboPC.SelectedIndex = 0;
            }
        }

        private async void BtnAceptar_Click(object? sender, EventArgs e)
        {
            string pinIngresado = txtPin.Text.Trim();
            if (string.IsNullOrEmpty(pinIngresado))
            {
                MessageBox.Show("Por favor, ingrese el PIN Maestro para continuar.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Obtener el PIN Maestro desde la API HTTP
            string pinMaestroCorrecto = await ServicioUsuarios.ObtenerPinMaestroAsync().ConfigureAwait(true);

            if (pinIngresado != pinMaestroCorrecto)
            {
                MessageBox.Show("PIN Maestro incorrecto.", "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (cboPC.SelectedItem == null && string.IsNullOrEmpty(cboPC.Text))
            {
                MessageBox.Show("Por favor, seleccione o ingrese una PC válida para reparar.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            PcAFiltrar = cboPC.SelectedItem != null ? cboPC.SelectedItem.ToString()! : cboPC.Text.Trim();

            // Formatear PC si es numérica
            if (PcAFiltrar != "Todas" && !PcAFiltrar.StartsWith("PC-", StringComparison.OrdinalIgnoreCase))
            {
                int num;
                if (int.TryParse(PcAFiltrar, out num))
                {
                    PcAFiltrar = "PC-" + num.ToString("D2");
                }
                else
                {
                    PcAFiltrar = "PC-" + PcAFiltrar;
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
