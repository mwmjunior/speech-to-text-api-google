using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace TranscriptionAPIGoogle.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TranscriptionController : ControllerBase
    {
        [HttpPost]
        [Route("transcribe")]
        public async Task<IActionResult> TranscribeAudio(IFormFile audioFile)
        {
            try
            {
                var apiKey = "GOOGLE_API_KEY"; // Coloque aqui sua chave de API do Google

                using var client = new HttpClient();

                // Configura a URL com a chave da API no formato de URL para Google Speech-to-Text
                var requestUri = $"https://speech.googleapis.com/v1/speech:recognize?key={apiKey}";

                using var memoryStream = new MemoryStream();
                await audioFile.CopyToAsync(memoryStream);
                var audioBytes = memoryStream.ToArray();

                // Monta o JSON de requisição conforme esperado pela API Google Speech-to-Text
                var requestBody = new
                {
                    config = new
                    {
                        encoding = "LINEAR16", // Ajuste conforme o tipo de áudio enviado
                        sampleRateHertz = 16000, // Ajuste conforme a taxa do áudio
                        languageCode = "pt-BR" // Código do idioma
                    },
                    audio = new
                    {
                        content = Convert.ToBase64String(audioBytes)
                    }
                };

                using var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync(requestUri, content);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

                var transcription = await response.Content.ReadAsStringAsync();
                return Ok(transcription);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
