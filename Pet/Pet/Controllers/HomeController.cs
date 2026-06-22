using Microsoft.AspNetCore.Mvc;

namespace PetSocial.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Splash()
        {
            return View();
        }
        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin"))
                {

                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                }
                else
                {
       
                    return RedirectToAction("Index", "Post");
                }
            }

            // Nếu chưa đăng nhập thì hiện Landing Page
            return View();
        }
    }
}
