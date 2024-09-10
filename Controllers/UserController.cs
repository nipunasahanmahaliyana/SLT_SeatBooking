using Microsoft.AspNetCore.Mvc;

namespace SLT_SeatBooking.Controllers
{
    public class UserController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }
    }
   
}
