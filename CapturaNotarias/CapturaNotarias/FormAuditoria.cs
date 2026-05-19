using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    public class FormAuditoria : Form
    {
        private DataGridView dgvAuditoria = null!;
        private TextBox txtBusqueda = null!;
        private ComboBox cmbUsuario = null!;
        private ComboBox cmbNotaria = null!;
        private DateTimePicker dtpFecha = null!;
        private CheckBox chkUsarFecha = null!;
        private Button btnLimpiar = null!;
        private Button btnExcel = null!;
        private Label lblTotal = null!;
        private Label lblProductividad = null!;
        
        private List<RegistroAuditoria> listaOriginal = new List<RegistroAuditoria>();
        private BindingSource bindingSource = new BindingSource();
        private bool isInitializing = true;

        public FormAuditoria()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Visor de Auditoría y Productividad";
            this.Size = new Size(984, 561);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowIcon = false;
            
            var fontTitulo = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold);
            var fontInput = new Font("Segoe UI", 9.75F, FontStyle.Regular);

            Label lblBusqueda = new Label() { Text = "Buscar por texto:", Location = new Point(12, 25), AutoSize = true, Font = fontTitulo };
            txtBusqueda = new TextBox() { Location = new Point(12, 45), Size = new Size(160, 25), Font = fontInput };
            txtBusqueda.TextChanged += TxtBusqueda_TextChanged;

            Label lblFiltros = new Label() { Text = "Otros filtros:", Location = new Point(180, 25), AutoSize = true, Font = fontTitulo };
            
            cmbUsuario = new ComboBox() { Location = new Point(180, 45), Size = new Size(180, 25), Font = fontInput, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbUsuario.SelectedIndexChanged += CmbUsuario_SelectedIndexChanged;

            chkUsarFecha = new CheckBox() { Text = "Filtrar Fecha", Location = new Point(375, 27), AutoSize = true };
            chkUsarFecha.CheckedChanged += ChkUsarFecha_CheckedChanged;
            
            dtpFecha = new DateTimePicker() { Location = new Point(375, 45), Size = new Size(110, 25), Font = fontInput, Format = DateTimePickerFormat.Short };
            dtpFecha.ValueChanged += DtpFecha_ValueChanged;

            cmbNotaria = new ComboBox() { Location = new Point(500, 45), Size = new Size(160, 25), Font = fontInput, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbNotaria.SelectedIndexChanged += CmbNotaria_SelectedIndexChanged;

            btnLimpiar = new Button() { Text = "🧹", Location = new Point(675, 44), Size = new Size(52, 27) };
            btnLimpiar.Click += BtnLimpiar_Click;

            btnExcel = new Button() { Text = "📊 Exportar Excel", Location = new Point(740, 44), Size = new Size(130, 27), Font = fontInput };
            btnExcel.Click += BtnExcel_Click;

            dgvAuditoria = new DataGridView()
            {
                Location = new Point(12, 85),
                Size = new Size(940, 390),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false
            };
            dgvAuditoria.DefaultCellStyle.SelectionBackColor = Color.SteelBlue;

            lblTotal = new Label() { Text = "Total registros: 0", Location = new Point(12, 490), AutoSize = true, Font = fontTitulo, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            
            lblProductividad = new Label() { 
                Text = "Productividad de Usuarios: 0", 
                Location = new Point(200, 485), 
                Size = new Size(750, 25), 
                Font = fontTitulo, 
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            this.Controls.Add(lblBusqueda);
            this.Controls.Add(txtBusqueda);
            this.Controls.Add(lblFiltros);
            this.Controls.Add(cmbUsuario);
            this.Controls.Add(chkUsarFecha);
            this.Controls.Add(dtpFecha);
            this.Controls.Add(cmbNotaria);
            this.Controls.Add(btnLimpiar);
            this.Controls.Add(btnExcel);
            this.Controls.Add(dgvAuditoria);
            this.Controls.Add(lblTotal);
            this.Controls.Add(lblProductividad);

            this.Load += FormAuditoria_Load;
        }

        private void FormAuditoria_Load(object? sender, EventArgs e)
        {
            isInitializing = true;
            CargarDatos();
            ConfigurarDgv();
            LlenarCombos();
            isInitializing = false;
            AplicarFiltros();
        }

        private void CargarDatos()
        {
            listaOriginal = ModuloAuditoria.ObtenerRegistrosTodos();
            listaOriginal.Reverse(); // Lo más reciente primero
            bindingSource.DataSource = listaOriginal;
            dgvAuditoria.DataSource = bindingSource;
            lblTotal.Text = "Total registros: " + listaOriginal.Count;
        }

        private void ConfigurarDgv()
        {
            if (dgvAuditoria.Columns.Count == 0) return;

            if (dgvAuditoria.Columns.Contains("FechaHora")) dgvAuditoria.Columns["FechaHora"].HeaderText = "Fecha y Hora";
            if (dgvAuditoria.Columns.Contains("NombreCompleto")) dgvAuditoria.Columns["NombreCompleto"].HeaderText = "Usuario";
            if (dgvAuditoria.Columns.Contains("Turno")) dgvAuditoria.Columns["Turno"].HeaderText = "Turno";
            if (dgvAuditoria.Columns.Contains("ArchivoOriginal")) dgvAuditoria.Columns["ArchivoOriginal"].HeaderText = "Archivo";
            if (dgvAuditoria.Columns.Contains("Notaria")) dgvAuditoria.Columns["Notaria"].HeaderText = "Notaría";
            if (dgvAuditoria.Columns.Contains("PC")) dgvAuditoria.Columns["PC"].HeaderText = "Equipo";
            if (dgvAuditoria.Columns.Contains("IP")) dgvAuditoria.Columns["IP"].HeaderText = "Dirección IP";
            if (dgvAuditoria.Columns.Contains("Paginas")) dgvAuditoria.Columns["Paginas"].HeaderText = "Páginas";
            
            if (dgvAuditoria.Columns.Contains("Usuario")) dgvAuditoria.Columns["Usuario"].Visible = false; // Ocultar el username corto
            if (dgvAuditoria.Columns.Contains("Accion")) dgvAuditoria.Columns["Accion"].Visible = false; // Ocultar Acción ya que solo capturan

            dgvAuditoria.AutoResizeColumns();
        }

        private void LlenarCombos()
        {
            var usuarios = listaOriginal.Where(r => !string.IsNullOrEmpty(r.NombreCompleto)).Select(r => r.NombreCompleto).Distinct().OrderBy(s => s).ToList();
            cmbUsuario.Items.Clear();
            cmbUsuario.Items.Add("--- Usuarios ---");
            foreach (var u in usuarios) if (u != null) cmbUsuario.Items.Add(u);
            cmbUsuario.SelectedIndex = 0;

            var notarias = listaOriginal.Where(r => !string.IsNullOrEmpty(r.Notaria)).Select(r => r.Notaria).Distinct().OrderBy(s => s).ToList();
            cmbNotaria.Items.Clear();
            cmbNotaria.Items.Add("--- Notarías ---");
            foreach (var n in notarias) if (n != null) cmbNotaria.Items.Add(n);
            cmbNotaria.SelectedIndex = 0;
        }

        private void AplicarFiltros()
        {
            if (isInitializing || listaOriginal == null) return;

            string textoBusqueda = txtBusqueda.Text.ToLower().Trim();
            string usuarioSel = cmbUsuario.SelectedIndex > 0 ? cmbUsuario.SelectedItem?.ToString() ?? "" : "";
            string notariaSel = cmbNotaria.SelectedIndex > 0 ? cmbNotaria.SelectedItem?.ToString() ?? "" : "";
            string fechaFiltro = dtpFecha.Value.ToString("yyyy-MM-dd");

            var filtrada = listaOriginal.Where(r => 
            {
                bool cumpleTexto = string.IsNullOrEmpty(textoBusqueda) ||
                    (r.NombreCompleto != null && r.NombreCompleto.ToLower().Contains(textoBusqueda)) ||
                    (r.ArchivoOriginal != null && r.ArchivoOriginal.ToLower().Contains(textoBusqueda)) ||
                    (r.Notaria != null && r.Notaria.ToLower().Contains(textoBusqueda)) ||
                    (r.IP != null && r.IP.ToLower().Contains(textoBusqueda)) ||
                    (r.Detalles != null && r.Detalles.ToLower().Contains(textoBusqueda));

                bool cumpleUsuario = string.IsNullOrEmpty(usuarioSel) || r.NombreCompleto == usuarioSel;
                bool cumpleNotaria = string.IsNullOrEmpty(notariaSel) || r.Notaria == notariaSel;
                bool cumpleFecha = !chkUsarFecha.Checked || (r.FechaHora != null && r.FechaHora.StartsWith(fechaFiltro));

                return cumpleTexto && cumpleUsuario && cumpleNotaria && cumpleFecha;
            }).ToList();

            bindingSource.DataSource = filtrada;
            lblTotal.Text = "Resultados: " + filtrada.Count;

            // Calcular productividad por usuario basada en el filtro actual
            var agrupados = filtrada.Where(r => !string.IsNullOrEmpty(r.NombreCompleto))
                                    .GroupBy(r => r.NombreCompleto)
                                    .Select(g => $"{g.Key}: {g.Count()} capturas")
                                    .ToList();

            if (agrupados.Count > 0)
            {
                lblProductividad.Text = "Productividad: " + string.Join(" | ", agrupados);
                lblProductividad.ForeColor = Color.MediumSeaGreen;
            }
            else
            {
                lblProductividad.Text = "Productividad: 0 capturas";
                lblProductividad.ForeColor = Color.Black;
            }

            ConfigurarDgv();
        }

        private void TxtBusqueda_TextChanged(object? sender, EventArgs e) => AplicarFiltros();
        private void CmbUsuario_SelectedIndexChanged(object? sender, EventArgs e) => AplicarFiltros();
        private void CmbNotaria_SelectedIndexChanged(object? sender, EventArgs e) => AplicarFiltros();
        private void DtpFecha_ValueChanged(object? sender, EventArgs e)
        {
            if (chkUsarFecha.Checked) AplicarFiltros();
        }
        private void ChkUsarFecha_CheckedChanged(object? sender, EventArgs e) => AplicarFiltros();

        private void BtnLimpiar_Click(object? sender, EventArgs e)
        {
            txtBusqueda.Clear();
            cmbUsuario.SelectedIndex = 0;
            cmbNotaria.SelectedIndex = 0;
            chkUsarFecha.Checked = false;
            dtpFecha.Value = DateTime.Now;
            AplicarFiltros();
        }

        private void BtnExcel_Click(object? sender, EventArgs e)
        {
            var datos = bindingSource.DataSource as List<RegistroAuditoria>;
            if (datos != null && datos.Count > 0)
            {
                ModuloAuditoria.ExportarExcel(datos);
            }
            else
            {
                MessageBox.Show("No hay datos para exportar.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
