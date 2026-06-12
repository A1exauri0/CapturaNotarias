using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CapturaNotarias
{
    public class FormSyncParcial : Form
    {
        private DataGridView gridArchivos = null!;
        private Button btnSincronizar = null!;
        private Button btnCancelar = null!;
        private Button btnSeleccionarTodos = null!;
        private Button btnDeseleccionarTodos = null!;
        private Label lblTitulo = null!;
        private RadioButton rbPorArchivos = null!;
        private RadioButton rbPorVolumenes = null!;
        
        private readonly List<RegistroAuditoria> _registrosOriginales;
        public List<RegistroAuditoria> RegistrosSeleccionados { get; private set; } = new List<RegistroAuditoria>();

        public FormSyncParcial(List<RegistroAuditoria> registros)
        {
            _registrosOriginales = registros ?? new List<RegistroAuditoria>();
            InitializeComponent();
            LlenarGrid();
        }

        private void InitializeComponent()
        {
            this.Text = "Sincronización Selectiva";
            this.Size = new Size(620, 480);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.WhiteSmoke;

            lblTitulo = new Label()
            {
                Text = "Selecciona los elementos que deseas enviar al servidor:",
                Location = new Point(20, 12),
                Size = new Size(560, 22),
                Font = new Font("Arial", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            // Selectores de agrupación
            rbPorArchivos = new RadioButton()
            {
                Text = "Por Archivos Individuales",
                Checked = true,
                Location = new Point(20, 40),
                Size = new Size(200, 22),
                Font = new Font("Arial", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };
            rbPorVolumenes = new RadioButton()
            {
                Text = "Por Volúmenes (Agrupado)",
                Location = new Point(230, 40),
                Size = new Size(220, 22),
                Font = new Font("Arial", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };

            rbPorArchivos.CheckedChanged += (s, e) => { if (rbPorArchivos.Checked) LlenarGrid(); };
            rbPorVolumenes.CheckedChanged += (s, e) => { if (rbPorVolumenes.Checked) LlenarGrid(); };

            // Grid
            gridArchivos = new DataGridView()
            {
                Location = new Point(20, 72),
                Size = new Size(560, 278),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                GridColor = Color.FromArgb(230, 230, 230)
            };

            // Headers styling
            gridArchivos.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(79, 129, 189); // Steel Blue
            gridArchivos.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gridArchivos.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 9.5F, FontStyle.Bold);
            gridArchivos.EnableHeadersVisualStyles = false;
            gridArchivos.RowTemplate.Height = 28;

            // Columns
            var colCheck = new DataGridViewCheckBoxColumn()
            {
                Name = "colSelect",
                HeaderText = "Enviar",
                Width = 60,
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            var colArchivo = new DataGridViewTextBoxColumn()
            {
                Name = "colArchivo",
                HeaderText = "Archivo PDF",
                ReadOnly = true,
                Width = 190,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            var colNotaria = new DataGridViewTextBoxColumn()
            {
                Name = "colNotaria",
                HeaderText = "Notaría",
                ReadOnly = true,
                Width = 160,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            var colPaginas = new DataGridViewTextBoxColumn()
            {
                Name = "colPaginas",
                HeaderText = "Págs",
                ReadOnly = true,
                Width = 60,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            var colFecha = new DataGridViewTextBoxColumn()
            {
                Name = "colFecha",
                HeaderText = "Fecha / Hora",
                ReadOnly = true,
                Width = 90,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            gridArchivos.Columns.AddRange(colCheck, colArchivo, colNotaria, colPaginas, colFecha);

            // Buttons
            btnSeleccionarTodos = new Button()
            {
                Text = "☑️ Seleccionar Todos",
                Location = new Point(20, 360),
                Size = new Size(160, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Font = new Font("Arial", 9F)
            };
            btnDeseleccionarTodos = new Button()
            {
                Text = "⬜ Desmarcar Todos",
                Location = new Point(190, 360),
                Size = new Size(160, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Font = new Font("Arial", 9F)
            };

            btnSincronizar = new Button()
            {
                Text = "🚀 Sincronizar Seleccionados",
                Location = new Point(250, 400),
                Size = new Size(200, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.SteelBlue,
                ForeColor = Color.White,
                Font = new Font("Arial", 10F, FontStyle.Bold)
            };
            btnCancelar = new Button()
            {
                Text = "Cancelar",
                Location = new Point(460, 400),
                Size = new Size(120, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.LightGray,
                Font = new Font("Arial", 10F)
            };

            btnSeleccionarTodos.Click += (s, e) => SetAllChecks(true);
            btnDeseleccionarTodos.Click += (s, e) => SetAllChecks(false);
            btnSincronizar.Click += BtnSincronizar_Click;
            btnCancelar.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            // Hover styles/Aesthetic details
            btnSincronizar.FlatAppearance.BorderSize = 0;
            btnCancelar.FlatAppearance.BorderSize = 0;
            btnSeleccionarTodos.FlatAppearance.BorderColor = Color.LightGray;
            btnDeseleccionarTodos.FlatAppearance.BorderColor = Color.LightGray;

            this.Controls.Add(lblTitulo);
            this.Controls.Add(rbPorArchivos);
            this.Controls.Add(rbPorVolumenes);
            this.Controls.Add(gridArchivos);
            this.Controls.Add(btnSeleccionarTodos);
            this.Controls.Add(btnDeseleccionarTodos);
            this.Controls.Add(btnSincronizar);
            this.Controls.Add(btnCancelar);
        }

        private void LlenarGrid()
        {
            gridArchivos.Rows.Clear();

            if (rbPorArchivos.Checked)
            {
                // Configurar columnas para archivos individuales
                gridArchivos.Columns["colArchivo"].Visible = true;
                gridArchivos.Columns["colNotaria"].HeaderText = "Notaría";
                gridArchivos.Columns["colPaginas"].HeaderText = "Págs";
                gridArchivos.Columns["colFecha"].HeaderText = "Fecha / Hora";

                foreach (var reg in _registrosOriginales)
                {
                    string notariaMostrada = ObtenerNotariaVolumen(reg);
                    gridArchivos.Rows.Add(true, reg.ArchivoOriginal, notariaMostrada, reg.Paginas, reg.FechaHora);
                }
            }
            else
            {
                // Configurar columnas para volúmenes agrupados
                gridArchivos.Columns["colArchivo"].Visible = false;
                gridArchivos.Columns["colNotaria"].HeaderText = "Notaría - Volumen";
                gridArchivos.Columns["colPaginas"].HeaderText = "Total Págs";
                gridArchivos.Columns["colFecha"].HeaderText = "Archivos";

                var grupos = ObtenerGruposVolumenes();
                foreach (var g in grupos)
                {
                    gridArchivos.Rows.Add(true, "", g.NotariaVolumen, g.TotalPaginas, g.CantidadArchivos + " archivos");
                }
            }
        }

        private void SetAllChecks(bool check)
        {
            foreach (DataGridViewRow row in gridArchivos.Rows)
            {
                row.Cells["colSelect"].Value = check;
            }
        }

        private void BtnSincronizar_Click(object? sender, EventArgs e)
        {
            RegistrosSeleccionados.Clear();

            if (rbPorArchivos.Checked)
            {
                for (int i = 0; i < gridArchivos.Rows.Count; i++)
                {
                    var row = gridArchivos.Rows[i];
                    bool isChecked = Convert.ToBoolean(row.Cells["colSelect"].Value);
                    if (isChecked)
                    {
                        RegistrosSeleccionados.Add(_registrosOriginales[i]);
                    }
                }
            }
            else
            {
                var grupos = ObtenerGruposVolumenes();
                for (int i = 0; i < gridArchivos.Rows.Count; i++)
                {
                    var row = gridArchivos.Rows[i];
                    bool isChecked = Convert.ToBoolean(row.Cells["colSelect"].Value);
                    if (isChecked)
                    {
                        string notariaVolumen = row.Cells["colNotaria"].Value?.ToString() ?? "";
                        var grupo = grupos.FirstOrDefault(g => g.NotariaVolumen == notariaVolumen);
                        if (grupo != null)
                        {
                            RegistrosSeleccionados.AddRange(grupo.Registros);
                        }
                    }
                }
            }

            if (RegistrosSeleccionados.Count == 0)
            {
                MessageBox.Show("Por favor, selecciona al menos un elemento para sincronizar.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        public class GrupoVolumen
        {
            public string NotariaVolumen { get; set; } = "";
            public List<RegistroAuditoria> Registros { get; set; } = new List<RegistroAuditoria>();
            public int CantidadArchivos => Registros.Count;
            public int TotalPaginas => Registros.Sum(r => r.Paginas);
        }

        private List<GrupoVolumen> ObtenerGruposVolumenes()
        {
            var grupos = new Dictionary<string, GrupoVolumen>();
            foreach (var reg in _registrosOriginales)
            {
                string key = ObtenerNotariaVolumen(reg);
                if (!grupos.ContainsKey(key))
                {
                    grupos[key] = new GrupoVolumen { NotariaVolumen = key };
                }
                grupos[key].Registros.Add(reg);
            }
            return grupos.Values.ToList();
        }

        private string ObtenerNotariaVolumen(RegistroAuditoria reg)
        {
            string notariaMostrada = reg.Notaria ?? "General";

            if (reg.Detalles != null && reg.Detalles.StartsWith("PDF Escaneado en ", StringComparison.OrdinalIgnoreCase))
            {
                string ruta = reg.Detalles.Substring("PDF Escaneado en ".Length);
                string[] segmentos = ruta.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

                string volumenStr = "";
                string notariaStr = "";

                for (int i = 0; i < segmentos.Length; i++)
                {
                    if (segmentos[i].StartsWith("VOLUMEN", StringComparison.OrdinalIgnoreCase))
                    {
                        volumenStr = segmentos[i];
                        if (i > 0 && !segmentos[i - 1].Equals("NOTARIAS", StringComparison.OrdinalIgnoreCase) && segmentos[i - 1].Length > 2 && !segmentos[i - 1].Contains(":"))
                        {
                            notariaStr = segmentos[i - 1];
                        }
                    }
                }

                if (!string.IsNullOrEmpty(volumenStr))
                {
                    if (!string.IsNullOrEmpty(notariaStr))
                    {
                        return notariaStr + " - " + volumenStr;
                    }
                    return (string.IsNullOrEmpty(notariaMostrada) || notariaMostrada.Equals("NOTARIAS", StringComparison.OrdinalIgnoreCase) ? "" : notariaMostrada + " - ") + volumenStr;
                }
            }
            return notariaMostrada;
        }
    }
}
