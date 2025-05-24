using ARM.Dolar.Guru.DTO;
using System.Text.Json;

namespace ARM.Dolar.Guru.BLL
{
    public static class DolarHistoricoClient
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<List<DolarHistorico>> ObtenerDolarHistoricoAsync(string endpoint)
        {
            try
            {
                HttpResponseMessage respuesta = await _httpClient.GetAsync(endpoint);
                respuesta.EnsureSuccessStatusCode();

                string contenidoJson = await respuesta.Content.ReadAsStringAsync();
                var cotizaciones = JsonSerializer.Deserialize<List<DolarHistorico>>(contenidoJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return cotizaciones ?? new List<DolarHistorico>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener DolarHistorico: {ex.Message}");
                return new List<DolarHistorico>();
            }
        }
    }
}