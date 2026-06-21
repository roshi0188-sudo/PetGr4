using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace PetSocial.Services
{
    public class OpenAiPetService : IPetAiService
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAiOptions _options;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public OpenAiPetService(HttpClient httpClient, IOptions<OpenAiOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

        public Task<string> AskCareQuestionAsync(string question, CancellationToken cancellationToken = default)
        {
            var input = $"""
            Cau hoi cua nguoi nuoi thu cung: {question}

            Hay tra loi bang tieng Viet, ngan gon, than thien, uu tien loi khuyen cham soc an toan.
            Neu co dau hieu cap cuu thu y, hay khuyen dua thu cung den bac si thu y.
            """;

            return CreateTextResponseAsync(
                "Ban la chatbot tu van cham soc thu cung cho mang xa hoi PetSocial.",
                input,
                cancellationToken);
        }

        public Task<string> SuggestDietAsync(string species, string? breed, int? age, decimal? weight, CancellationToken cancellationToken = default)
        {
            var input = $"""
            Hay goi y che do an cho thu cung voi thong tin:
            - Loai: {species}
            - Giong: {breed}
            - Tuoi: {age?.ToString() ?? "khong ro"}
            - Can nang: {weight?.ToString("0.##") ?? "khong ro"} kg

            Tra loi bang tieng Viet, gom: khau phan goi y, thuc pham nen dung, thuc pham can tranh, luu y suc khoe.
            """;

            return CreateTextResponseAsync(
                "Ban la tro ly dinh duong thu cung. Khong chan doan benh, chi dua loi khuyen tham khao.",
                input,
                cancellationToken);
        }

        public async Task<PetImageClassification> ClassifyPetImageAsync(IFormFile imageFile, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
                return new PetImageClassification(null, null, "Chua cau hinh OpenAI API key.");

            var imageDataUrl = await ToDataUrlAsync(imageFile, cancellationToken);
            var payload = new
            {
                model = _options.Model,
                instructions = """
                Nhận diện vật nuôi trong ảnh và trả về tiếng Việt có dấu.
                Chỉ trả về JSON hợp lệ, không thêm giải thích ngoài JSON.
                Loài phải là tên cụ thể nếu nhận ra, ví dụ: Chó, Mèo, Chim, Cá, Thỏ, Hamster, Rùa, Rắn, Bò sát, Khác.
                Giống hoặc nhóm giống phải cụ thể nhất có thể bằng tiếng Việt có dấu, ví dụ: Poodle, Mèo Anh lông ngắn, Corgi, Rắn cạp nong, Rùa tai đỏ.
                Nếu không chắc giống cụ thể, vẫn ghi nhóm nhận diện rõ ràng hơn thay vì để trống, ví dụ: Rắn chưa xác định giống.
                """,
                input = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "input_text", text = "Hãy nhận diện loài và giống/nhóm giống cụ thể trong ảnh. Trả JSON đúng mẫu: {\"species\":\"Loài tiếng Việt có dấu\",\"breed\":\"Giống hoặc nhóm giống cụ thể tiếng Việt có dấu\",\"note\":\"ghi chú ngắn tiếng Việt\"}" },
                            new { type = "input_image", image_url = imageDataUrl }
                        }
                    }
                }
            };

            var text = await PostResponsesAsync(payload, cancellationToken);
            var parsed = TryReadJsonObject(text);

            return NormalizePetImageClassification(new PetImageClassification(
                ReadString(parsed, "species"),
                ReadString(parsed, "breed"),
                ReadString(parsed, "note")));
        }

        public async Task<ContentModerationResult> CheckContentAsync(string content, IFormFile? imageFile = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new ContentModerationResult(false, false, string.Empty);

            var localResult = CheckLocalCommunityViolation(content);

            if (!IsConfigured)
                return localResult;

            var flagged = false;
            var moderationReason = string.Empty;

            try
            {
                var moderationPayload = new
                {
                    model = _options.ModerationModel,
                    input = content
                };

                using var moderationResponse = await SendJsonAsync("/v1/moderations", moderationPayload, cancellationToken);
                var moderationJson = await moderationResponse.Content.ReadAsStringAsync(cancellationToken);
                using var moderationDoc = JsonDocument.Parse(moderationJson);
                var result = moderationDoc.RootElement.GetProperty("results")[0];
                flagged = result.TryGetProperty("flagged", out var flaggedElement) && flaggedElement.GetBoolean();

                if (flagged && result.TryGetProperty("categories", out var categories))
                {
                    var names = categories.EnumerateObject()
                        .Where(x => x.Value.ValueKind == JsonValueKind.True)
                        .Select(x => x.Name)
                        .ToList();
                    moderationReason = names.Count > 0 ? string.Join(", ", names) : "Noi dung co dau hieu vi pham.";
                }
            }
            catch
            {
                moderationReason = string.Empty;
            }

            var spamResult = await ClassifySpamAsync(content, cancellationToken);
            var isSpam = spamResult.IsSpam || localResult.IsSpam;
            var reason = string.Join("; ", new[] { localResult.Reason, moderationReason, spamResult.Reason }.Where(x => !string.IsNullOrWhiteSpace(x)));

            return new ContentModerationResult(flagged || localResult.IsFlagged, isSpam, reason);
        }

        private static ContentModerationResult CheckLocalCommunityViolation(string content)
        {
            var normalized = RemoveDiacritics(content).ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"[^\p{L}\p{N}\s]", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            var petTerms = new[]
            {
                "cho", "meo", "thu cung", "vat nuoi", "cun", "cún", "mieu", "miu", "pet"
            };

            var violencePatterns = new[]
            {
                @"\b(muon|se|sap|can|phai|tinh|dinh)\s+(danh|dam|dap|da|tat|bat|hanh ha|nguoc dai|tra tan|giet|chem)\b",
                @"\b(danh|dam|dap|da|tat|bat|hanh ha|nguoc dai|tra tan|giet|chem)\s+(no|cho|meo|thu cung|vat nuoi|cun|miu|con)\b",
                @"\b(dam chet|dap chet|danh chet|da chet|cho no nho doi|thanh cho no nho doi)\b"
            };

            var hasPetContext = petTerms.Any(term => normalized.Contains(RemoveDiacritics(term).ToLowerInvariant()));
            var hasViolenceIntent = violencePatterns.Any(pattern => Regex.IsMatch(normalized, pattern));

            if (hasPetContext && hasViolenceIntent)
            {
                return new ContentModerationResult(
                    true,
                    false,
                    "Noi dung co dau hieu bao luc hoac nguoc dai thu cung.");
            }

            return new ContentModerationResult(false, false, string.Empty);
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(capacity: normalized.Length);

            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category != UnicodeCategory.NonSpacingMark)
                    builder.Append(character);
            }

            return builder.ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd')
                .Replace('Đ', 'D');
        }

        private static PetImageClassification NormalizePetImageClassification(PetImageClassification result)
        {
            return new PetImageClassification(
                CleanAiText(result.Species),
                CleanAiText(result.Breed),
                CleanAiText(result.Note));
        }

        private static string? CleanAiText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Regex.Replace(value.Trim(), @"\s+", " ");
        }

        private async Task<(bool IsSpam, string Reason)> ClassifySpamAsync(string content, CancellationToken cancellationToken)
        {
            var payload = new
            {
                model = _options.Model,
                instructions = "Ban la bo loc bai viet mang xa hoi thu cung. Chi tra ve JSON hop le.",
                input = $$"""
                Kiem tra bai viet sau co phai spam/quang cao rac/lua dao/noi dung vi pham cong dong khong.
                Tra ve JSON: {"isSpam":true/false,"reason":"ly do ngan bang tieng Viet"}

                Noi dung: {{content}}
                """
            };

            try
            {
                var text = await PostResponsesAsync(payload, cancellationToken);
                var parsed = TryReadJsonObject(text);
                var isSpam = parsed.TryGetProperty("isSpam", out var spamElement) && spamElement.ValueKind == JsonValueKind.True;
                return (isSpam, ReadString(parsed, "reason") ?? string.Empty);
            }
            catch
            {
                return (false, string.Empty);
            }
        }

        private async Task<string> CreateTextResponseAsync(string instructions, string input, CancellationToken cancellationToken)
        {
            if (!IsConfigured)
                return "Chua cau hinh OpenAI API key. Hay them OpenAI:ApiKey hoac bien moi truong OPENAI_API_KEY de su dung tinh nang AI.";

            var payload = new
            {
                model = _options.Model,
                instructions,
                input
            };

            return await PostResponsesAsync(payload, cancellationToken);
        }

        private async Task<string> PostResponsesAsync(object payload, CancellationToken cancellationToken)
        {
            using var response = await SendJsonAsync("/v1/responses", payload, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.TryGetProperty("output_text", out var outputText))
                return outputText.GetString() ?? string.Empty;

            if (document.RootElement.TryGetProperty("output", out var output))
            {
                foreach (var item in output.EnumerateArray())
                {
                    if (!item.TryGetProperty("content", out var content)) continue;
                    foreach (var contentItem in content.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("text", out var text))
                            return text.GetString() ?? string.Empty;
                    }
                }
            }

            return string.Empty;
        }

        private async Task<HttpResponseMessage> SendJsonAsync(string path, object payload, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"OpenAI request failed ({(int)response.StatusCode}): {ExtractOpenAiError(body)}");
            }
            return response;
        }

        private static string ExtractOpenAiError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "No response body.";

            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("error", out var error))
                {
                    var message = ReadString(error, "message");
                    var code = ReadString(error, "code");
                    return string.Join(" - ", new[] { code, message }.Where(x => !string.IsNullOrWhiteSpace(x)));
                }
            }
            catch
            {
                return body.Length > 300 ? body[..300] : body;
            }

            return body.Length > 300 ? body[..300] : body;
        }

        private static async Task<string> ToDataUrlAsync(IFormFile imageFile, CancellationToken cancellationToken)
        {
            await using var stream = imageFile.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            return $"data:{imageFile.ContentType};base64,{Convert.ToBase64String(memory.ToArray())}";
        }

        private static JsonElement TryReadJsonObject(string text)
        {
            var cleaned = text.Trim();
            if (cleaned.StartsWith("```"))
            {
                cleaned = cleaned.Trim('`').Trim();
                if (cleaned.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                    cleaned = cleaned[4..].Trim();
            }

            var start = cleaned.IndexOf('{');
            var end = cleaned.LastIndexOf('}');
            if (start >= 0 && end > start)
                cleaned = cleaned[start..(end + 1)];

            using var document = JsonDocument.Parse(cleaned);
            return document.RootElement.Clone();
        }

        private static string? ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}
