using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TranscriptionAPIGoogle.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TranscriptionController : ControllerBase
    {
        private static string ultimaTranscricao = string.Empty;
        private static List<KeywordWithTimestamp> wordTimestamps = new List<KeywordWithTimestamp>();

        [HttpPost]
        [Route("transcribe")]
        public async Task<IActionResult> TranscribeAudio(IFormFile audioFile)
        {
            try
            {
                var apiKey = ""; // Coloque aqui sua chave de API do Google

                using var client = new HttpClient();
                var requestUri = $"https://speech.googleapis.com/v1/speech:recognize?key={apiKey}";

                using var memoryStream = new MemoryStream();
                await audioFile.CopyToAsync(memoryStream);
                var audioBytes = memoryStream.ToArray();

                var requestBody = new
                {
                    config = new
                    {
                        encoding = "FLAC",
                        sampleRateHertz = 44100,
                        languageCode = "pt-BR",
                        audioChannelCount = 2,
                        enableWordTimeOffsets = true // Habilita o tempo de início e fim das palavras
                    },
                    audio = new
                    {
                        content = Convert.ToBase64String(audioBytes)
                    }
                };

                using var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync(requestUri, content);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, $"Erro na resposta da API: {jsonResponse}");
                }

                var transcriptionData = JsonSerializer.Deserialize<TranscriptionResponse>(jsonResponse);

                if (!string.IsNullOrWhiteSpace(jsonResponse))
                {
                    var jsonDocument = JsonNode.Parse(jsonResponse);

                    var results = jsonDocument?["results"]?.AsArray();
                    if (results != null && results.Count > 0 && results[0]?["alternatives"]?.AsArray()?.Count > 0)
                    {
                        ultimaTranscricao = string.Join(" ", results.Select(result => result["alternatives"][0]["transcript"].ToString()));
                        var confidence = results[0]["alternatives"][0]["confidence"]?.GetValue<float>() ?? 0;

                        // Obtenha o tempo das palavras, se disponível
                        var wordsWithTime = results[0]["alternatives"][0]["words"]?.AsArray();
                        wordTimestamps = new List<KeywordWithTimestamp>();

                        if (wordsWithTime != null)
                        {
                            foreach (var wordInfo in wordsWithTime)
                            {
                                var word = wordInfo["word"]?.ToString();
                                var startTime = wordInfo["startTime"]?.ToString();

                                wordTimestamps.Add(new KeywordWithTimestamp
                                {
                                    Keyword = word,
                                    Timestamp = startTime // Use o startTime para a marca de tempo inicial
                                });
                            }
                        }

                        return Ok(new
                        {
                            message = "Transcrição encontrada com sucesso.",
                            confidence = confidence,
                            //wordTimestamps = wordTimestamps,
                            responseDetails = jsonDocument
                        });
                    }
                    else
                    {
                        return Ok(new
                        {
                            message = "Nenhuma transcrição encontrada na resposta.",
                            responseDetails = jsonDocument
                        });
                    }
                }
                else
                {
                    return BadRequest("Erro: resposta vazia da API.");
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro de processamento: {ex.Message}");
            }
        }

        [HttpPost]
        [Route("search-keywords")]
        public IActionResult BuscarPalavrasChave([FromBody] List<string> keywords)
        {
            if (string.IsNullOrEmpty(ultimaTranscricao))
                return BadRequest("Nenhuma transcrição disponível. Realize uma transcrição primeiro.");

            // Filtra os timestamps para as palavras-chave especificadas
            var foundKeywords = wordTimestamps
                .Where(wt => keywords.Contains(wt.Keyword, StringComparer.OrdinalIgnoreCase))
                .ToList();

            return Ok(foundKeywords);
        }

        public class KeywordWithTimestamp
        {
            public string Keyword { get; set; }
            public string Timestamp { get; set; } // Agora a propriedade Timestamp é uma string
        }

        private class TranscriptionResponse
        {
            public List<TranscriptionResult> Results { get; set; }
        }

        private class TranscriptionResult
        {
            public List<TranscriptionAlternative> Alternatives { get; set; }
        }

        private class TranscriptionAlternative
        {
            public string Transcript { get; set; }
            public float Confidence { get; set; }
        }
    }
}
