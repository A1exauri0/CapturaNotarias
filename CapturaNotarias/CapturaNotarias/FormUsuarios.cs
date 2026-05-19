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
            this.Size = new System.Drawing.Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            dgvUsuarios = new DataGridView()
            {
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(320, 310),
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            Label lblNombre = new Label() { Text = "Nombre Completo:", Location = new System.Drawing.Point(360, 20), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            txtNombreCompleto = new TextBox() { Location = new System.Drawing.Point(360, 40), Width = 200 };

            Label lblUsuario = new Label() { Text = "Nombre de Usuario:", Location = new System.Drawing.Point(360, 80), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            txtNombreUsuario = new TextBox() { Location = new System.Drawing.Point(360, 100), Width = 200 };

            Label lblPin = new Label() { Text = "PIN (4 dígitos):", Location = new System.Drawing.Point(360, 140), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            txtPinUsuario = new TextBox() { Location = new System.Drawing.Point(360, 160), Width = 100, MaxLength = 4, PasswordChar = '*' };
            txtPinUsuario.KeyPress += TxtPinUsuario_KeyPress;

            btnAgregar = new Button() { Text = "➕ Agregar Usuario", Location = new System.Drawing.Point(360, 210), Width = 200, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = System.Drawing.Color.LightGreen };
            btnEliminar = new Button() { Text = "❌ Eliminar Seleccionado", Location = new System.Drawing.Point(360, 250), Width = 200, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = System.Drawing.Color.LightCoral };
            btnCerrar = new Button() { Text = "Cerrar", Location = new System.Drawing.Point(360, 300), Width = 200, Height = 30, FlatStyle = FlatStyle.Flat };

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

            foreach (var u in datos.Usuarios)
            {
                dt.Rows.Add(u.Id, u.NombreCompleto, u.NombreUsuario);
            }
            dgvUsuarios.DataSource = dt;
        }

        private void BtnAgregar_Click(object? sender, EventArgs e)
        {
            string nombre = txtNombreCompleto.Text.Trim();
            string user = txtNombreUsuario.Text.Trim();
            string pin = txtPinUsuario.Text.Trim();

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
                Pin = pin
            });

            GuardarDatos(datos);

            txtNombreCompleto.Clear();
            txtNombreUsuario.Clear();
            txtPinUsuario.Clear();
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
