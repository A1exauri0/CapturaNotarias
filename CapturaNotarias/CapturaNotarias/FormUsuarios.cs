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
        private Button btnEditarUsuario = null!;
        private Button btnIntercambiar = null!;

        public FormUsuarios()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Administración de Usuarios";
            this.Size = new System.Drawing.Size(514, 470);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            dgvUsuarios = new DataGridView()
            {
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(475, 200),
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

            btnAgregar = new Button() { Text = "Agregar", Location = new System.Drawing.Point(12, 380), Size = new System.Drawing.Size(110, 35), FlatStyle = FlatStyle.Popup, Font = fontGeneral };
            btnEliminar = new Button() { Text = "Eliminar", Location = new System.Drawing.Point(132, 380), Size = new System.Drawing.Size(110, 35), FlatStyle = FlatStyle.Popup, Font = fontGeneral };
            btnEditarUsuario = new Button() { Text = "Editar Usuario", Location = new System.Drawing.Point(252, 380), Size = new System.Drawing.Size(110, 35), FlatStyle = FlatStyle.Popup, Font = fontGeneral };
            btnIntercambiar = new Button() { Text = "Cambiar Turnos", Location = new System.Drawing.Point(372, 380), Size = new System.Drawing.Size(115, 35), FlatStyle = FlatStyle.Popup, Font = fontGeneral };

            btnAgregar.Click += BtnAgregar_Click;
            btnEliminar.Click += BtnEliminar_Click;
            btnEditarUsuario.Click += BtnEditarUsuario_Click;
            btnIntercambiar.Click += BtnIntercambiar_Click;

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
            this.Controls.Add(btnEditarUsuario);
            this.Controls.Add(btnIntercambiar);

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

        private void BtnIntercambiar_Click(object? sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Esta acción cambiará el turno de TODOS los usuarios (de Matutino a Vespertino y viceversa).\n¿Está seguro de que desea continuar?",
                "Confirmar Cambio de Turnos",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (confirm == DialogResult.Yes)
            {
                var datos = CargarDatos();
                bool huboCambios = false;

                foreach (var usuario in datos.Usuarios)
                {
                    string turnoActual = usuario.Turno ?? "Matutino";

                    if (turnoActual.Equals("Matutino", StringComparison.OrdinalIgnoreCase))
                    {
                        usuario.Turno = "Vespertino";
                        huboCambios = true;
                    }
                    else if (turnoActual.Equals("Vespertino", StringComparison.OrdinalIgnoreCase))
                    {
                        usuario.Turno = "Matutino";
                        huboCambios = true;
                    }
                }

                if (huboCambios)
                {
                    GuardarDatos(datos);
                    CargarTabla();
                    MessageBox.Show("Se han intercambiado los turnos de todos los usuarios.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No se encontraron usuarios con turno Matutino o Vespertino para intercambiar.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnEditarUsuario_Click(object? sender, EventArgs e)
        {
            if (dgvUsuarios.SelectedRows.Count == 0)
            {
                MessageBox.Show("Seleccione un usuario de la lista.");
                return;
            }

            int id = (int)dgvUsuarios.SelectedRows[0].Cells["ID"].Value;
            var datos = CargarDatos();
            var usuario = datos.Usuarios.FirstOrDefault(u => u.Id == id);

            if (usuario == null) return;

            using (var formEditar = new FormEditarUsuario(usuario))
            {
                if (formEditar.ShowDialog() == DialogResult.OK)
                {
                    string nuevoUser = formEditar.NombreUsuario;
                    if (datos.Usuarios.Any(u => u.Id != id && u.NombreUsuario != null && u.NombreUsuario.Equals(nuevoUser, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show("Ese nombre de usuario ya existe en otro registro.");
                        return;
                    }

                    usuario.NombreCompleto = formEditar.NombreCompleto;
                    usuario.NombreUsuario = nuevoUser;
                    usuario.Pin = formEditar.Pin;
                    usuario.Turno = formEditar.Turno;

                    GuardarDatos(datos);
                    CargarTabla();
                    MessageBox.Show("Usuario actualizado correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }

    public class FormEditarUsuario : Form
    {
        private TextBox txtNombreCompleto = null!;
        private TextBox txtNombreUsuario = null!;
        private TextBox txtPinUsuario = null!;
        private ComboBox cmbTurno = null!;
        private Button btnGuardar = null!;
        private Button btnCancelar = null!;

        public string NombreCompleto => txtNombreCompleto.Text.Trim();
        public string NombreUsuario => txtNombreUsuario.Text.Trim();
        public string Pin => txtPinUsuario.Text.Trim();
        public string Turno => cmbTurno.SelectedItem?.ToString() ?? "Matutino";

        public FormEditarUsuario(Usuario usuario)
        {
            InitializeComponent(usuario);
        }

        private void InitializeComponent(Usuario usuario)
        {
            this.Text = "Editar Usuario";
            this.Size = new System.Drawing.Size(400, 320);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var fontGeneral = new System.Drawing.Font("Arial Narrow", 12F, System.Drawing.FontStyle.Regular);

            Label lblNombre = new Label() { Text = "Nombre completo:", Location = new System.Drawing.Point(12, 20), AutoSize = true, Font = fontGeneral };
            txtNombreCompleto = new TextBox() { Text = usuario.NombreCompleto, Location = new System.Drawing.Point(150, 17), Width = 220, Font = fontGeneral, CharacterCasing = CharacterCasing.Upper, BorderStyle = BorderStyle.FixedSingle };

            Label lblUsuario = new Label() { Text = "Usuario:", Location = new System.Drawing.Point(12, 60), AutoSize = true, Font = fontGeneral };
            txtNombreUsuario = new TextBox() { Text = usuario.NombreUsuario, Location = new System.Drawing.Point(150, 57), Width = 150, Font = fontGeneral, BorderStyle = BorderStyle.FixedSingle };

            Label lblPin = new Label() { Text = "PIN:", Location = new System.Drawing.Point(12, 100), AutoSize = true, Font = fontGeneral };
            txtPinUsuario = new TextBox() { Text = usuario.Pin, Location = new System.Drawing.Point(150, 97), Width = 80, MaxLength = 4, PasswordChar = '*', Font = fontGeneral, BorderStyle = BorderStyle.FixedSingle };
            txtPinUsuario.KeyPress += (s, e) => {
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back)
                    e.Handled = true;
            };

            Label lblTurno = new Label() { Text = "Turno:", Location = new System.Drawing.Point(12, 140), AutoSize = true, Font = fontGeneral };
            cmbTurno = new ComboBox() { Location = new System.Drawing.Point(150, 137), Width = 150, Font = fontGeneral, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTurno.Items.AddRange(new string[] { "Matutino", "Vespertino", "Nocturno" });
            cmbTurno.SelectedItem = usuario.Turno ?? "Matutino";

            btnGuardar = new Button() { Text = "Guardar", Location = new System.Drawing.Point(120, 200), Size = new System.Drawing.Size(110, 35), FlatStyle = FlatStyle.Popup, Font = fontGeneral, DialogResult = DialogResult.OK };
            btnCancelar = new Button() { Text = "Cancelar", Location = new System.Drawing.Point(240, 200), Size = new System.Drawing.Size(110, 35), FlatStyle = FlatStyle.Popup, Font = fontGeneral, DialogResult = DialogResult.Cancel };

            btnGuardar.Click += BtnGuardar_Click;

            this.Controls.Add(lblNombre);
            this.Controls.Add(txtNombreCompleto);
            this.Controls.Add(lblUsuario);
            this.Controls.Add(txtNombreUsuario);
            this.Controls.Add(lblPin);
            this.Controls.Add(txtPinUsuario);
            this.Controls.Add(lblTurno);
            this.Controls.Add(cmbTurno);
            this.Controls.Add(btnGuardar);
            this.Controls.Add(btnCancelar);

            this.AcceptButton = btnGuardar;
            this.CancelButton = btnCancelar;
        }

        private void BtnGuardar_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NombreCompleto))
            {
                MessageBox.Show("Ingrese el nombre completo.");
                this.DialogResult = DialogResult.None;
                return;
            }
            if (string.IsNullOrWhiteSpace(NombreUsuario))
            {
                MessageBox.Show("Ingrese el nombre de usuario.");
                this.DialogResult = DialogResult.None;
                return;
            }
            if (Pin.Length != 4)
            {
                MessageBox.Show("El PIN debe ser de 4 dígitos.");
                this.DialogResult = DialogResult.None;
                return;
            }
        }
    }
}
