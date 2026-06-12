using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    /// <summary>
    /// Cliente HTTP que envía registros de auditoría al servidor local (PC Servidor).
    /// Se usa en las PCs cliente para enviar capturas en tiempo real.
    /// </summary>
    public static class ClienteHttpLocal
    {
        private static readonly HttpClient _cliente = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5) // Timeout corto para no bloquear
        };

        /// <summary>
        /// Obtiene la URL base del servidor local (ej: http://192.168.1.10:5050)
        /// </summary>
        private static string ObtenerUrlBase()
        {
            return $"http://{ModuloConfiguracion.IP_SERVIDOR}:{ModuloConfiguracion.PUERTO_SERVIDOR}";
        }

        /// <summary>
        /// Envía un registro individual al servidor local.
        /// Retorna true si el servidor lo recibió correctamente.
        /// </summary>
        public static async Task<bool> EnviarRegistroAsync(RegistroAuditoria registro)
        {
            try
            {
                string url = ObtenerUrlBase() + "/api/auditoria";
                string json = JsonConvert.SerializeObject(registro);
                var contenido = new StringContent(json, Encoding.UTF8, "application/json");

                var respuesta = await _cliente.PostAsync(url, contenido).ConfigureAwait(false);
                return respuesta.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Envía un lote de registros pendientes al servidor local.
        /// Se usa para reintentos de registros que no se pudieron enviar anteriormente.
        /// </summary>
        public static async Task<bool> EnviarLoteAsync(System.Collections.Generic.List<RegistroAuditoria> registros)
        {
            try
            {
                string url = ObtenerUrlBase() + "/api/auditoria/lote";
                var lote = new ArchivoAuditoriaJson { Registros = registros };
                string json = JsonConvert.SerializeObject(lote);
                var contenido = new StringContent(json, Encoding.UTF8, "application/json");

                var respuesta = await _cliente.PostAsync(url, contenido).ConfigureAwait(false);
                return respuesta.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si el servidor local está activo con un ping rápido.
        /// </summary>
        public static async Task<bool> PingAsync()
        {
            try
            {
                string url = ObtenerUrlBase() + "/api/ping";
                var respuesta = await _cliente.GetAsync(url).ConfigureAwait(false);
                return respuesta.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
