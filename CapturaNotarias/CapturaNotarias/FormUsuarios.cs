using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    public class Usuario
    {
        public int Id { get; set; }
        public string? NombreCompleto { get; set; }
        public string? NombreUsuario { get; set; }
        public string? Pin { get; set; }
        public string? Turno { get; set; } // Matutino, Vespertino, Nocturno
    }

    public class DatosUsuarios
    {
        public string PinMaestro { get; set; } = "2003";
        public List<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }

    public class FormUsuarios : Form
    {
        private DataGridView dgvUsuarios = null!;
        private TextBox txtNombreCompleto = null!;
        private TextBox txtNombreUsuario = null!;
        private TextBox txtPinUsuario = null!;
        private ComboBox cmbTurno = null!;
        private Button btnAgregar = null!;
        private Button btnEliminar = null!;
        private Button btnCerrar = null!;

        public FormUsuarios()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Administración de Usuarios";
            this.Size = new System.Drawing.Size(484, 470);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            dgvUsuarios = new DataGridView()
            {
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(445, 200),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            var fontGeneral = new System.Drawing.Font("Arial Narrow", 12F, System.Drawing.FontStyle.Regular);

            Label lblNombre = new Label() { Text = "Nombre completo:", Location = new System.Drawing.Point(12, 230), AutoSize = true, Font = fontGeneral };
            txtNombreCompleto = new TextBox() { Location = new System.Drawing.Point(150, 227), Width = 250, Font = fontGeneral, CharacterCasing = CharacterCasing.Upper, BorderStyle = BorderStyle.FixedSingle };

            Label lblUsuario = new Label() { Text = "Usuario:", Location = new System.Drawing.Point(12, 265), AutoSize = true, Font = fontGeneral };
            txtNombreUsuario = new TextBox() { Location = new System.Drawing.Point(150, 262), Width = 150, Font = fontGeneral, BorderStyle = BorderStyle.FixedSingle };

            Label lblPin = new Label() { Text = "PIN:", Location = new System.Drawing.Point(12, 300), AutoSize = true, Font = fontGeneral };
            txtPinUsuario = new TextBox() { Location = new System.Drawing.Point(150, 297), Width = 80, MaxLength = 4, PasswordChar = '*', Font = fontGeneral, BorderStyle = BorderStyle.FixedSingle };
            txtPinUsuario.KeyPress += TxtPinUsuario_KeyPress;

            Label lblTurno = new Label() { Text = "Turno:", Location = new System.Drawing.Point(12, 335), AutoSize = true, Font = fontGeneral };
            cmbTurno = new ComboBox() { Location = new System.Drawing.Point(150, 332), Width = 150, Font = fontGeneral, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTurno.Items.AddRange(new string[] { "Matutino", "Vespertino", "Nocturno" });
            cmbTurno.SelectedIndex = 0;

            btnAgregar = new Button() { Text = "Agregar", Location = new System.Drawing.Point(12, 380), Size = new System.Drawing.Size(120, 35), FlatStyle = FlatStyle.Popup, Font = fontGeneral };
            btnEliminar = new Button() { Text = "Eliminar", Location = new System.Drawing.Point(150, 380), Size = new System.Drawing.Size(120, 35), FlatStyle = FlatStyle.Popup, Font = fontGeneral };
            btnCerrar = new Button() { Text = "Cerrar", Location = new System.Drawing.Point(335, 380), Size = new System.Drawing.Size(120, 35), FlatStyle = FlatStyle.Popup, Font = fontGeneral };

            btnAgregar.Click += BtnAgregar_Click;
            btnEliminar.Click += BtnEliminar_Click;
            btnCerrar.Click += BtnCerrar_Click;

            this.Controls.Add(dgvUsuarios);
            this.Controls.Add(lblNombre);
            this.Controls.Add(txtNombreCompleto);
            this.Controls.Add(lblUsuario);
            this.Controls.Add(txtNombreUsuario);
            this.Controls.Add(lblPin);
            this.Controls.Add(txtPinUsuario);
            this.Controls.Add(lblTurno);
            this.Controls.Add(cmbTurno);
            this.Controls.Add(btnAgregar);
            this.Controls.Add(btnEliminar);
            this.Controls.Add(btnCerrar);

            CargarTabla();
        }

        private void TxtPinUsuario_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back)
            {
                e.Handled = true;
            }
        }

        private DatosUsuarios CargarDatos()
        {
            string rutaUsuarios = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "usuarios.json");
            if (File.Exists(rutaUsuarios))
            {
                try
                {
                    string json = File.ReadAllText(rutaUsuarios);
                    return JsonConvert.DeserializeObject<DatosUsuarios>(json) ?? new DatosUsuarios();
                }
                catch { }
            }
            return new DatosUsuarios();
        }

        private void GuardarDatos(DatosUsuarios datos)
        {
            string rutaUsuarios = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "usuarios.json");
            try
            {
                string json = JsonConvert.SerializeObject(datos, Formatting.Indented);
                File.WriteAllText(rutaUsuarios, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar usuarios: " + ex.Message);
            }
        }

        private void CargarTabla()
        {
            var datos = CargarDatos();
            dgvUsuarios.DataSource = null;
            dgvUsuarios.Columns.Clear();

            DataTable dt = new DataTable();
            dt.Columns.Add("ID", typeof(int));
            dt.Columns.Add("Nombre Completo", typeof(string));
            dt.Columns.Add("Usuario", typeof(string));
            dt.Columns.Add("Turno", typeof(string));

            foreach (var u in datos.Usuarios)
            {
                dt.Rows.Add(u.Id, u.NombreCompleto, u.NombreUsuario, u.Turno ?? "Matutino");
            }
            dgvUsuarios.DataSource = dt;
        }

        private void BtnAgregar_Click(object? sender, EventArgs e)
        {
            string nombre = txtNombreCompleto.Text.Trim();
            string user = txtNombreUsuario.Text.Trim();
            string pin = txtPinUsuario.Text.Trim();
            string turno = cmbTurno.SelectedItem?.ToString() ?? "Matutino";

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show("Ingrese el nombre completo.");
                return;
            }
            if (string.IsNullOrWhiteSpace(user))
            {
                MessageBox.Show("Ingrese el nombre de usuario.");
                return;
            }
            if (pin.Length != 4)
            {
                MessageBox.Show("El PIN debe ser de 4 dígitos.");
                return;
            }

            var datos = CargarDatos();

            if (datos.Usuarios.Any(u => u.NombreUsuario != null && u.NombreUsuario.Equals(user, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Ese nombre de usuario ya existe.");
                return;
            }

            int nuevoId = datos.Usuarios.Count > 0 ? datos.Usuarios.Max(u => u.Id) + 1 : 1;
            datos.Usuarios.Add(new Usuario
            {
                Id = nuevoId,
                NombreCompleto = nombre,
                NombreUsuario = user,
                Pin = pin,
                Turno = turno
            });

            GuardarDatos(datos);

            txtNombreCompleto.Clear();
            txtNombreUsuario.Clear();
            txtPinUsuario.Clear();
            cmbTurno.SelectedIndex = 0;
            CargarTabla();
            MessageBox.Show("Usuario agregado correctamente.");
        }

        private void BtnEliminar_Click(object? sender, EventArgs e)
        {
            if (dgvUsuarios.SelectedRows.Count == 0)
            {
                MessageBox.Show("Seleccione un usuario de la lista.");
                return;
            }

            int id = (int)dgvUsuarios.SelectedRows[0].Cells["ID"].Value;
            string nombre = dgvUsuarios.SelectedRows[0].Cells["Nombre Completo"].Value.ToString() ?? "";

            var confirm = MessageBox.Show($"¿Eliminar al usuario '{nombre}'?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                var datos = CargarDatos();
                datos.Usuarios.RemoveAll(u => u.Id == id);
                GuardarDatos(datos);
                CargarTabla();
            }
        }

        private void BtnCerrar_Click(object? sender, EventArgs e)
        {
            this.Close();
        }
    }
}
