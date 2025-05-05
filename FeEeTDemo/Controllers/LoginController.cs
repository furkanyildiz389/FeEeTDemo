using DataAccessLayer.Concrete;
using EntityLayer.Concrete;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FeEeTDemo.Controllers
{
    [AllowAnonymous]
    public class LoginController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(User p)
        {
            Context c = new Context();
            var user = c.Users.FirstOrDefault(x => x.Email == p.Email);

            if (user == null)
            {
                // E-posta bulunamadı
                ModelState.AddModelError("Email", "E-posta adresi bulunamadı");
            }
            else if (user.Password != p.Password)
            {
                // Şifre yanlış
                ModelState.AddModelError("Password", "Şifre yanlıştır");
            }
            else
            {
                // Giriş başarılı
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Email)
                };
                var useridentity = new ClaimsIdentity(claims, "a");
                ClaimsPrincipal principal = new ClaimsPrincipal(useridentity);

                await HttpContext.SignInAsync(principal);
                return RedirectToAction("Index", "User");
            }

            // Hata durumunda model ile birlikte geri dön
            return View(p);
        }
    }
}
