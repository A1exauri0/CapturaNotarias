using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CapturaNotarias
{
    public class FormConsultaProductividad : Form
    {
        private ComboBox cboUsuario;
        private DateTimePicker dtpFecha;
        private Button btnConsultar;
        private Label lblResultadoPdfs;
        private Label lblResultadoImagenes;

        public FormConsultaProductividad()
        {
            this.Text = "Consulta de Productividad";
            this.Size = new Size(420, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var fuenteGeneral = new Font("Arial", 11F, FontStyle.Regular);
            var fuenteNegrita = new Font("Arial", 11F, FontStyle.Bold);
            var fuenteTitulo = new Font("Arial", 14F, FontStyle.Bold);
            var fuenteResultado = new Font("Arial", 13F, FontStyle.Bold);

            // Título
            Label lblTitulo = new Label()
            {
                Text = "Consultar Productividad",
                Font = fuenteTitulo,
                Location = new Point(20, 20),
                Size = new Size(360, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Campo de búsqueda de usuario (Select / ComboBox)
            Label lblUsuario = new Label()
            {
                Text = "Seleccionar Usuario:",
                Font = fuenteNegrita,
                Location = new Point(30, 70),
                Size = new Size(150, 25)
            };
            cboUsuario = new ComboBox()
            {
                Font = fuenteGeneral,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(190, 67),
                Size = new Size(180, 25)
            };

            // Campo de selección de fecha
            Label lblFecha = new Label()
            {
                Text = "Seleccionar Fecha:",
                Font = fuenteNegrita,
                Location = new Point(30, 115),
                Size = new Size(150, 25)
            };
            dtpFecha = new DateTimePicker()
            {
                Font = fuenteGeneral,
                Format = DateTimePickerFormat.Short,
                Location = new Point(190, 112),
                Size = new Size(180, 25)
            };

            // Botón de Consultar
            btnConsultar = new Button()
            {
                Text = "🔍 Consultar",
                Font = fuenteNegrita,
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(30, 160),
                Size = new Size(340, 35)
            };
            btnConsultar.Click += BtnConsultar_Click;

            // Grupo de resultados
            GroupBox gboResultados = new GroupBox()
            {
                Text = "Resultados",
                Font = fuenteNegrita,
                Location = new Point(30, 210),
                Size = new Size(340, 90)
            };

            Label lblPdfs = new Label()
            {
                Text = "PDFs Trabajados:",
                Font = fuenteGeneral,
                Location = new Point(15, 30),
                Size = new Size(150, 25)
            };
            lblResultadoPdfs = new Label()
            {
                Text = "0",
                Font = fuenteResultado,
                ForeColor = Color.Blue,
                Location = new Point(180, 28),
                Size = new Size(140, 25),
                TextAlign = ContentAlignment.MiddleRight
            };

            Label lblImagenes = new Label()
            {
                Text = "Imágenes (Páginas):",
                Font = fuenteGeneral,
                Location = new Point(15, 60),
                Size = new Size(150, 25)
            };
            lblResultadoImagenes = new Label()
            {
                Text = "0",
                Font = fuenteResultado,
                ForeColor = Color.DarkGreen,
                Location = new Point(180, 58),
                Size = new Size(140, 25),
                TextAlign = ContentAlignment.MiddleRight
            };

            gboResultados.Controls.Add(lblPdfs);
            gboResultados.Controls.Add(lblResultadoPdfs);
            gboResultados.Controls.Add(lblImagenes);
            gboResultados.Controls.Add(lblResultadoImagenes);

            this.Controls.Add(lblTitulo);
            this.Controls.Add(lblUsuario);
            this.Controls.Add(cboUsuario);
            this.Controls.Add(lblFecha);
            this.Controls.Add(dtpFecha);
            this.Controls.Add(btnConsultar);
            this.Controls.Add(gboResultados);

            this.AcceptButton = btnConsultar;

            // Cargar usuarios al inicializar la ventana
            CargarUsuarios();
        }

        private void CargarUsuarios()
        {
            cboUsuario.Items.Clear();
            var listaNombres = new System.Collections.Generic.List<string>();

            // 2. Cargar/Complementar desde la base de datos local SQLite (los nombres completos reales)
            try
            {
                var usuariosBd = RepositorioAuditoria.ObtenerUsuariosUnicos();
                foreach (var u in usuariosBd)
                {
                    string nombre = u.Trim();
                    if (!string.IsNullOrEmpty(nombre) && !listaNombres.Contains(nombre))
                    {
                        listaNombres.Add(nombre);
                    }
                }
            }
            catch { }

            // Ordenar alfabéticamente de forma ascendente
            listaNombres.Sort(StringComparer.CurrentCultureIgnoreCase);

            // Agregar al ComboBox
            foreach (var nombre in listaNombres)
            {
                cboUsuario.Items.Add(nombre);
            }

            if (cboUsuario.Items.Count > 0)
            {
                cboUsuario.SelectedIndex = 0;
            }
            else
            {
                cboUsuario.Items.Add("Sin usuarios registrados");
                cboUsuario.SelectedIndex = 0;
            }
        }

        private void BtnConsultar_Click(object? sender, EventArgs e)
        {
            if (cboUsuario.SelectedItem == null || cboUsuario.SelectedItem.ToString() == "Sin usuarios registrados")
            {
                MessageBox.Show("No hay usuarios válidos seleccionados para consultar.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string busqueda = cboUsuario.SelectedItem.ToString()!;
            string fechaSeleccionada = dtpFecha.Value.ToString("yyyy-MM-dd");

            // Realizar la consulta a SQLite local
            var (pdfs, imagenes) = RepositorioAuditoria.ConsultarProductividadUsuario(busqueda, fechaSeleccionada);

            lblResultadoPdfs.Text = pdfs.ToString("N0");
            lblResultadoImagenes.Text = imagenes.ToString("N0");

            if (pdfs == 0)
            {
                MessageBox.Show("No se encontraron registros de captura para el usuario seleccionado en esta fecha.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
