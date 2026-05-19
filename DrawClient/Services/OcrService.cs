using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DrawClient.Services
{
    class OcrService
    {
        private const string MY_SERVER_OCR_URL = "http://localhost:5274/api/ocr/recognize";

        public static async Task<string> RecognizeTextAsync(string base64Image)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var requestBody = new { Base64Image = base64Image };
                    var jsonRequest = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(MY_SERVER_OCR_URL, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Lỗi từ Master Server: " + error);
                        return null;
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        if (doc.RootElement.TryGetProperty("text", out var textElement))
                        {
                            return textElement.GetString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi kết nối đến Master Server: " + ex.Message);
            }

            return null;
        }
    }
}