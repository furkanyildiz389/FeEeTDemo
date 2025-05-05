using BusinessLayer.Abstract;
using BusinessLayer.Concrete;
using DataAccessLayer.Concrete;
using DataAccessLayer.EntityFramework;
using EntityLayer.Concrete;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using FeEeTDemo.Models;
using System.Linq;

namespace FeEeTDemo.Controllers
{
    public class UserController : Controller
    {
        EventManager em = new EventManager(new EfEventRepository());
        //profil
        public IActionResult Index()
        {
            var _usermail = User.Identity.Name;
            Context c = new Context();
            //var _username = c.Users.Where(x => x.Email == _usermail).Select(y => y.Username).FirstOrDefault();
            var user = c.Users.FirstOrDefault(x => x.Email == _usermail);

            if (user != null)
            {
                ViewData["UserId"] = user.Id;
                ViewData["Username"] = user.Username;
                ViewData["Email"] = user.Email;
                ViewData["Password"] = "**********";
                ViewData["CreatedAt"] = user.CreatedAt.ToString("yyyy-MM-dd");

            }
            return View();
        }
        //listeleme
        public async Task<IActionResult> EventList()
        {
            var userEmail = User.Identity.Name;
            using (var c = new Context())
            {
                var user = c.Users.FirstOrDefault(x => x.Email == userEmail);
                if (user != null)
                {
                    var userEvents = em.GetEventsByUserId(user.Id);
                    return View(userEvents);
                }
            }
            return RedirectToAction("Index", "Home");
        }
        //oluşturma

        [HttpGet]
        public IActionResult CreateEvent()
        {
            return View(new CreateEventViewModel
            {
                DateTimeOptions = new List<DateTime> { DateTime.Now }
            });
        }
        [HttpPost]
        public async Task<IActionResult> CreateEvent(CreateEventViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false });

