using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private System.Windows.Forms.Timer? temporizadorSincronizacionGlobal;
        private int ultimaHoraSincronizada = -1;

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
            ToolStripMenuItem itemEnviarRegistros = new ToolStripMenuItem("🌐 Enviar Registros de Auditoría a Astronmx...");
            ToolStripMenuItem itemEnviarPDFs = new ToolStripMenuItem("📁 Transferir Archivos PDF al Servidor Central...");
            ToolStripMenuItem itemLugarTrabajo = new ToolStripMenuItem("📍 Cambiar Lugar de Trabajo...");
            ToolStripMenuItem itemMigrarHistoricos = new ToolStripMenuItem("🔄 Importar/Verificar JSONs a Base de Datos...");
            ToolStripMenuItem itemConsultaProductividad = new ToolStripMenuItem("🔎 Consultar Productividad por Usuario...");
            ToolStripMenuItem itemRepararPaginas = new ToolStripMenuItem("🔄 Reparar Páginas de Capturas");
            
            itemConfigServidor.Click += BtnConfig_Click;
            itemAdminUsuarios.Click += BtnUsuarios_Click;
            itemAuditoria.Click += BtnAuditoria_Click;
            itemExcel.Click += ItemExcel_Click;
            itemEnviarRegistros.Click += ItemEnviarRegistros_Click;
            itemEnviarPDFs.Click += ItemEnviarPDFs_Click;
            itemLugarTrabajo.Click += ItemLugarTrabajo_Click;
            itemMigrarHistoricos.Click += ItemMigrarHistoricos_Click;
            itemConsultaProductividad.Click += ItemConsultaProductividad_Click;
            itemRepararPaginas.Click += ItemRepararPaginas_Click;
            
            menuConfig.Items.Add(itemConfigServidor);
            menuConfig.Items.Add(itemAdminUsuarios);
            menuConfig.Items.Add(itemAuditoria);
            menuConfig.Items.Add(itemExcel);
            menuConfig.Items.Add(itemEnviarRegistros);
            menuConfig.Items.Add(itemEnviarPDFs);
            menuConfig.Items.Add(itemLugarTrabajo);
            menuConfig.Items.Add(itemMigrarHistoricos);
            menuConfig.Items.Add(itemConsultaProductividad);
            menuConfig.Items.Add(itemRepararPaginas);

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
                
                MessageBox.Show("Configuración guardada correctamente.");
            }
        }

        private void BtnUsuarios_Click(object? sender, EventArgs e)
        {
            MessageBox.Show(
                "La administración de usuarios se ha centralizado.\n" +
                "Por favor, realice la gestión de capturistas directamente desde el Panel del Administrador en el Servidor.",
                "Gestión Centralizada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void BtnAuditoria_Click(object? sender, EventArgs e)
        {
            // Opcional: También podrías pedir PIN maestro aquí si no quieres que cualquiera lo vea
            using (FormAuditoria frm = new FormAuditoria())
            {
                frm.ShowDialog();
            }
        }
        private async void ItemExcel_Click(object? sender, EventArgs e)
        {
            string pinMaestroCorrecto = await ServicioUsuarios.ObtenerPinMaestroAsync().ConfigureAwait(true);

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
                    // Mostrar selector interactivo de rango de fechas
                    using (Form selectorFechas = new Form() { Width = 320, Height = 200, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Rango del Reporte", StartPosition = FormStartPosition.CenterScreen, MaximizeBox = false, MinimizeBox = false })
                    {
                        Label lblInicio = new Label() { Left = 20, Top = 20, Width = 100, Text = "Fecha Inicio:", AutoSize = true };
                        DateTimePicker dtpInicio = new DateTimePicker() { Left = 120, Top = 18, Width = 150, Format = DateTimePickerFormat.Short };

                        Label lblFin = new Label() { Left = 20, Top = 60, Width = 100, Text = "Fecha Fin:", AutoSize = true };
                        DateTimePicker dtpFin = new DateTimePicker() { Left = 120, Top = 58, Width = 150, Format = DateTimePickerFormat.Short };

                        Button btnExportar = new Button() { Text = "Generar Excel", Left = 150, Width = 120, Top = 110, Height = 35, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.System };

                        // Rango por defecto: ultimos 7 dias
                        dtpInicio.Value = DateTime.Today.AddDays(-7);
                        dtpFin.Value = DateTime.Today;

                        selectorFechas.Controls.Add(lblInicio);
                        selectorFechas.Controls.Add(dtpInicio);
                        selectorFechas.Controls.Add(lblFin);
                        selectorFechas.Controls.Add(dtpFin);
                        selectorFechas.Controls.Add(btnExportar);
                        selectorFechas.AcceptButton = btnExportar;

                        if (selectorFechas.ShowDialog() == DialogResult.OK)
                        {
                            DateTime inicio = dtpInicio.Value.Date;
                            DateTime fin = dtpFin.Value.Date;

                            var todos = ModuloAuditoria.ObtenerRegistrosTodos();
                            // Filtrar los registros en base a la fecha del reporte adaptando el turno nocturno
                            var filtrados = todos.Where(r => {
                                string fechaStr = ModuloAuditoria.ObtenerFechaReporte(r);
                                if (DateTime.TryParse(fechaStr, out DateTime rFecha))
                                {
                                    return rFecha.Date >= inicio && rFecha.Date <= fin;
                                }
                                return false;
                            }).ToList();

                            ModuloAuditoria.ExportarExcel(filtrados);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("PIN Maestro incorrecto.", "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void ItemEnviarRegistros_Click(object? sender, EventArgs e)
        {
            string pinMaestroCorrecto = await ServicioUsuarios.ObtenerPinMaestroAsync().ConfigureAwait(true);

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
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            ModuloAuditoria.EnviarAuditoriasAlServidorCentral(silencioso: false, soloRegistros: true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Error en envio manual: " + ex.Message);
                        }
                    });
                }
                else
                {
                    MessageBox.Show("PIN Maestro incorrecto.", "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ItemEnviarPDFs_Click(object? sender, EventArgs e)
        {
            // Solicitar el PIN maestro al usuario para autorizar la transferencia de archivos PDF
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
                    List<string> carpetasSeleccionadas = new List<string>();
                    List<string> archivosPdfList = new List<string>();

                    while (true)
                    {
                        using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                        {
                            fbd.Description = "Seleccione una carpeta o volumen de Notarías a transferir:";
                            fbd.ShowNewFolderButton = false;

                            if (carpetasSeleccionadas.Count > 0)
                            {
                                fbd.Description = string.Format("Carpetas seleccionadas: {0}\n\nSeleccione otra carpeta o cancele para iniciar la transferencia:", carpetasSeleccionadas.Count);
                            }

                            if (fbd.ShowDialog() == DialogResult.OK)
                            {
                                string carpeta = fbd.SelectedPath;
                                if (carpetasSeleccionadas.Contains(carpeta))
                                {
                                    MessageBox.Show("Esta carpeta ya ha sido seleccionada.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    continue;
                                }

                                string[] pdfsEnCarpeta;
                                try
                                {
                                    pdfsEnCarpeta = Directory.GetFiles(carpeta, "*.pdf", SearchOption.AllDirectories);
                                }
                                catch (Exception exScan)
                                {
                                    MessageBox.Show("Error al escanear la carpeta: " + exScan.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    continue;
                                }

                                if (pdfsEnCarpeta.Length == 0)
                                {
                                    MessageBox.Show("No se encontraron archivos PDF en la carpeta seleccionada.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    continue;
                                }

                                carpetasSeleccionadas.Add(carpeta);
                                archivosPdfList.AddRange(pdfsEnCarpeta);

                                var respuesta = MessageBox.Show(
                                    string.Format("Se agregaron {0} archivos de la carpeta: {1}\nTotal acumulado: {2} archivos PDF.\n\n¿Desea seleccionar otra carpeta?", pdfsEnCarpeta.Length, Path.GetFileName(carpeta), archivosPdfList.Count),
                                    "Seleccionar Varias Carpetas",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question
                                );

                                if (respuesta == DialogResult.No)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    if (archivosPdfList.Count == 0)
                    {
                        return;
                    }

                    string[] archivosPdf = archivosPdfList.ToArray();

                    var confirmacionTransferencia = MessageBox.Show(
                        string.Format("Se encontraron {0} archivos PDF en total.\n\n¿Desea iniciar la transferencia al servidor central ssdirec?", archivosPdf.Length),
                        "Confirmar Transferencia",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (confirmacionTransferencia != DialogResult.Yes)
                    {
                        return;
                    }

                            bool borrarLocales = true;

                            // Mostrar formulario de progreso
                            Form frmProgreso = new Form()
                            {
                                ClientSize = new Size(420, 100),
                                Text = "Transferencia de Archivos PDF",
                                FormBorderStyle = FormBorderStyle.FixedDialog,
                                StartPosition = FormStartPosition.CenterScreen,
                                ControlBox = false
                            };
                            Label lblProgreso = new Label() { Left = 30, Top = 20, Width = 360, Height = 25, Text = "Preparando transferencia..." };
                            ProgressBar barProgreso = new ProgressBar() 
                            { 
                                Left = 30, 
                                Top = 50, 
                                Width = 360, 
                                Height = 23, 
                                Style = ProgressBarStyle.Continuous, 
                                Minimum = 0, 
                                Maximum = archivosPdf.Length, 
                                Value = 0 
                            };
                            frmProgreso.Controls.Add(lblProgreso);
                            frmProgreso.Controls.Add(barProgreso);

                            frmProgreso.Show();
                            System.Threading.Tasks.Task.Run(() =>
                            {
                                int copiados = 0;
                                int errores = 0;
                                List<string> archivosExitosos = new List<string>();
                                object lockObj = new object();

                                using (var semaforo = new System.Threading.SemaphoreSlim(4)) // Límite de 4 subidas concurrentes
                                using (var httpClient = new System.Net.Http.HttpClient())
                                {
                                    httpClient.Timeout = TimeSpan.FromMinutes(5); // Tiempo amplio para PDFs pesados

                                    var tareas = archivosPdf.Select(async rutaArchivo =>
                                    {
                                        await semaforo.WaitAsync().ConfigureAwait(false);
                                        try
                                        {
                                            string subrutaNotaria = "General";
                                            string[] segmentos = rutaArchivo.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                            int indiceNotaria = -1;
                                            for (int i = 0; i < segmentos.Length; i++)
                                            {
                                                if (segmentos[i].StartsWith("NOTARIA", StringComparison.OrdinalIgnoreCase) && !segmentos[i].StartsWith("NOTARIAS", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    indiceNotaria = i;
                                                    break;
                                                }
                                            }

                                            if (indiceNotaria != -1)
                                            {
                                                // Obtener la notaría y el volumen (ej. NOTARIA 53\VOLUMEN 24)
                                                string[] subsegmentos = segmentos.Skip(indiceNotaria).Take(segmentos.Length - 1 - indiceNotaria).ToArray();
                                                subrutaNotaria = string.Join("\\", subsegmentos);
                                            }

                                            // Subir el PDF directamente al servidor de Laravel por HTTP POST en paralelo
                                            bool subidaExito = await ModuloAuditoria.SubirPdfAlServidorHttpAsync(
                                                httpClient,
                                                rutaArchivo,
                                                Path.GetFileName(rutaArchivo),
                                                ModuloConfiguracion.TipoCaptura,
                                                subrutaNotaria
                                            ).ConfigureAwait(false);

                                            if (subidaExito)
                                            {
                                                lock (lockObj)
                                                {
                                                    copiados++;
                                                    archivosExitosos.Add(Path.GetFileName(rutaArchivo));
                                                }

                                                // Borrar localmente si el usuario lo decidió
                                                if (borrarLocales)
                                                {
                                                    try { File.Delete(rutaArchivo); } catch { }
                                                }
                                            }
                                            else
                                            {
                                                lock (lockObj)
                                                {
                                                    errores++;
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            lock (lockObj)
                                            {
                                                errores++;
                                            }
                                        }
                                        finally
                                        {
                                            // Actualizar progreso en la UI
                                            frmProgreso.Invoke((MethodInvoker)delegate
                                            {
                                                int completados = copiados + errores;
                                                if (completados <= barProgreso.Maximum)
                                                {
                                                    barProgreso.Value = completados;
                                                }
                                                lblProgreso.Text = string.Format("Subiendo archivo {0} de {1}...", completados, archivosPdf.Length);
                                            });

                                            semaforo.Release();
                                        }
                                    });

                                    System.Threading.Tasks.Task.WhenAll(tareas).GetAwaiter().GetResult();
                                }

                                if (archivosExitosos.Count > 0)
                                {
                                    try { RepositorioAuditoria.MarcarArchivosComoExportadosRed(archivosExitosos); } catch { }
                                }

                                if (borrarLocales)
                                {
                                    foreach (string carpeta in carpetasSeleccionadas)
                                    {
                                        try
                                        {
                                            EliminarCarpetasVaciasRecursivo(carpeta);
                                        }
                                        catch { }
                                    }
                                }

                                // Cerrar form al finalizar
                                frmProgreso.Invoke((MethodInvoker)delegate
                                {
                                    frmProgreso.Close();
                                    string msgResultado = string.Format("Transferencia finalizada.\n\n• Archivos subidos: {0}\n• Errores/Fallidos: {1}", copiados, errores);
                                    if (errores > 0)
                                    {
                                        msgResultado += string.Format("\n\nDetalle del último error:\n{0}", ModuloAuditoria.UltimoErrorSubidaPdf);
                                    }
                                    MessageBox.Show(
                                        msgResultado,
                                        "Resultado de Transferencia",
                                        MessageBoxButtons.OK,
                                        errores > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                                });
                            });
                }
                else
                {
                    MessageBox.Show("PIN Maestro incorrecto.", "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static void EliminarCarpetasVaciasRecursivo(string rutaCarpeta)
        {
            if (!Directory.Exists(rutaCarpeta)) return;

            try
            {
                foreach (string subcarpeta in Directory.GetDirectories(rutaCarpeta))
                {
                    EliminarCarpetasVaciasRecursivo(subcarpeta);
                }

                if (Directory.GetFiles(rutaCarpeta).Length == 0 && Directory.GetDirectories(rutaCarpeta).Length == 0)
                {
                    Directory.Delete(rutaCarpeta, false);
                }
            }
            catch { }
        }

        private async void ItemMigrarHistoricos_Click(object? sender, EventArgs e)
        {
            string pinMaestroCorrecto = await ServicioUsuarios.ObtenerPinMaestroAsync().ConfigureAwait(true);

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
                    Cursor.Current = Cursors.WaitCursor;
                    var result = ModuloAuditoria.MigrarJsonHistoricosASqlite();
                    Cursor.Current = Cursors.Default;

                    string msg = string.Format(
                        "Proceso de importación/verificación finalizado:\n\n" +
                        "• Archivos JSON encontrados: {0}\n" +
                        "• Registros totales leídos: {1}\n" +
                        "• Registros nuevos importados a SQLite: {2}\n" +
                        "• Registros duplicados omitidos: {3}",
                        result.archivos, result.leidos, result.importados, result.duplicados
                    );
                    MessageBox.Show(msg, "Resultado de Importación", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("PIN Maestro incorrecto.", "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ItemLugarTrabajo_Click(object? sender, EventArgs e)
        {
            using (FormLugarTrabajo frm = new FormLugarTrabajo())
            {
                frm.ShowDialog();
            }
        }

        private async void BtnLogin_Click(object? sender, EventArgs e)
        {
            string user = txtUsername.Text.Trim();
            string pin = txtPin.Text.Trim();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pin))
            {
                MessageBox.Show("Ingresa usuario y PIN.");
                return;
            }

            Cursor.Current = Cursors.WaitCursor;
            Usuario usuario = await ServicioUsuarios.LoginAsync(user, pin).ConfigureAwait(true);
            Cursor.Current = Cursors.Default;

            if (usuario != null)
            {
                ModuloConfiguracion.UsuarioActual = usuario.NombreUsuario;
                ModuloConfiguracion.NombreCompletoActual = usuario.NombreCompleto ?? usuario.NombreUsuario;
                ModuloConfiguracion.TurnoActual = usuario.Turno ?? "Matutino";
                IniciarApp();
            }
            else
            {
                // Modo fallback extremo fuera de línea (Admin temporal)
                if (user == "admin" && pin == "1234")
                {
                    ModuloConfiguracion.UsuarioActual = "admin";
                    ModuloConfiguracion.NombreCompletoActual = "Administrador Local (Offline)";
                    ModuloConfiguracion.TurnoActual = "Matutino";
                    IniciarApp();
                    return;
                }
                MessageBox.Show("Credenciales incorrectas o el servidor no se encuentra disponible.");
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

            // Iniciar temporizador global de sincronización horaria
            InicializarSincronizacionGlobal();

            // Depurar registros no exportados cuyos archivos ya no existen localmente
            System.Threading.Tasks.Task.Run(() =>
            {
                RepositorioAuditoria.DepurarRegistrosSinArchivoFisico();
            });
        }

        private void InicializarSincronizacionGlobal()
        {
            temporizadorSincronizacionGlobal = new System.Windows.Forms.Timer();
            // Verificar cada 10 segundos para máxima precisión
            temporizadorSincronizacionGlobal.Interval = 10000;
            temporizadorSincronizacionGlobal.Tick += TemporizadorSincronizacionGlobal_Tick;
            temporizadorSincronizacionGlobal.Start();
        }

        private void TemporizadorSincronizacionGlobal_Tick(object? sender, EventArgs e)
        {
            // Solo el servidor realiza el envío automático a la nube
            if (!ModuloConfiguracion.EsServidor)
            {
                return;
            }

            int horaActual = DateTime.Now.Hour;

            // Si cambió de hora o al iniciar la app
            if (ultimaHoraSincronizada != horaActual)
            {
                ultimaHoraSincronizada = horaActual;

                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        // Primero importamos de forma silenciosa los registros nuevos de las carpetas de red a la BD local
                        ModuloAuditoria.MigrarJsonHistoricosASqlite();

                        // Enviar registros al servidor central (nube) de forma silenciosa
                        ModuloAuditoria.EnviarAuditoriasAlServidorCentral(silencioso: true, soloRegistros: true);
                    }
                    catch { }
                });
            }
        }

        private void ItemConsultaProductividad_Click(object? sender, EventArgs e)
        {
            using (var frm = new FormConsultaProductividad())
            {
                frm.ShowDialog();
            }
        }

        private void ItemRepararPaginas_Click(object? sender, EventArgs e)
        {
            using (var frm = new FormRepararPaginas())
            {
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    var item = sender as ToolStripMenuItem;
                    if (item != null) item.Enabled = false;

                    string pcAFiltrar = frm.PcAFiltrar;

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        var resultado = ModuloAuditoria.RecontarPaginasDelDia(pcAFiltrar);

                        this.Invoke((MethodInvoker)delegate
                        {
                            if (item != null) item.Enabled = true;

                            if (resultado.actualizados > 0)
                            {
                                MessageBox.Show(
                                    string.Format("Proceso de reparación completado con éxito para: {0}\n\n\u2022 Registros corregidos: {1}\n\u2022 Total de páginas procesadas: {2}", pcAFiltrar, resultado.actualizados, resultado.totalPaginas),
                                    "Reparación Exitosa",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show(
                                    string.Format("No se encontraron registros con páginas incorrectas (0 o 1) para corregir en: {0}.", pcAFiltrar),
                                    "Reparación Completada",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                        });
                    });
                }
            }
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
