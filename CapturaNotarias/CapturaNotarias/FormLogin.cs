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

        public FormLogin()
        {
            this.Text = "Captura Notarias";
            this.Size = new Size(360, 260);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            Label lblTitle = new Label() { Text = "Captura Notarias", Font = new Font("Arial", 14, FontStyle.Bold), Location = new Point(20, 18), AutoSize = true };
            
            Label lblUser = new Label() { Text = "Usuario:", Location = new Point(40, 70), AutoSize = true };
            txtUsername = new TextBox() { Location = new Point(120, 67), Width = 150 };
            
            Label lblPin = new Label() { Text = "PIN:", Location = new Point(40, 110), AutoSize = true };
            txtPin = new TextBox() { Location = new Point(120, 107), Width = 100, PasswordChar = '*' };

            btnLogin = new Button() { Text = "Iniciar Sesión", Location = new Point(120, 150), Width = 150, BackColor = Color.LightBlue, FlatStyle = FlatStyle.Flat };
            
            btnConfig = new Button() { Text = "⚙ Opciones", Location = new Point(245, 15), Width = 85, Height = 28, FlatStyle = FlatStyle.Flat };

            // Configurar el menú desplegable (ContextMenuStrip) en el botón de engranaje
            menuConfig = new ContextMenuStrip();
            ToolStripMenuItem itemConfigServidor = new ToolStripMenuItem("⚙ Configurar Servidor...");
            ToolStripMenuItem itemAdminUsuarios = new ToolStripMenuItem("👥 Administrar Usuarios...");
            ToolStripMenuItem itemAuditoria = new ToolStripMenuItem("📊 Ver Productividad y Auditoría...");
            ToolStripMenuItem itemExcel = new ToolStripMenuItem("📊 Descargar Reporte Excel...");
            ToolStripMenuItem itemEnviarArchivos = new ToolStripMenuItem("📤 Enviar Auditorías a Servidor Central...");
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
            Label lblAdvertencia = new Label() 
            { 
                Text = "⚠️ Si cierra la aplicación, no se enviarán datos al servidor.", 
                AutoSize = true, 
                Location = new Point(20, 190), 
                Font = new Font("Arial", 8f, FontStyle.Italic), 
                ForeColor = Color.Firebrick 
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

        private void InicializarUsuariosJson()
        {
            try
            {
                if (string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria)) return;

                if (!Directory.Exists(ModuloConfiguracion.RutaServidorAuditoria))
                {
                    Directory.CreateDirectory(ModuloConfiguracion.RutaServidorAuditoria);
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
            Form prompt = new Form() { Width = 500, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Configuración de Red", StartPosition = FormStartPosition.CenterScreen };
            Label textLabel = new Label() { Left = 20, Top = 20, Width = 400, Text = "Ruta del Servidor Local (Donde están usuarios.json):" };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 400, Text = ModuloConfiguracion.RutaServidorAuditoria };
            Button confirmation = new Button() { Text = "Guardar", Left = 320, Width = 100, Top = 80, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            if (prompt.ShowDialog() == DialogResult.OK)
            {
                ModuloConfiguracion.RutaServidorAuditoria = textBox.Text;
                ConfiguracionApp conf = ModuloConfiguracion.CargarConfiguracion();
                conf.RutaServidorAuditoria = textBox.Text;
                ModuloConfiguracion.GuardarConfiguracion(conf);
                
                // Intentar autogenerar el archivo usuarios.json al guardar la ruta
                InicializarUsuariosJson();
                
                MessageBox.Show("Ruta guardada y archivo de usuarios verificado: " + textBox.Text);
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
            // Mostrar advertencia si el usuario está cerrando la aplicación manualmente
            if (e.CloseReason == CloseReason.UserClosing)
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
