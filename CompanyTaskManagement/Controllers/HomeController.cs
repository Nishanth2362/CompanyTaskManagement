using CompanyTaskManagement.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CompanyTaskManagement.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Task");
        }

        public IActionResult Privacy()
        {
            return RedirectToAction("Index", "Task");
        }

        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
