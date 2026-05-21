using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    public class RegistroAuditoria
    {
        public string? FechaHora { get; set; }
        public string? Usuario { get; set; }
        public string? NombreCompleto { get; set; }
        public string? Turno { get; set; } // "Matutino", "Vespertino", "Nocturno"
        public string? PC { get; set; }
        public string? IP { get; set; }
        public string? Notaria { get; set; }
        public string? Accion { get; set; }
        public string? ArchivoOriginal { get; set; }
        public string? Detalles { get; set; }
        public int Paginas { get; set; } = 1; // Para guardar el total de imágenes (páginas) del PDF
    }

    public class ArchivoAuditoriaJson
    {
        public List<RegistroAuditoria> Registros { get; set; } = new List<RegistroAuditoria>();
    }

    public static class ModuloAuditoria
    {
        public static int ObtenerPaginasPdf(string rutaCompleta)
        {
            if (string.IsNullOrEmpty(rutaCompleta) || !File.Exists(rutaCompleta))
                return 1;

            // Reintentos en caso de que el escáner mantenga bloqueado el archivo mientras se guarda
            for (int i = 0; i < 15; i++)
            {
                try
                {
                    using (var fs = new FileStream(rutaCompleta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var document = PdfSharp.Pdf.IO.PdfReader.Open(fs, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
                        {
                            return document.PageCount;
                        }
                    }
                }
                catch
                {
                    System.Threading.Thread.Sleep(500);
                }
            }
            return 1; // Fallback
        }

        public static void RegistrarAccion(string notaria, string archivo, string rutaCompleta, string detalles = "")
        {
            try
            {
                // Si no hay un usuario activo logueado en la aplicación, ignoramos el registro
                if (string.IsNullOrEmpty(ModuloConfiguracion.UsuarioActual))
                {
                    return;
                }

                string pcNombre = string.IsNullOrEmpty(ModuloConfiguracion.NombrePC) ? Environment.MachineName : ModuloConfiguracion.NombrePC;

                // Si el nombre del archivo empieza con "PC" seguido de dígitos (ej: PC02...),
                // validamos que coincida con el número de esta PC para evitar registrar capturas de otros equipos en red.
                string archivoUpper = archivo.ToUpper();
                var coincidenciaPc = System.Text.RegularExpressions.Regex.Match(archivoUpper, @"^PC(\d+)");
                if (coincidenciaPc.Success)
                {
                    string prefijoArchivo = coincidenciaPc.Value; // Ej: "PC02"
                    string pcNombreNorm = pcNombre.Replace("-", "").Replace(" ", "").ToUpper(); // Ej: "PC-02" -> "PC02"
                    if (pcNombreNorm != prefijoArchivo && !pcNombreNorm.Contains(prefijoArchivo) && !prefijoArchivo.Contains(pcNombreNorm))
                    {
                        // No pertenece a este equipo, ignoramos el evento de red silenciosamente
                        return;
                    }
                }

                string localIp = "";
                try
                {
                    var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                    foreach (var ip in host.AddressList)
                    {
                        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            localIp = ip.ToString();
                            break;
                        }
                    }
                }
                catch {}

                // Contar páginas del PDF de forma segura
                int paginas = ObtenerPaginasPdf(rutaCompleta);

                // Carpeta centralizada en el servidor para guardar las auditorías de captura
                string rutaCarpetaPC = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "MonitoreoCaptura", pcNombre);
                
                if (!Directory.Exists(rutaCarpetaPC))
                    Directory.CreateDirectory(rutaCarpetaPC);

                string rutaJson = Path.Combine(rutaCarpetaPC, "auditoria.json");
                ArchivoAuditoriaJson auditoria;

                if (File.Exists(rutaJson))
                {
                    try
                    {
                        // Leemos con FileShare.ReadWrite para evitar colisiones
                        string json = "";
                        using (FileStream fs = new FileStream(rutaJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (StreamReader sr = new StreamReader(fs))
                        {
                            json = sr.ReadToEnd();
                        }

                        auditoria = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json) ?? new ArchivoAuditoriaJson();
                    }
                    catch
                    {
                        // Si está corrupto, empezamos uno nuevo para no detener la captura
                        auditoria = new ArchivoAuditoriaJson();
                    }
                }
                else
                {
                    auditoria = new ArchivoAuditoriaJson();
                }

                // Inyectamos el nuevo registro
                auditoria.Registros.Add(new RegistroAuditoria
                {
                    FechaHora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Usuario = ModuloConfiguracion.UsuarioActual,
                    NombreCompleto = ModuloConfiguracion.NombreCompletoActual,
                    Turno = ModuloConfiguracion.TurnoActual,
                    PC = pcNombre,
                    IP = localIp,
                    Notaria = notaria,
                    Accion = "Capturado",
                    ArchivoOriginal = archivo,
                    Detalles = detalles,
                    Paginas = paginas
                });

                string jsonFinal = JsonConvert.SerializeObject(auditoria, Formatting.Indented);
                
                // Intentamos guardar con reintentos silenciosos
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        File.WriteAllText(rutaJson, jsonFinal);
                        break;
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                }
            }
            catch
            {
                // En segundo plano no interrumpimos con MsgBox
            }
        }

        public static List<RegistroAuditoria> ObtenerRegistrosTodos()
        {
            var todos = new List<RegistroAuditoria>();
            try
            {
                if (string.IsNullOrEmpty(ModuloConfiguracion.RutaServidorAuditoria)) return todos;
                
                string rutaBase = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "MonitoreoCaptura");
                if (Directory.Exists(rutaBase))
                {
                    string[] carpetasPC = Directory.GetDirectories(rutaBase);
                    foreach (string pc in carpetasPC)
                    {
                        string rutaJson = Path.Combine(pc, "auditoria.json");
                        if (File.Exists(rutaJson))
                        {
                            try
                            {
                                string json = "";
                                using (FileStream fs = new FileStream(rutaJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                using (StreamReader sr = new StreamReader(fs))
                                {
                                    json = sr.ReadToEnd();
                                }
                                var obj = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json);
                                if (obj != null && obj.Registros != null)
                                {
                                    todos.AddRange(obj.Registros);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
            // Ordenar por fecha de forma descendente (el más nuevo arriba)
            return todos.OrderBy(r => r.FechaHora).ToList();
        }

        public static void ExportarExcel(List<RegistroAuditoria> datos)
        {
            if (datos == null || datos.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Filtrar registros inválidos (sin usuario) para evitar filas vacías en el reporte Excel
            datos = datos.Where(r => !string.IsNullOrEmpty(r.Usuario) || !string.IsNullOrEmpty(r.NombreCompleto)).ToList();

            // 1. Agrupar todos los datos por Fecha (YYYY-MM-DD)
            var registrosPorFecha = new Dictionary<string, List<RegistroAuditoria>>();
            foreach (var reg in datos)
            {
                string fecha = "Sin Fecha";
                if (!string.IsNullOrEmpty(reg.FechaHora) && reg.FechaHora.Length >= 10)
                {
                    fecha = reg.FechaHora.Substring(0, 10);
                }
                if (!registrosPorFecha.ContainsKey(fecha))
                {
                    registrosPorFecha[fecha] = new List<RegistroAuditoria>();
                }
                registrosPorFecha[fecha].Add(reg);
            }

            // 2. Para cada fecha, deduplicar los registros de forma global por archivo original
            // para que no se dupliquen entre diferentes PCs o usuarios que vigilan la misma carpeta.
            var registrosDeduplicados = new List<RegistroAuditoria>();
            foreach (var fecha in registrosPorFecha.Keys)
            {
                var grupoArchivos = registrosPorFecha[fecha]
                    .GroupBy(r => r.ArchivoOriginal ?? "Desconocido", StringComparer.OrdinalIgnoreCase);

                foreach (var grupo in grupoArchivos)
                {
                    if (grupo.Count() == 1)
                    {
                        registrosDeduplicados.Add(grupo.First());
                    }
                    else
                    {
                        // Seleccionar el registro óptimo basándonos en el prefijo del archivo o la hora de registro
                        string nombreArchivo = grupo.Key;
                        var coincidenciaPc = System.Text.RegularExpressions.Regex.Match(nombreArchivo, @"^PC(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        
                        RegistroAuditoria? seleccionado = null;
                        if (coincidenciaPc.Success)
                        {
                            string prefijoPC = coincidenciaPc.Value.ToUpper(); // Ej: "PC12" o "PC04"
                            seleccionado = grupo.FirstOrDefault(r => {
                                if (string.IsNullOrEmpty(r.PC)) return false;
                                string pcNormalizada = r.PC.Replace("-", "").Replace(" ", "").ToUpper(); // Ej: "PC-04" -> "PC04"
                                return pcNormalizada == prefijoPC || pcNormalizada.Contains(prefijoPC) || prefijoPC.Contains(pcNormalizada);
                            });
                        }

                        // Si no hay coincidencia de prefijo con ninguna PC del grupo, o el archivo no tiene formato PCXX (ej. 0001.pdf),
                        // nos quedamos con el registro que ocurrió primero (la hora más temprana).
                        if (seleccionado == null)
                        {
                            seleccionado = grupo
                                .OrderBy(r => string.IsNullOrEmpty(r.FechaHora) ? "9999-12-31" : r.FechaHora)
                                .First();
                        }

                        // Conservar el mayor número de páginas si en otros registros se leyó de forma completa
                        int maximoPaginas = grupo.Max(r => r.Paginas);
                        if (maximoPaginas > seleccionado.Paginas)
                        {
                            seleccionado.Paginas = maximoPaginas;
                        }

                        registrosDeduplicados.Add(seleccionado);
                    }
                }
            }

            // 3. Agrupar los registros deduplicados por Fecha -> PC -> IP -> Usuario -> Turno para su procesamiento
            var registrosAgrupados = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, List<RegistroAuditoria>>>>>>();

            foreach (var reg in registrosDeduplicados)
            {
                string fecha = "Sin Fecha";
                if (!string.IsNullOrEmpty(reg.FechaHora) && reg.FechaHora.Length >= 10)
                {
                    fecha = reg.FechaHora.Substring(0, 10);
                }

                if (!registrosAgrupados.ContainsKey(fecha))
                {
                    registrosAgrupados[fecha] = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, List<RegistroAuditoria>>>>>();
                }

                string pc = string.IsNullOrEmpty(reg.PC) ? "Desconocido" : reg.PC;
                if (!registrosAgrupados[fecha].ContainsKey(pc))
                {
                    registrosAgrupados[fecha][pc] = new Dictionary<string, Dictionary<string, Dictionary<string, List<RegistroAuditoria>>>>();
                }

                string ip = string.IsNullOrEmpty(reg.IP) ? "Desconocido" : reg.IP;
                if (!registrosAgrupados[fecha][pc].ContainsKey(ip))
                {
                    registrosAgrupados[fecha][pc][ip] = new Dictionary<string, Dictionary<string, List<RegistroAuditoria>>>();
                }

                string usuario = string.IsNullOrEmpty(reg.NombreCompleto) ? (string.IsNullOrEmpty(reg.Usuario) ? "Desconocido" : reg.Usuario) : reg.NombreCompleto;
                if (!registrosAgrupados[fecha][pc][ip].ContainsKey(usuario))
                {
                    registrosAgrupados[fecha][pc][ip][usuario] = new Dictionary<string, List<RegistroAuditoria>>();
                }

                string turno = string.IsNullOrEmpty(reg.Turno) ? "Matutino" : reg.Turno;
                if (!registrosAgrupados[fecha][pc][ip][usuario].ContainsKey(turno))
                {
                    registrosAgrupados[fecha][pc][ip][usuario][turno] = new List<RegistroAuditoria>();
                }

                registrosAgrupados[fecha][pc][ip][usuario][turno].Add(reg);
            }

            // 4. Crear el diccionario final con los totales ya procesados y deduplicados
            var datosPorFecha = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, (int pdfs, int paginas)>>>>>();

            foreach (var fecha in registrosAgrupados.Keys)
            {
                datosPorFecha[fecha] = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, (int pdfs, int paginas)>>>>();
                foreach (var pc in registrosAgrupados[fecha].Keys)
                {
                    datosPorFecha[fecha][pc] = new Dictionary<string, Dictionary<string, Dictionary<string, (int pdfs, int paginas)>>>();
                    foreach (var ip in registrosAgrupados[fecha][pc].Keys)
                    {
                        datosPorFecha[fecha][pc][ip] = new Dictionary<string, Dictionary<string, (int pdfs, int paginas)>>();
                        foreach (var usuario in registrosAgrupados[fecha][pc][ip].Keys)
                        {
                            datosPorFecha[fecha][pc][ip][usuario] = new Dictionary<string, (int pdfs, int paginas)>();
                            foreach (var turno in registrosAgrupados[fecha][pc][ip][usuario].Keys)
                            {
                                var listaRegs = registrosAgrupados[fecha][pc][ip][usuario][turno];
                                
                                int totalPdfs = listaRegs.Count;
                                int totalPaginas = listaRegs.Sum(r => r.Paginas > 0 ? r.Paginas : 1);

                                datosPorFecha[fecha][pc][ip][usuario][turno] = (totalPdfs, totalPaginas);
                            }
                        }
                    }
                }
            }

            try
            {
                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    // Crear una hoja por cada fecha (ordenadas descendente)
                    foreach (var fecha in datosPorFecha.Keys.OrderByDescending(k => k))
                    {
                        string sheetName = fecha.Replace("-", "_");
                        // ClosedXML worksheets names cannot exceed 31 chars
                        if (sheetName.Length > 31) sheetName = sheetName.Substring(0, 31);
                        
                        var worksheet = workbook.Worksheets.Add(sheetName);

                        // Cabeceras
                        worksheet.Cell(1, 1).Value = "PC";
                        worksheet.Cell(1, 2).Value = "IP";
                        worksheet.Cell(1, 3).Value = "Usuario";
                        worksheet.Cell(1, 4).Value = "Turno";
                        worksheet.Cell(1, 5).Value = "Capturas (PDFs)";
                        worksheet.Cell(1, 6).Value = "Total de Imágenes";

                        var listaFilas = new List<(string PC, string IP, string Usuario, string Turno, int Pdfs, int Paginas)>();
                        var pcsDeLaFecha = datosPorFecha[fecha];
                        foreach (var pc in pcsDeLaFecha.Keys)
                        {
                            var ipsDeLaPc = pcsDeLaFecha[pc];
                            foreach (var ip in ipsDeLaPc.Keys)
                            {
                                var usuariosDeLaIp = ipsDeLaPc[ip];
                                foreach (var usuario in usuariosDeLaIp.Keys)
                                {
                                    var turnosDelUsuario = usuariosDeLaIp[usuario];
                                    foreach (var turno in turnosDelUsuario.Keys)
                                    {
                                        var stats = turnosDelUsuario[turno];
                                        listaFilas.Add((pc, ip, usuario, turno, stats.pdfs, stats.paginas));
                                    }
                                }
                            }
                        }

                        // Ordenar por turno: Matutino -> Vespertino -> Nocturno -> Otros
                        var filasOrdenadas = listaFilas.OrderBy(r => {
                            if (r.Turno.Equals("Matutino", StringComparison.OrdinalIgnoreCase)) return 1;
                            if (r.Turno.Equals("Vespertino", StringComparison.OrdinalIgnoreCase)) return 2;
                            if (r.Turno.Equals("Nocturno", StringComparison.OrdinalIgnoreCase)) return 3;
                            return 4;
                        }).ToList();

                        int rowIdx = 2;
                        foreach (var fila in filasOrdenadas)
                        {
                            worksheet.Cell(rowIdx, 1).Value = fila.PC;
                            worksheet.Cell(rowIdx, 2).Value = fila.IP;
                            worksheet.Cell(rowIdx, 3).Value = fila.Usuario;
                            worksheet.Cell(rowIdx, 4).Value = fila.Turno;
                            worksheet.Cell(rowIdx, 5).Value = fila.Pdfs;
                            worksheet.Cell(rowIdx, 6).Value = fila.Paginas;

                            // Pintamos la fila según el turno con tonos pastel muy elegantes
                            string colorHtml = "#F2F2F2"; // Gris para cualquier otro
                            if (fila.Turno.Equals("Matutino", StringComparison.OrdinalIgnoreCase))
                            {
                                colorHtml = "#FFF2CC"; // Amarillo/Naranja pastel suave
                            }
                            else if (fila.Turno.Equals("Vespertino", StringComparison.OrdinalIgnoreCase))
                            {
                                colorHtml = "#E2EFDA"; // Verde claro pastel suave
                            }
                            else if (fila.Turno.Equals("Nocturno", StringComparison.OrdinalIgnoreCase))
                            {
                                colorHtml = "#DDEBF7"; // Azul pastel suave
                            }

                            var rangoFila = worksheet.Range(rowIdx, 1, rowIdx, 6);
                            rangoFila.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml(colorHtml);
                            rangoFila.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                            rangoFila.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                            rangoFila.Style.Border.OutsideBorderColor = ClosedXML.Excel.XLColor.FromHtml("#D9D9D9");
                            rangoFila.Style.Border.InsideBorderColor = ClosedXML.Excel.XLColor.FromHtml("#D9D9D9");

                            rowIdx++;
                        }

                        // Estilo Cabeceras
                        var rangoHeader = worksheet.Range(1, 1, 1, 6);
                        rangoHeader.Style.Font.Bold = true;
                        rangoHeader.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#4F81BD");
                        rangoHeader.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                        worksheet.Columns().AdjustToContents();
                    }

                    // Guardar
                    using (var sfd = new SaveFileDialog())
                    {
                        sfd.Filter = "Libro de Excel (*.xlsx)|*.xlsx";
                        sfd.FileName = "Reporte_Diario_Captura_" + DateTime.Now.ToString("yyyyMMdd_HHmm");

                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            workbook.SaveAs(sfd.FileName);
                            MessageBox.Show("Reporte generado con éxito.", "Resultado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                            }
                            catch {}
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar Excel: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void EnviarAuditoriasAlServidorCentral(bool silencioso = false)
        {
            string destinoBase = @"\\172.40.5.84\ssdirec\NOTARIAS";

            try
            {
                // Verificar/crear el directorio de destino
                if (!Directory.Exists(destinoBase))
                {
                    try
                    {
                        Directory.CreateDirectory(destinoBase);
                    }
                    catch (Exception ex)
                    {
                        if (!silencioso)
                            MessageBox.Show("No se pudo acceder o crear la carpeta de destino en el servidor: " + ex.Message, "Error de Conexión", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                // Obtener todos los registros locales de todas las PCs
                var registrosLocales = ObtenerRegistrosTodos();
                if (registrosLocales.Count == 0)
                {
                    if (!silencioso)
                        MessageBox.Show("No se encontraron registros de auditoría local en " + ModuloConfiguracion.RutaServidorAuditoria, "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Cargar auditoría central si existe
                string rutaJsonCentral = Path.Combine(destinoBase, "auditoria.json");
                ArchivoAuditoriaJson auditoriaCentral = new ArchivoAuditoriaJson();

                if (File.Exists(rutaJsonCentral))
                {
                    try
                    {
                        string jsonCentral = "";
                        using (FileStream fs = new FileStream(rutaJsonCentral, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (StreamReader sr = new StreamReader(fs))
                        {
                            jsonCentral = sr.ReadToEnd();
                        }
                        auditoriaCentral = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(jsonCentral) ?? new ArchivoAuditoriaJson();
                    }
                    catch
                    {
                        auditoriaCentral = new ArchivoAuditoriaJson();
                    }
                }

                if (auditoriaCentral.Registros == null)
                {
                    auditoriaCentral.Registros = new List<RegistroAuditoria>();
                }

                // Crear un conjunto de claves únicas ya registradas en la auditoría central para evitar duplicados.
                var registrosYaEnviados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var reg in auditoriaCentral.Registros)
                {
                    string clave = string.Format("{0}_{1}_{2}", reg.PC ?? "", reg.ArchivoOriginal ?? "", reg.FechaHora ?? "");
                    registrosYaEnviados.Add(clave);
                }

                int totalEnviados = 0;
                int totalExistentes = 0;
                int totalErrores = 0;

                // Crear formulario de progreso solo si no es silencioso
                Form? formularioProgreso = null;
                Label? etiquetaProgreso = null;
                ProgressBar? barraProgreso = null;

                if (!silencioso)
                {
                    formularioProgreso = new Form()
                    {
                        ClientSize = new Size(420, 130),
                        Text = "Sincronización de Auditoría",
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        StartPosition = FormStartPosition.CenterScreen,
                        ControlBox = false
                    };
                    etiquetaProgreso = new Label() { Left = 30, Top = 25, Width = 360, Height = 25, Text = "Analizando registros a transferir..." };
                    barraProgreso = new ProgressBar() { Left = 30, Top = 65, Width = 360, Height = 23, Style = ProgressBarStyle.Continuous, Maximum = registrosLocales.Count };
                    formularioProgreso.Controls.Add(etiquetaProgreso);
                    formularioProgreso.Controls.Add(barraProgreso);
                    formularioProgreso.Show();
                    formularioProgreso.Refresh();
                }

                int contador = 0;

                foreach (var reg in registrosLocales)
                {
                    contador++;
                    if (!silencioso && barraProgreso != null && etiquetaProgreso != null && formularioProgreso != null)
                    {
                        barraProgreso.Value = contador;
                        etiquetaProgreso.Text = string.Format("Sincronizando {0} de {1}: {2}", contador, registrosLocales.Count, reg.ArchivoOriginal);
                        formularioProgreso.Refresh();
                    }

                    string claveRegistro = string.Format("{0}_{1}_{2}", reg.PC ?? "", reg.ArchivoOriginal ?? "", reg.FechaHora ?? "");

                    // Si ya existe en la auditoría central, lo contamos y saltamos
                    if (registrosYaEnviados.Contains(claveRegistro))
                    {
                        totalExistentes++;
                        continue;
                    }

                    try
                    {
                        auditoriaCentral.Registros.Add(new RegistroAuditoria
                        {
                            FechaHora = reg.FechaHora,
                            Usuario = reg.Usuario,
                            NombreCompleto = reg.NombreCompleto,
                            Turno = reg.Turno,
                            PC = reg.PC,
                            IP = reg.IP,
                            Notaria = reg.Notaria,
                            Accion = reg.Accion ?? "Capturado",
                            ArchivoOriginal = reg.ArchivoOriginal,
                            Detalles = reg.Detalles,
                            Paginas = reg.Paginas
                        });
                        registrosYaEnviados.Add(claveRegistro);
                        totalEnviados++;
                    }
                    catch
                    {
                        totalErrores++;
                    }
                }

                if (!silencioso && formularioProgreso != null)
                {
                    formularioProgreso.Close();
                }

                // Guardar la auditoría central actualizada (solo si se agregaron nuevos registros)
                if (totalEnviados > 0)
                {
                    string jsonFinal = JsonConvert.SerializeObject(auditoriaCentral, Formatting.Indented);
                    File.WriteAllText(rutaJsonCentral, jsonFinal);
                }

                if (!silencioso)
                {
                    string mensajeResultado = string.Format(
                        "Sincronización completada.\n\n" +
                        "• Nuevos registros agregados: {0}\n" +
                        "• Registros ya existentes en el servidor: {1}\n" +
                        "• Errores al registrar: {2}\n\n" +
                        "La auditoría central se actualizó correctamente.", 
                        totalEnviados, totalExistentes, totalErrores
                    );
                    MessageBox.Show(mensajeResultado, "Resultado de Sincronización", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (!silencioso)
                    MessageBox.Show("Ocurrió un error general durante la sincronización: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static List<DiagnosticoPC> ObtenerDiagnosticoPCs()
        {
            List<DiagnosticoPC> lista = new List<DiagnosticoPC>();
            try
            {
                string rutaBase = Path.Combine(ModuloConfiguracion.RutaServidorAuditoria, "MonitoreoCaptura");
                if (!Directory.Exists(rutaBase))
                {
                    lista.Add(new DiagnosticoPC 
                    { 
                        PC = "MonitoreoCaptura", 
                        Estado = "🔴 Carpeta no existe", 
                        Detalles = "No se encontró la ruta base: " + rutaBase, 
                        UltimaModificacion = "-", 
                        EsCorrecto = false 
                    });
                    return lista;
                }

                string[] carpetasPC = Directory.GetDirectories(rutaBase);
                foreach (string pc in carpetasPC)
                {
                    string nombrePC = Path.GetFileName(pc);
                    string rutaJson = Path.Combine(pc, "auditoria.json");
                    
                    DiagnosticoPC diag = new DiagnosticoPC { PC = nombrePC };

                    try
                    {
                        if (!File.Exists(rutaJson))
                        {
                            diag.Estado = "⚠️ Sin auditoría";
                            diag.Detalles = "No existe el archivo auditoria.json en esta carpeta.";
                            diag.UltimaModificacion = "-";
                            diag.EsCorrecto = false;
                        }
                        else
                        {
                            DateTime ultimaMod = File.GetLastWriteTime(rutaJson);
                            diag.UltimaModificacion = ultimaMod.ToString("yyyy-MM-dd HH:mm:ss");

                            // Intentar leer para validar permisos y formato
                            string json = "";
                            using (FileStream fs = new FileStream(rutaJson, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (StreamReader sr = new StreamReader(fs))
                            {
                                json = sr.ReadToEnd();
                            }

                            ArchivoAuditoriaJson auditoria = JsonConvert.DeserializeObject<ArchivoAuditoriaJson>(json) ?? new ArchivoAuditoriaJson();
                            if (auditoria.Registros == null)
                            {
                                diag.Estado = "🔴 Sin registros";
                                diag.Detalles = "El archivo JSON existe pero no contiene lista de registros.";
                                diag.EsCorrecto = false;
                            }
                            else
                            {
                                int totalRegistros = auditoria.Registros.Count;
                                diag.Estado = "🟢 Conectado";
                                diag.Detalles = string.Format("Legible. Contiene {0} registros.", totalRegistros);
                                diag.EsCorrecto = true;
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        diag.Estado = "🔴 Acceso denegado";
                        diag.Detalles = "No se tienen permisos de lectura en red.";
                        diag.EsCorrecto = false;
                        diag.UltimaModificacion = "-";
                    }
                    catch (IOException ioEx)
                    {
                        diag.Estado = "🔴 Archivo bloqueado";
                        diag.Detalles = "Bloqueado por otro proceso: " + ioEx.Message;
                        diag.EsCorrecto = false;
                        diag.UltimaModificacion = "-";
                    }
                    catch (Exception ex)
                    {
                        diag.Estado = "🔴 Error de lectura";
                        diag.Detalles = ex.Message;
                        diag.EsCorrecto = false;
                        diag.UltimaModificacion = "-";
                    }

                    lista.Add(diag);
                }
            }
            catch (Exception ex)
            {
                lista.Add(new DiagnosticoPC 
                { 
                    PC = "General", 
                    Estado = "🔴 Fallo general", 
                    Detalles = "Error: " + ex.Message, 
                    UltimaModificacion = "-", 
                    EsCorrecto = false 
                });
            }

            return lista;
        }
    }

    public class DiagnosticoPC
    {
        public string PC { get; set; } = "";
        public string Estado { get; set; } = "";
        public string Detalles { get; set; } = "";
        public string UltimaModificacion { get; set; } = "";
        public bool EsCorrecto { get; set; } = false;
    }
}
