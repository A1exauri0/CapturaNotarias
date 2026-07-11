using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CapturaNotarias
{
    /// <summary>
    /// Servicio modular para gestionar la comunicación HTTP con la API de Usuarios del Servidor.
    /// </summary>
    public static class ServicioUsuarios
    {
        // Resuelve de forma dinámica la URL del servidor Node.js basada en la ruta de auditoría configurada
        public static string ObtenerUrlServidor()
        {
            string ruta = ModuloConfiguracion.RutaServidorAuditoria;
            if (string.IsNullOrEmpty(ruta)) return "http://localhost:3000";

            if (ruta.StartsWith(@"\\"))
            {
                // Extraer el host o IP de un path UNC de red (ej. \\192.168.1.10\NOTARIAS -> 192.168.1.10)
                string sinBarras = ruta.Substring(2);
                int indiceBarra = sinBarras.IndexOf('\\');
                string host = indiceBarra != -1 ? sinBarras.Substring(0, indiceBarra) : sinBarras;
                return $"http://{host}:3000";
            }
            return "http://localhost:3000";
        }

        /// <summary>
        /// Valida las credenciales de un usuario contra la API del servidor.
        /// </summary>
        public static async Task<Usuario?> LoginAsync(string usuario, string pin)
        {
            string url = $"{ObtenerUrlServidor()}/api/usuarios/login";
            using (var cliente = new HttpClient())
            {
                cliente.Timeout = TimeSpan.FromSeconds(10);
                var payload = new { nombre_usuario = usuario, pin = pin };
                string json = JsonConvert.SerializeObject(payload);
                var contenido = new StringContent(json, Encoding.UTF8, "application/json");

                try
                {
                    var respuesta = await cliente.PostAsync(url, contenido).ConfigureAwait(false);
                    if (respuesta.IsSuccessStatusCode)
                    {
                        string cuerpo = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var resultado = JsonConvert.DeserializeObject<RespuestaLogin>(cuerpo);
                        if (resultado != null && resultado.Ok && resultado.Usuario != null)
                        {
                            return resultado.Usuario;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error al conectar con la API de Login: " + ex.Message);
                }
            }
            return null;
        }

        /// <summary>
        /// Obtiene el PIN Maestro actual registrado en el servidor central.
        /// </summary>
        public static async Task<string> ObtenerPinMaestroAsync()
        {
            string url = $"{ObtenerUrlServidor()}/api/usuarios/pin-maestro";
            using (var cliente = new HttpClient())
            {
                cliente.Timeout = TimeSpan.FromSeconds(5);
                try
                {
                    var respuesta = await cliente.GetAsync(url).ConfigureAwait(false);
                    if (respuesta.IsSuccessStatusCode)
                    {
                        string cuerpo = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var resultado = JsonConvert.DeserializeObject<RespuestaPinMaestro>(cuerpo);
                        if (resultado != null && resultado.Ok && !string.IsNullOrEmpty(resultado.PinMaestro))
                        {
                            return resultado.PinMaestro;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error al obtener PIN Maestro de la API: " + ex.Message);
                }
            }
            return "2003"; // PIN por defecto si hay un fallo de conexión
        }
    }

    // Clases auxiliares para mapear respuestas JSON de la API
    public class RespuestaLogin
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("usuario")]
        public Usuario? Usuario { get; set; }

        [JsonProperty("mensaje")]
        public string? Mensaje { get; set; }
    }

    public class RespuestaPinMaestro
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("pin_maestro")]
        public string? PinMaestro { get; set; }
    }
}
