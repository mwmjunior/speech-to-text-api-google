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
                        audioChannelCount = 2
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
                    // Converte jsonResponse para um objeto JSON dinâmico para análise direta
                    var jsonDocument = JsonNode.Parse(jsonResponse);

                    // Verifica se há transcrições nos resultados
                    var results = jsonDocument?["results"]?.AsArray();
                    if (results != null && results.Count > 0 && results[0]?["alternatives"]?.AsArray()?.Count > 0)
                    {
                        // Extrai a transcrição e confiança
                        ultimaTranscricao = string.Join(" ", results.Select(result => result["alternatives"][0]["transcript"].ToString()));
                        var confidence = results[0]["alternatives"][0]["confidence"]?.GetValue<float>() ?? 0;

                        return Ok(new
                        {
                            message = "Transcrição encontrada com sucesso.",
                            confidence = confidence,
                            responseDetails = jsonDocument
                        });
                    }
                    else
                    {
                        // Mostra a resposta completa do JSON caso não haja transcrição
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

        //[HttpPost]
        //[Route("search-keywords")]
        //public IActionResult BuscarPalavrasChave([FromBody] List<string> keywords)
        //{
        //    if (string.IsNullOrEmpty(ultimaTranscricao))
        //        return BadRequest("Nenhuma transcrição disponível. Realize uma transcrição primeiro.");

        //    var foundKeywords = keywords
        //        .Where(keyword => ultimaTranscricao.Contains(keyword))
        //        .ToList();

        //    return Ok(new { keywordsFound = foundKeywords });
        //}

        //[HttpPost]
        //[Route("search-keywords")]
        //public IActionResult BuscarPalavrasChave([FromBody] List<string> keywords)
        //{
        //    if (string.IsNullOrEmpty(ultimaTranscricao))
        //        return BadRequest("Nenhuma transcrição disponível. Realize uma transcrição primeiro.");

        //    // Crie um dicionário para armazenar as palavras-chave encontradas e suas contagens
        //    var foundKeywords = new Dictionary<string, int>();

        //    // Itere sobre cada palavra-chave
        //    foreach (var keyword in keywords)
        //    {
        //        // Utilize o método Count() para contar as ocorrências da palavra-chave na transcrição
        //        int count = ultimaTranscricao.Split(' ').Count(word => word.Equals(keyword, StringComparison.OrdinalIgnoreCase));

        //        // Adicione a palavra-chave e sua contagem ao dicionário
        //        if (count > 0)
        //        {
        //            foundKeywords[keyword] = count;
        //        }
        //    }

        //    return Ok(new { keywordsFound = foundKeywords });
        //}

        //[HttpPost]
        //[Route("search-keywords")]
        //public IActionResult BuscarPalavrasChave([FromBody] List<string> keywords)
        //{
        //    if (string.IsNullOrEmpty(ultimaTranscricao))
        //        return BadRequest("Nenhuma transcrição disponível. Realize uma transcrição primeiro.");

        //    // Crie um dicionário para armazenar as palavras-chave encontradas com suas marcas de tempo
        //    var foundKeywords = new Dictionary<string, List<double>>();

        //    // Converta a string da transcrição para uma lista de palavras
        //    var words = ultimaTranscricao.Split(' ');

        //    // Itere sobre as palavras da transcrição
        //    for (int i = 0; i < words.Length; i++)
        //    {
        //        // Verifique se a palavra atual corresponde a alguma das palavras-chave
        //        foreach (var keyword in keywords)
        //        {
        //            if (words[i].Equals(keyword, StringComparison.OrdinalIgnoreCase))
        //            {
        //                // Calcula a marca de tempo aproximada da palavra na transcrição
        //                double timestamp = (double)i / words.Length * ultimaTranscricao.Length;

        //                // Adiciona a palavra-chave ao dicionário, com sua marca de tempo
        //                if (!foundKeywords.ContainsKey(keyword))
        //                {
        //                    foundKeywords[keyword] = new List<double>();
        //                }
        //                foundKeywords[keyword].Add(timestamp);
        //            }
        //        }
        //    }

        //    return Ok(new { keywordsFound = foundKeywords });
        //}

        [HttpPost]
        [Route("search-keywords")]
        public IActionResult BuscarPalavrasChave([FromBody] List<string> keywords)
        {
            if (string.IsNullOrEmpty(ultimaTranscricao))
                return BadRequest("Nenhuma transcrição disponível. Realize uma transcrição primeiro.");

            // Crie uma lista para armazenar os objetos com palavra-chave e tempo
            var foundKeywords = new List<KeywordWithTimestamp>();

            // Converta a string da transcrição para uma lista de palavras
            var words = ultimaTranscricao.Split(' ');

            // Itere sobre as palavras da transcrição
            for (int i = 0; i < words.Length; i++)
            {
                // Verifique se a palavra atual corresponde a alguma das palavras-chave
                foreach (var keyword in keywords)
                {
                    if (words[i].Equals(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        // Calcula a marca de tempo aproximada da palavra na transcrição
                        double timestamp = (double)i / words.Length * ultimaTranscricao.Length;

                        // Cria um objeto KeywordWithTimestamp e adiciona à lista
                        foundKeywords.Add(new KeywordWithTimestamp { Keyword = keyword, Timestamp = timestamp });
                    }
                }
            }

            return Ok(foundKeywords);
        }

        // Classe para representar uma palavra-chave com sua marca de tempo
        public class KeywordWithTimestamp
        {
            public string Keyword { get; set; }
            public double Timestamp { get; set; }
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
