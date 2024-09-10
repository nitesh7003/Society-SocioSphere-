using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sociosphere.Data;
using sociosphere.Models;
using System.Diagnostics;

namespace sociosphere.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // In your HomeController
        public IActionResult about()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Submit(ContactFormModel model)
        {
            if (ModelState.IsValid)
            {
                // Save the details to the database
                _context.contactForms.Add(model);
                _context.SaveChanges();

                // Redirect to a success page or display a success message
                TempData["SuccessMessage"] = "Your message has been sent successfully!";
                return RedirectToAction("Index");
            }

            // If the model is not valid, return the same view with validation errors
            return View("Index", model);
        }

    }
}
