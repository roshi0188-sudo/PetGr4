using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetSocial.Services;

namespace PetSocial.Controllers
{
    [Authorize]
    public class AiController : Controller
    {
        private readonly IPetAiService _petAiService;
        private readonly ILogger<AiController> _logger;

        public AiController(IPetAiService petAiService, ILogger<AiController> logger)
        {
            _petAiService = petAiService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            ViewData["FullWidth"] = true;
            ViewData["ActiveMenu"] = "Ai";
            ViewBag.IsAiConfigured = _petAiService.IsConfigured;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ask(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return Json(new { success = false, message = "Vui lòng nhập câu hỏi." });

            try
            {
                var answer = await _petAiService.AskCareQuestionAsync(question);
                return Json(new { success = true, answer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI ask request failed");
                return Json(new { success = false, message = GetFriendlyAiError(ex) });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Diet(string species, string? breed, int? age, decimal? weight)
        {
            if (string.IsNullOrWhiteSpace(species))
                return Json(new { success = false, message = "Vui lòng nhập loài thú cưng." });

            try
            {
                var answer = await _petAiService.SuggestDietAsync(species, breed, age, weight);
                return Json(new { success = true, answer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI diet request failed");
                return Json(new { success = false, message = GetFriendlyAiError(ex) });
            }
        }

        private static string GetFriendlyAiError(Exception ex)
        {
            var message = ex.Message;
            if (message.Contains("401") || message.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase))
                return "OpenAI API key không hợp lệ. Hãy kiểm tra lại key trong appsettings.json rồi khởi động lại web.";
            if (message.Contains("429") || message.Contains("quota", StringComparison.OrdinalIgnoreCase))
                return "Tài khoản OpenAI đang hết quota hoặc bị giới hạn tốc độ.";
            if (message.Contains("model", StringComparison.OrdinalIgnoreCase))
                return "Model OpenAI đang cấu hình không dùng được với tài khoản này.";

            return "Không thể kết nối AI. Hãy kiểm tra mạng, API key và khởi động lại web.";
        }
    }
}
