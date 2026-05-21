using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CapturaNotarias
{
    public class FormDiagnostico : Form
    {
        private ListView listaResultados;
        private Button btnReanalizar;
        private Button btnCerrar;
        private Label lblResumen;

        public FormDiagnostico()
        {
            this.Text = "Diagnóstico de Conexión de PCs";
            this.Size = new Size(650, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            lblResumen = new Label() 
            { 
                Left = 20, 
                Top = 15, 
                Width = 600, 
                Height = 25, 
                Font = new Font("Arial", 9.5F, FontStyle.Bold) 
            };

            listaResultados = new ListView()
            {
                Left = 20,
                Top = 45,
                Width = 595,
                Height = 250,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            listaResultados.Columns.Add("Equipo (PC)", 110);
            listaResultados.Columns.Add("Estado", 130);
            listaResultados.Columns.Add("Detalles", 200);
            listaResultados.Columns.Add("Última Modificación", 140);

            btnReanalizar = new Button() { Text = "🔄 Reanalizar", Left = 20, Top = 310, Width = 120, Height = 30, FlatStyle = FlatStyle.Flat };
            btnCerrar = new Button() { Text = "Cerrar", Left = 495, Top = 310, Width = 120, Height = 30, FlatStyle = FlatStyle.Flat };

            btnReanalizar.Click += (s, e) => EjecutarDiagnostico();
            btnCerrar.Click += (s, e) => this.Close();

            this.Controls.Add(lblResumen);
            this.Controls.Add(listaResultados);
            this.Controls.Add(btnReanalizar);
            this.Controls.Add(btnCerrar);

            EjecutarDiagnostico();
        }

        private void EjecutarDiagnostico()
        {
            listaResultados.Items.Clear();
            Cursor.Current = Cursors.WaitCursor;

            var resultados = ModuloAuditoria.ObtenerDiagnosticoPCs();

            int correctas = 0;
            int incorrectas = 0;

            foreach (var r in resultados)
            {
                ListViewItem item = new ListViewItem(r.PC);
                item.SubItems.Add(r.Estado);
                item.SubItems.Add(r.Detalles);
                item.SubItems.Add(r.UltimaModificacion);

                if (r.EsCorrecto)
                {
                    item.ForeColor = Color.DarkGreen;
                    correctas++;
                }
                else
                {
                    item.ForeColor = Color.DarkRed;
                    incorrectas++;
                }

                listaResultados.Items.Add(item);
            }

            lblResumen.Text = string.Format("Diagnóstico finalizado. PCs Activas: {0} | Con Errores o Inactivas: {1}", correctas, incorrectas);
            Cursor.Current = Cursors.Default;
        }
    }
}
