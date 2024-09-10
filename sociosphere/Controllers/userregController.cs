using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sociosphere.Data;
using sociosphere.Models;
using System.Data;
using System.Net.Mail;
using System.Net;

namespace sociosphere.Controllers
{
    public class userregController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public userregController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(userregphoto v)
        {

            string userRole = HttpContext.Session.GetString("Role");
            if (userRole != "Admin")
            {
                return RedirectToAction("Login", "userreg");
            }

            var path = _environment.WebRootPath; //getting wwwroot folder location
            var filePath = "Content/Images/" + v.photo.FileName; //placing the image
            var fullPath = Path.Combine(path, filePath); //combine the path with wwwroot folder
            UploadFile(v.photo, fullPath);

            var p = new userreg()
            {
                photo = filePath,
                name = v.name,
                email = v.email,
                city = v.city,
                role = "User",
                password = v.password
            };
            _context.Add(p);
            _context.SaveChanges();
            SendRegistrationEmail(v.name, v.email, v.password);
            return RedirectToAction("Register", "userreg");
        }

        public void UploadFile(IFormFile file, string path)
        {
            using var stream = new FileStream(path, FileMode.Create);
            file.CopyTo(stream);
        }


        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> Login(string emailOrname, string password)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.userregs
                    .FirstOrDefaultAsync(u =>
                        (u.email == emailOrname || u.name == emailOrname) &&
                        u.password == password); // In production, hash and compare hashed passwords

                if (user != null)
                {
                    var userFlatDetails = await _context.alloteflats
                     .FirstOrDefaultAsync(f => f.name == user.name);

                    if (userFlatDetails != null)
                    {
                        HttpContext.Session.SetString("WingName", userFlatDetails.wingname);
                        HttpContext.Session.SetString("FlatNo", userFlatDetails.flatno.ToString());
                    }



                    HttpContext.Session.SetString("Role", user.role);
                    HttpContext.Session.SetString("name", user.name);
                    HttpContext.Session.SetString("Email", user.email);
                    HttpContext.Session.SetString("PHOTO", user.photo);

                    switch (user.role)
                    {
                        case "Admin":
                            return RedirectToAction("Dashboard", "Admin");
                        case "User":
                            return RedirectToAction("ViewAnnouncements", "User");
                        default:
                            return RedirectToAction("Index", "Home");
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                }
            }

            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        private void SendRegistrationEmail(string name, string email, string password)
        {
            try
            {
                var fromAddress = new MailAddress("pruthvirajgavali2001@gmail.com", "SocioSphere");
                var toAddress = new MailAddress(email, name);
                const string fromPassword = "zuoddhlazdzterrq";
                const string subject = "Welcome to SocioSphere";
                string body = $"Dear {name},\n\nYour registration was successful.\n\nEmail: {email}\nPassword: {password}\n\n" +
                  "Please do not reply to this mail as it is a computer-generated mail. If you have any query, meet in the office.\n\nRegards,\nSocioSphere Team";

                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com", // Replace with your SMTP server
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body
                })
                {
                    smtp.Send(message);
                }
            }
            catch (Exception ex)
            {
                // Log exception or handle error
                Console.WriteLine($"Email sending failed: {ex.Message}");
            }
        }

        [HttpGet]
        public IActionResult GetAllUsers()
        {
            var data = _context.userregs.ToList();
            return View(data);
        }

		[HttpGet]
		public JsonResult GetLatestUsers()
		{
			var data = _context.userregs
							   .OrderByDescending(u => u.Id) // Assuming Id is auto-incremented, you can use a DateTime field if available
							   .Take(5)
							   .ToList();
			return Json(data);
		}
 

		[HttpGet]
        public IActionResult Edit(int id)
        {
            var user = _context.userregs.Find(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpPost]
        public IActionResult Edit(int id, userreg updatedUser)
        {
            if (id != updatedUser.Id)
            {
                return BadRequest();
            }

            var existingUser = _context.userregs.Find(id);
            if (existingUser == null)
            {
                return NotFound();
            }

            // Update user details
            existingUser.name = updatedUser.name;
            existingUser.email = updatedUser.email;
            existingUser.city = updatedUser.city;
            //existingUser.role = updatedUser.role;
            existingUser.password = updatedUser.password;

            _context.SaveChanges();
            return RedirectToAction("GetAllUsers");
        }
        [HttpPost]
        public IActionResult Delete(int id)
        {
            var user = _context.userregs.Find(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found!";
                return RedirectToAction("GetAllUsers");
            }

            _context.userregs.Remove(user);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "User deleted successfully!";
            return RedirectToAction("GetAllUsers");
        }

		[HttpGet]
		public IActionResult ProfileEdit()
		{
			string email = HttpContext.Session.GetString("Email");
			if (email == null)
			{
				return RedirectToAction("Login", "userreg");
			}

			var user = _context.userregs.FirstOrDefault(u => u.email == email);
			if (user == null)
			{
				return NotFound();
			}

			return View(user);
		}

		

		


	}
}


