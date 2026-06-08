using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    public class FormLogin : Form
    {
        private TextBox txtUsername;
        private TextBox txtPin;
        private Button btnLogin;
        private Button btnConfig;
        private ContextMenuStrip menuConfig;
        private Label lblAdvertencia = null!;

        public FormLogin()
        {
            // Cargar configuración al iniciar para conocer el estado de ActivarEnvioAuditoria
            ModuloConfiguracion.CargarConfiguracion();

            this.Text = "Captura Notarias";
            this.Size = new Size(480, 340);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var fontGeneral = new Font("Arial", 11.5F, FontStyle.Regular);
            var fontBold = new Font("Arial", 11.5F, FontStyle.Bold);

            Label lblTitle = new Label() { Text = "Captura Notarias", Font = new Font("Arial", 16, FontStyle.Bold), Location = new Point(30, 25), AutoSize = true };
            
            Label lblUser = new Label() { Text = "Usuario:", Location = new Point(50, 95), AutoSize = true, Font = fontBold };
            txtUsername = new TextBox() { Location = new Point(160, 92), Width = 220, Font = fontGeneral };
            
            Label lblPin = new Label() { Text = "PIN:", Location = new Point(50, 145), AutoSize = true, Font = fontBold };
            txtPin = new TextBox() { Location = new Point(160, 142), Width = 150, PasswordChar = '*', Font = fontGeneral };

            btnLogin = new Button() { Text = "Iniciar Sesión", Location = new Point(160, 195), Width = 220, Height = 40, BackColor = Color.LightBlue, FlatStyle = FlatStyle.Flat, Font = fontBold };
            
            btnConfig = new Button() { Text = "⚙ Opciones", Location = new Point(345, 23), Width = 100, Height = 35, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10.5F) };

            // Configurar el menú desplegable (ContextMenuStrip) en el botón de engranaje
            menuConfig = new ContextMenuStrip();
            ToolStripMenuItem itemConfigServidor = new ToolStripMenuItem("⚙ Configuración...");
            ToolStripMenuItem itemAdminUsuarios = new ToolStripMenuItem("👥 Administrar Usuarios...");
            ToolStripMenuItem itemAuditoria = new ToolStripMenuItem("📊 Ver Productividad y Auditoría...");
            ToolStripMenuItem itemExcel = new ToolStripMenuItem("📊 Descargar Reporte Excel...");
            ToolStripMenuItem itemEnviarArchivos = new ToolStripMenuItem("📤 Enviar Auditorías  y Archivos a Servidor Central...");
            ToolStripMenuItem itemDiagnostico = new ToolStripMenuItem("🔍 Diagnóstico de Conexión de PCs...");
            ToolStripMenuItem itemLugarTrabajo = new ToolStripMenuItem("📍 Cambiar Lugar de Trabajo...");
            
            itemConfigServidor.Click += BtnConfig_Click;
            itemAdminUsuarios.Click += BtnUsuarios_Click;
            itemAuditoria.Click += BtnAuditoria_Click;
            itemExcel.Click += ItemExcel_Click;
            itemEnviarArchivos.Click += ItemEnviarArchivos_Click;
            itemDiagnostico.Click += ItemDiagnostico_Click;
            itemLugarTrabajo.Click += ItemLugarTrabajo_Click;
            
            menuConfig.Items.Add(itemConfigServidor);
            menuConfig.Items.Add(itemAdminUsuarios);
            menuConfig.Items.Add(itemAuditoria);
            menuConfig.Items.Add(itemExcel);
            menuConfig.Items.Add(itemEnviarArchivos);
            menuConfig.Items.Add(itemDiagnostico);
            menuConfig.Items.Add(itemLugarTrabajo);

            btnConfig.Click += (s, e) => {
                menuConfig.Show(btnConfig, new Point(0, btnConfig.Height));
            };
            btnLogin.Click += BtnLogin_Click;

            // Etiqueta de advertencia de cierre
            lblAdvertencia = new Label() 
            { 
                Text = "⚠️ Si cierra la aplicación, no se enviarán datos al servidor.", 
                AutoSize = true, 
                Location = new Point(30, 260), 
                Font = new Font("Arial", 9.5F, FontStyle.Italic), 
                ForeColor = Color.Firebrick,
                Visible = ModuloConfiguracion.ActivarEnvioAuditoria
            };

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblUser);
            this.Controls.Add(txtUsername);
            this.Controls.Add(lblPin);
            this.Controls.Add(txtPin);
            this.Controls.Add(btnLogin);
            this.Controls.Add(btnConfig);
            this.Controls.Add(lblAdvertencia);
            
            this.AcceptButton = btnLogin;
        }

        private bool ExisteDirectorioConTimeout(string ruta, int timeoutMs)
        {
            try
            {
                var tarea = System.Threading.Tasks.Task.Run(() => Directory.Exists(ruta));
                if (tarea.Wait(timeoutMs))
                {
                    return tarea.Result;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void InicializarUsuariosJson()
        {
            try
            {
                if (string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria)) return;

                // Evitar congelamiento de la interfaz si la ruta de red no está accesible
                if (!ExisteDirectorioConTimeout(ModuloConfiguracion.RutaServidorAuditoria, 1500))
                {
                    return;
                }

                string rutaUsuarios = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "usuarios.json");
                if (!File.Exists(rutaUsuarios))
                {
                    DatosUsuarios datos = new DatosUsuarios
                    {
                        PinMaestro = "2003",
                        Usuarios = new List<Usuario>
                        {
                            new Usuario
                            {
                                Id = 1,
                                NombreCompleto = "Administrador",
                                NombreUsuario = "admin",
                                Pin = "2003"
                            }
                        }
                    };
                    string json = JsonConvert.SerializeObject(datos, Formatting.Indented);
                    File.WriteAllText(rutaUsuarios, json);
                }
            }
            catch { }
        }

        private void BtnConfig_Click(object? sender, EventArgs e)
        {
            Form prompt = new Form() { Width = 500, Height = 330, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Configuración General", StartPosition = FormStartPosition.CenterScreen };
            Label textLabel = new Label() { Left = 20, Top = 20, Width = 400, Text = "Ruta del Servidor Local (Donde están usuarios.json):" };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 400, Text = ModuloConfiguracion.RutaServidorAuditoria };
            
            Label pcLabel = new Label() { Left = 20, Top = 85, Width = 150, Text = "Identificación de esta PC:" };
            TextBox pcTextBox = new TextBox() { Left = 180, Top = 82, Width = 100, Text = ModuloConfiguracion.NombrePC };

            Label lugarLabel = new Label() { Left = 20, Top = 120, Width = 150, Text = "Lugar de Trabajo:" };
            ComboBox lugarComboBox = new ComboBox() { Left = 180, Top = 117, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            lugarComboBox.Items.Add("5 de mayo - 1");
            lugarComboBox.Items.Add("5 de mayo - 2");
            lugarComboBox.Items.Add("IREC");
            lugarComboBox.Items.Add("RPP");

            if (!string.IsNullOrEmpty(ModuloConfiguracion.LugarTrabajo))
            {
                lugarComboBox.SelectedItem = ModuloConfiguracion.LugarTrabajo;
            }
            else
            {
                lugarComboBox.SelectedIndex = 0;
            }

            Label tipoLabel = new Label() { Left = 20, Top = 155, Width = 150, Text = "Tipo de Captura:" };
            ComboBox tipoComboBox = new ComboBox() { Left = 180, Top = 152, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            tipoComboBox.Items.Add("NOTARIAS");
            tipoComboBox.Items.Add("LIBROS");
            tipoComboBox.Items.Add("NOMINAS");
            tipoComboBox.SelectedItem = ModuloConfiguracion.TipoCaptura;

            tipoComboBox.SelectedIndexChanged += (s, ev) => {
                string nuevoTipo = tipoComboBox.SelectedItem?.ToString() ?? "NOTARIAS";
                string rutaActual = textBox.Text.Trim();
                
                // Si la ruta actual coincide con alguna de las predeterminadas, la actualizamos automáticamente
                if (rutaActual.Equals(@"\\192.168.1.10\NOTARIAS", StringComparison.OrdinalIgnoreCase) ||
                    rutaActual.Equals(@"\\192.168.1.10\LIBROS", StringComparison.OrdinalIgnoreCase) ||
                    rutaActual.Equals(@"\\192.168.1.10\NOMINAS", StringComparison.OrdinalIgnoreCase))
                {
                    textBox.Text = @"\\192.168.1.10\" + nuevoTipo;
                }
                else if (rutaActual.Equals(@"C:\NOTARIAS", StringComparison.OrdinalIgnoreCase) ||
                         rutaActual.Equals(@"C:\LIBROS", StringComparison.OrdinalIgnoreCase) ||
                         rutaActual.Equals(@"C:\NOMINAS", StringComparison.OrdinalIgnoreCase))
                {
                    textBox.Text = @"C:\" + nuevoTipo;
                }
            };
            
            CheckBox chkEnvio = new CheckBox() { Left = 20, Top = 195, Width = 450, Text = "Activar envío automático de auditorías al servidor central", Checked = ModuloConfiguracion.ActivarEnvioAuditoria };
            
            Button confirmation = new Button() { Text = "Guardar", Left = 340, Width = 100, Top = 240, DialogResult = DialogResult.OK };
            
            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(pcLabel);
            prompt.Controls.Add(pcTextBox);
            prompt.Controls.Add(lugarLabel);
            prompt.Controls.Add(lugarComboBox);
            prompt.Controls.Add(tipoLabel);
            prompt.Controls.Add(tipoComboBox);
            prompt.Controls.Add(chkEnvio);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            if (prompt.ShowDialog() == DialogResult.OK)
            {
                string pcNombreValido = pcTextBox.Text.Trim().ToUpper();
                if (!string.IsNullOrEmpty(pcNombreValido))
                {
                    if (!pcNombreValido.StartsWith("PC-"))
                    {
                        // Si el usuario ingresó solo el número (ej. "3" o "03"), formatearlo a PC-03
                        int num;
                        if (int.TryParse(pcNombreValido, out num))
                        {
                            pcNombreValido = "PC-" + num.ToString("D2");
                        }
                        else
                        {
                            pcNombreValido = "PC-" + pcNombreValido;
                        }
                    }
                }

                string lugarSeleccionado = lugarComboBox.SelectedItem?.ToString() ?? "";
                string tipoSeleccionado = tipoComboBox.SelectedItem?.ToString() ?? "NOTARIAS";

                ModuloConfiguracion.RutaServidorAuditoria = textBox.Text;
                ModuloConfiguracion.ActivarEnvioAuditoria = chkEnvio.Checked;
                ModuloConfiguracion.LugarTrabajo = lugarSeleccionado;
                ModuloConfiguracion.TipoCaptura = tipoSeleccionado;
                if (!string.IsNullOrEmpty(pcNombreValido))
                {
                    ModuloConfiguracion.NombrePC = pcNombreValido;
                }
                
                ConfiguracionApp conf = ModuloConfiguracion.CargarConfiguracion();
                conf.RutaServidorAuditoria = textBox.Text;
                conf.ActivarEnvioAuditoria = chkEnvio.Checked;
                conf.LugarTrabajo = lugarSeleccionado;
                conf.TipoCaptura = tipoSeleccionado;
                if (!string.IsNullOrEmpty(pcNombreValido))
                {
                    conf.NombrePC = pcNombreValido;
                }
                ModuloConfiguracion.GuardarConfiguracion(conf);
                
                // Actualizar visibilidad de la advertencia
                lblAdvertencia.Visible = ModuloConfiguracion.ActivarEnvioAuditoria;
                
                // Intentar autogenerar el archivo usuarios.json al guardar la ruta
                InicializarUsuariosJson();
                
                MessageBox.Show("Configuración guardada correctamente.");
            }
        }

        private void BtnUsuarios_Click(object? sender, EventArgs e)
        {
            // Asegurarnos de que el archivo exista en la ruta de red
            InicializarUsuariosJson();

            string pinMaestroCorrecto = "2003";
            string rutaUsuarios = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "usuarios.json");
            
            if (File.Exists(rutaUsuarios))
            {
                try
                {
                    string json = File.ReadAllText(rutaUsuarios);
                    var datos = JsonConvert.DeserializeObject<DatosUsuarios>(json);
                    if (datos != null && !string.IsNullOrEmpty(datos.PinMaestro))
                    {
                        pinMaestroCorrecto = datos.PinMaestro;
                    }
                }
                catch { }
            }

            // Solicitar el PIN maestro al usuario
            Form prompt = new Form() { Width = 300, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Acceso Administrador", StartPosition = FormStartPosition.CenterScreen };
            Label textLabel = new Label() { Left = 20, Top = 10, Width = 250, Text = "Ingrese el PIN Maestro:" };
            TextBox textBox = new TextBox() { Left = 20, Top = 35, Width = 240, PasswordChar = '*', MaxLength = 8 };
            Button confirmation = new Button() { Text = "Aceptar", Left = 160, Width = 100, Top = 70, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            if (prompt.ShowDialog() == DialogResult.OK)
            {
                if (textBox.Text == pinMaestroCorrecto)
                {
                    using (FormUsuarios frm = new FormUsuarios())
                    {
                        frm.ShowDialog();
                    }
                }
                else
                {
                    MessageBox.Show("PIN Maestro incorrecto.", "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnAuditoria_Click(object? sender, EventArgs e)
        {
            // Opcional: También podrías pedir PIN maestro aquí si no quieres que cualquiera lo vea
            using (FormAuditoria frm = new FormAuditoria())
            {
                frm.ShowDialog();
            }
        }
        private void ItemExcel_Click(object? sender, EventArgs e)
        {
            // Asegurarnos de que el archivo exista en la ruta de red
            InicializarUsuariosJson();

            string pinMaestroCorrecto = "2003";
            string rutaUsuarios = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "usuarios.json");
            
            if (File.Exists(rutaUsuarios))
            {
                try
                {
                    string json = File.ReadAllText(rutaUsuarios);
                    var datos = JsonConvert.DeserializeObject<DatosUsuarios>(json);
                    if (datos != null && !string.IsNullOrEmpty(datos.PinMaestro))
                    {
                        pinMaestroCorrecto = datos.PinMaestro;
                    }
                }
                catch { }
            }

            // Solicitar el PIN maestro al usuario
            Form prompt = new Form() { Width = 300, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Acceso Autorizado", StartPosition = FormStartPosition.CenterScreen };
            Label textLabel = new Label() { Left = 20, Top = 10, Width = 250, Text = "Ingrese el PIN Maestro:" };
            TextBox textBox = new TextBox() { Left = 20, Top = 35, Width = 240, PasswordChar = '*', MaxLength = 8 };
            Button confirmation = new Button() { Text = "Aceptar", Left = 160, Width = 100, Top = 70, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            if (prompt.ShowDialog() == DialogResult.OK)
            {
                if (textBox.Text == pinMaestroCorrecto)
                {
                    var todos = ModuloAuditoria.ObtenerRegistrosTodos();
                    ModuloAuditoria.ExportarExcel(todos);
                }
                else
                {
                    MessageBox.Show("PIN Maestro incorrecto.", "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ItemEnviarArchivos_Click(object? sender, EventArgs e)
        {
            // Solicitar el PIN maestro al usuario para autorizar la transferencia
            InicializarUsuariosJson();

            string pinMaestroCorrecto = "2003";
            string rutaUsuarios = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "usuarios.json");
            
            if (File.Exists(rutaUsuarios))
            {
                try
                {
                    string json = File.ReadAllText(rutaUsuarios);
                    var datos = JsonConvert.DeserializeObject<DatosUsuarios>(json);
                    if (datos != null && !string.IsNullOrEmpty(datos.PinMaestro))
                    {
                        pinMaestroCorrecto = datos.PinMaestro;
                    }
                }
                catch { }
            }

            Form formularioPrompt = new Form() { Width = 300, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Acceso Autorizado", StartPosition = FormStartPosition.CenterScreen };
            Label etiquetaTexto = new Label() { Left = 20, Top = 10, Width = 250, Text = "Ingrese el PIN Maestro:" };
            TextBox cajaTexto = new TextBox() { Left = 20, Top = 35, Width = 240, PasswordChar = '*', MaxLength = 8 };
            Button botonConfirmacion = new Button() { Text = "Aceptar", Left = 160, Width = 100, Top = 70, DialogResult = DialogResult.OK };
            formularioPrompt.Controls.Add(etiquetaTexto);
            formularioPrompt.Controls.Add(cajaTexto);
            formularioPrompt.Controls.Add(botonConfirmacion);
            formularioPrompt.AcceptButton = botonConfirmacion;

            if (formularioPrompt.ShowDialog() == DialogResult.OK)
            {
                if (cajaTexto.Text == pinMaestroCorrecto)
                {
                    ModuloAuditoria.EnviarAuditoriasAlServidorCentral();
                }
                else
                {
                    MessageBox.Show("PIN Maestro incorrecto.", "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ItemDiagnostico_Click(object? sender, EventArgs e)
        {
            using (FormDiagnostico frm = new FormDiagnostico())
            {
                frm.ShowDialog();
            }
        }

        private void ItemLugarTrabajo_Click(object? sender, EventArgs e)
        {
            using (FormLugarTrabajo frm = new FormLugarTrabajo())
            {
                frm.ShowDialog();
            }
        }

        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            string user = txtUsername.Text.Trim();
            string pin = txtPin.Text.Trim();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pin))
            {
                MessageBox.Show("Ingresa usuario y PIN.");
                return;
            }

            // Validar/Inicializar antes de leer
            InicializarUsuariosJson();

            string rutaUsuarios = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "usuarios.json");
            
            if (!File.Exists(rutaUsuarios))
            {
                // Modo fallback extremo
                if (user == "admin" && pin == "1234")
                {
                    ModuloConfiguracion.UsuarioActual = "admin";
                    ModuloConfiguracion.NombreCompletoActual = "Administrador Local";
                    ModuloConfiguracion.TurnoActual = "Matutino";
                    IniciarApp();
                    return;
                }
                MessageBox.Show("No se pudo crear ni encontrar el archivo usuarios.json en: " + ModuloConfiguracion.RutaServidorAuditoria);
                return;
            }

            try
            {
                string json = File.ReadAllText(rutaUsuarios);
                var datos = JsonConvert.DeserializeObject<DatosUsuarios>(json);
                if (datos != null && datos.Usuarios != null)
                {
                    foreach (var u in datos.Usuarios)
                    {
                        if (u.NombreUsuario != null && u.NombreUsuario.Equals(user, StringComparison.OrdinalIgnoreCase) && u.Pin == pin)
                        {
                            ModuloConfiguracion.UsuarioActual = u.NombreUsuario;
                            ModuloConfiguracion.NombreCompletoActual = u.NombreCompleto ?? u.NombreUsuario;
                            ModuloConfiguracion.TurnoActual = u.Turno ?? "Matutino";
                            IniciarApp();
                            return;
                        }
                    }
                }
                MessageBox.Show("Credenciales incorrectas.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error leyendo usuarios: " + ex.Message);
            }
        }

        private void IniciarApp()
        {
            this.Hide();
            using (FormCaptura frm = new FormCaptura())
            {
                frm.ShowDialog();
            }
            // Cuando cierra sesión, vuelve aquí
            txtPin.Clear();
            this.Show();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ConfiguracionApp conf = ModuloConfiguracion.CargarConfiguracion();
            if (!string.IsNullOrEmpty(conf.RutaServidorAuditoria))
            {
                ModuloConfiguracion.RutaServidorAuditoria = conf.RutaServidorAuditoria;
            }
            // Intentar inicializar al cargar la app si ya hay una ruta configurada
            InicializarUsuariosJson();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Mostrar advertencia si el usuario está cerrando la aplicación manualmente y el envío automático de auditorías está activo
            if (e.CloseReason == CloseReason.UserClosing && ModuloConfiguracion.ActivarEnvioAuditoria)
            {
                var resultado = MessageBox.Show(
                    "Si cierra la aplicación, se detendrá el monitoreo y ya no se enviarán datos de auditoría al servidor.\n\n¿Está seguro de que desea salir?",
                    "Confirmación de Salida",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (resultado == DialogResult.No)
                {
                    e.Cancel = true; // Cancelar el cierre de la ventana
                    return;
                }
            }
            base.OnFormClosing(e);
        }
    }
}
