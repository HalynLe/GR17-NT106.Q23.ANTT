using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MasterServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OcrController : ControllerBase
    {
        private readonly string _apiKey;

        public OcrController(IConfiguration configuration)
        {
            _apiKey = configuration["GoogleVision:ApiKey"]
                      ?? throw new InvalidOperationException("Thiếu cấu hình Google Vision API Key.");
        }

        [HttpPost("recognize")]
        public async Task<IActionResult> RecognizeText([FromBody] OcrRequest request)
        {
            if (string.IsNullOrEmpty(request.Base64Image))
                return BadRequest("Dữ liệu ảnh không hợp lệ.");

            try
            {
                using var client = new HttpClient();
                string googleApiUrl = $"https://vision.googleapis.com/v1/images:annotate?key={_apiKey}";

                var requestBody = new
                {
                    requests = new[]
                    {
                        new
                        {
                            image = new { content = request.Base64Image },
                            features = new[] { new { type = "DOCUMENT_TEXT_DETECTION" } },
                            imageContext = new { languageHints = new[] { "en-t-i0-handwrit", "vi" } }
                        }
                    }
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await client.PostAsync(googleApiUrl, jsonContent);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, "Lỗi từ Google: " + jsonResponse);

                using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                {
                    var root = doc.RootElement;
                    var responses = root.GetProperty("responses");

                    if (responses.GetArrayLength() > 0)
                    {
                        var firstResponse = responses[0];
                        if (firstResponse.TryGetProperty("fullTextAnnotation", out var fullTextAnnotation) &&
                            fullTextAnnotation.TryGetProperty("text", out var textElement))
                        {
                            return Ok(new { text = textElement.GetString()?.Trim() });
                        }
                    }
                }

                return Ok(new { text = "" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Lỗi máy chủ: " + ex.Message);
            }
        }
    }

    public class OcrRequest
    {
        public string Base64Image { get; set; } = "";
    }
}