            using (var c = new Context())
            {
                var currentUser = c.Users.FirstOrDefault(x => x.Email == User.Identity.Name);
                if (currentUser == null)
                    return Json(new { success = false });

                var newEvent = new Event
                {
                    Title = model.Title,
                    Description = model.Description,
                    Location = model.Location,
                    CreatedAt = DateTime.Now,
                    CreatedById = currentUser.Id,
                    IsActive = true,
                    VotingDeadline = model.VotingDeadline,
                    DateTimeOptions = model.DateTimeOptions.Select(dto => new DateTimeOption
                    {
                        DateTime = dto
                    }).ToList()
                };

                c.Events.Add(newEvent);
                await c.SaveChangesAsync();

                return Json(new { success = true, redirectUrl = Url.Action("EventDetails", new { id = newEvent.Id }) });
            }
        }


        //detaylar
        public async Task<IActionResult> EventDetails(int id)
        {
            var currentUserEmail = User.Identity.Name;
            using (var c = new Context())
            {
                var currentUser = c.Users.FirstOrDefault(x => x.Email == currentUserEmail);
                if (currentUser == null) return RedirectToAction("Index", "Login");

                var eventDetails = em.GetEventDetailsById(id);
                if (eventDetails == null) return NotFound();

                ViewData["CurrentUserId"] = currentUser.Id;
                return View(eventDetails);
            }
        }



        //düzenleme 
        [HttpGet]
        public IActionResult EditEvent(int id)
        {
            var currentUserEmail = User.Identity.Name;
            using (var c = new Context())
            {
                var currentUser = c.Users.FirstOrDefault(x => x.Email == currentUserEmail);
                if (currentUser == null) return RedirectToAction("Index", "Login");

                var event_ = em.GetByID_DTO(id);
                if (event_ == null || event_.CreatedById != currentUser.Id)
                    return RedirectToAction("EventList");

                return View(event_);
            }
        }

        [HttpPost]
        public IActionResult EditEvent(Event event_)
        {
            // Kullanıcının oturum bilgisini al
            var currentUserEmail = User.Identity.Name;

            using (var context = new Context())
            {
                // Mevcut kullanıcıyı al
                var currentUser = context.Users.FirstOrDefault(x => x.Email == currentUserEmail);
                if (currentUser == null) return RedirectToAction("Index", "Login"); // Kullanıcı yoksa login sayfasına yönlendir

                // Düzenlenecek etkinliği veritabanından getir
                var existingEvent = context.Events
                    .Include(e => e.DateTimeOptions)
                    .ThenInclude(dto => dto.SurveyResponses)
                    .FirstOrDefault(e => e.Id == event_.Id);

                if (existingEvent == null || existingEvent.CreatedById != currentUser.Id)
                    return RedirectToAction("EventList"); // Etkinlik mevcut değilse veya kullanıcı yetkili değilse listeye yönlendir


                // Mevcut ve güncellenen tarih seçeneklerinin ID'lerini al
                var existingDateTimeOptionIds = existingEvent.DateTimeOptions.Select(dto => dto.Id).ToList();
                var updatedDateTimeOptionIds = event_.DateTimeOptions.Select(dto => dto.Id).ToList();

                // Silinmesi gereken tarih seçeneklerini tespit et
                var deletedDateTimeOptionIds = existingDateTimeOptionIds.Except(updatedDateTimeOptionIds).ToList();
                foreach (var deletedId in deletedDateTimeOptionIds)
                {
                    var dateTimeOption = context.DateTimeOptions
                        .Include(dto => dto.SurveyResponses)
                        .FirstOrDefault(dto => dto.Id == deletedId);

                    if (dateTimeOption != null)
                    {
                        // Önce bağlı survey yanıtlarını sil
                        context.SurveyResponses.RemoveRange(dateTimeOption.SurveyResponses);
                        // Daha sonra tarih seçeneğini sil
                        context.DateTimeOptions.Remove(dateTimeOption);
                    }
                }

                // Yeni eklenen tarih seçeneklerini tespit et ve ekle
                var newDateTimeOptions = event_.DateTimeOptions.Where(dto => dto.Id == 0).ToList();
                foreach (var newDateTimeOption in newDateTimeOptions)
                {
                    existingEvent.DateTimeOptions.Add(new DateTimeOption
                    {
                        DateTime = newDateTimeOption.DateTime,
                        EventId = existingEvent.Id
                    });
                }

                foreach (var updatedDateTimeOption in event_.DateTimeOptions.Where(dto => dto.Id != 0))
                {
                    var existingOption = existingEvent.DateTimeOptions.FirstOrDefault(dto => dto.Id == updatedDateTimeOption.Id);
                    if (existingOption != null)
                    {
                        existingOption.DateTime = updatedDateTimeOption.DateTime; // Tarih güncellemesi
                    }
                    else
                    {
                        // Eğer var olan tarih seçeneği bulunamazsa, hata mesajı ekleyebiliriz
                        ModelState.AddModelError("", "Güncellenmek istenen tarih seçeneği bulunamadı.");
                    }
                }

                var dateTimeSet = new HashSet<DateTime>();
                foreach (var option in event_.DateTimeOptions)
                {
                    if (!dateTimeSet.Add(option.DateTime))
                    {
                        return Json(new { success = false, message = "Aynı tarih ve saat birden fazla kez seçilemez." });
                    }
                }

                // Etkinlik bilgilerini güncelle
                existingEvent.Title = event_.Title;
                existingEvent.Description = event_.Description;
                existingEvent.Location = event_.Location;
                existingEvent.IsActive = event_.IsActive;
                existingEvent.VotingDeadline = event_.VotingDeadline;

                try
                {
                    // Değişiklikleri kaydet
                    context.SaveChanges();
                    return Json(new { success = true });
                }

                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Düzenleme sırasında bir hata oluştu." });
                }
            }
        }

        //silme
        public IActionResult DeleteEvent(int id)
        {
            var currentUserEmail = User.Identity.Name;
            using (var c = new Context())
            {
                var currentUser = c.Users.FirstOrDefault(x => x.Email == currentUserEmail);
                if (currentUser == null) return RedirectToAction("Index", "Login");

                var event_ = c.Events
                    .Include(e => e.DateTimeOptions)
                        .ThenInclude(dto => dto.SurveyResponses)
                    .FirstOrDefault(e => e.Id == id);

                if (event_ != null && event_.CreatedById == currentUser.Id)
                {
                    foreach (var option in event_.DateTimeOptions)
                    {
                        c.SurveyResponses.RemoveRange(option.SurveyResponses);
                    }
                    c.DateTimeOptions.RemoveRange(event_.DateTimeOptions);
                    c.Events.Remove(event_);
                    c.SaveChanges();
                }
            }
            return RedirectToAction("EventList");
        }

        [HttpPost]
        public async Task<IActionResult> SubmitVote(int dateTimeOptionId)
        {
            var currentUserEmail = User.Identity.Name;
            using (var c = new Context())
            {
                var currentUser = c.Users.FirstOrDefault(x => x.Email == currentUserEmail);
                if (currentUser == null)
                    return Json(new { success = false, message = "Kullanıcı bulunamadı." });

                var dateTimeOption = c.DateTimeOptions
                    .Include(dto => dto.Event)
                    .FirstOrDefault(dto => dto.Id == dateTimeOptionId);

                if (dateTimeOption == null || !dateTimeOption.Event.IsActive)
                    return Json(new { success = false, message = "Etkinlik pasif." });

                if (dateTimeOption.Event.VotingDeadline < DateTime.Now)
                    return Json(new { success = false, message = "Oylama süresi dolmuştur." });

                var existingVote = c.SurveyResponses
                    .FirstOrDefault(sr => sr.UserId == currentUser.Id && sr.DateTimeOptionId == dateTimeOptionId);

                if (existingVote != null)
                {
                    c.SurveyResponses.Remove(existingVote);
                }
                else
                {
                    var newVote = new SurveyResponse
                    {
                        UserId = currentUser.Id,
                        DateTimeOptionId = dateTimeOptionId
                    };
                    c.SurveyResponses.Add(newVote);
                }

                await c.SaveChangesAsync();

                var voteCount = c.SurveyResponses.Count(sr => sr.DateTimeOptionId == dateTimeOptionId);
                var usersVoted = c.SurveyResponses
                    .Where(sr => sr.DateTimeOptionId == dateTimeOptionId)
                    .Select(sr => sr.User.Username)
                    .ToList();

                return Json(new
                {
                    success = true,
                    voteCount = voteCount,
                    usersVoted = usersVoted
                });
            }
        }
        //çıkış
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home"); // Giriş sayfasına yönlendirme
        }
    }
}
