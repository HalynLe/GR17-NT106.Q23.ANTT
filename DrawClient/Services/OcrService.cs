using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DrawClient.Services
{
    class OcrService
    {
        private const string API_KEY = ""; // gắn sau
        private const string API_URL = "https://vision.googleapis.com/v1/images:annotate?key=" + API_KEY;

        public static async Task<string> RecognizeTextAsync(string base64Image)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var requestBody = new
                    {
                        requests = new[]
                        {
                            new
                            {
                                image = new { content = base64Image },
                                features = new[]
                                {
                                    new { type = "DOCUMENT_TEXT_DETECTION" }
                                }
                            }
                        }
                    };

                    var jsonRequest = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(API_URL, content);
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Lỗi API OCR: " + jsonResponse);
                        return null;
                    }

                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        var root = doc.RootElement;
                        var responses = root.GetProperty("responses");

                        if (responses.GetArrayLength() > 0)
                        {
                            var firstResponse = responses[0];
                            if (firstResponse.TryGetProperty("fullTextAnnotation", out var fullTextAnnotation))
                            {
                                if (fullTextAnnotation.TryGetProperty("text", out var textElement))
                                {
                                    return textElement.GetString()?.Trim();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi gọi OCR Service: " + ex.Message);
            }
            return null;
        }
    }
}
