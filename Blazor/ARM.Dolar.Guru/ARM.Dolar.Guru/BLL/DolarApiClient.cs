using ARM.Dolar.Guru.DTO;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ARM.Dolar.Guru.BLL
{
    public static class DolarApiClient
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<List<DolarCotizacion>> ObtenerCotizacionesAsync(string endpoint)
        {
            try
            {
                HttpResponseMessage respuesta = await _httpClient.GetAsync(endpoint);
                respuesta.EnsureSuccessStatusCode();

                string contenidoJson = await respuesta.Content.ReadAsStringAsync();
                var cotizaciones = JsonSerializer.Deserialize<List<DolarCotizacion>>(contenidoJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return cotizaciones ?? new List<DolarCotizacion>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener cotizaciones: {ex.Message}");
                return new List<DolarCotizacion>();
            }
        }
    }
}