using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using sociosphere.Data;
using sociosphere.Models;
using System.Globalization;
using System.Text;


namespace sociosphere.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext db;
        public AdminController(ApplicationDbContext db)
        {
            this.db = db;
        }

        public IActionResult AddFlat()
        {
            return View();
        }

        // POST: Admin/AddFlat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFlat(addflat flat)
        {
            string userRole = HttpContext.Session.GetString("Role");
            if (userRole != "Admin")
            {
                return RedirectToAction("Login", "userreg");
            }
            if (ModelState.IsValid)
            {

                db.Add(flat);
                await db.SaveChangesAsync();
                //return RedirectToAction(nameof(FlatSuccess)); // Redirect to success page or list view
            }
            return View(flat);
        }
        public async Task<IActionResult> AlloteFlat()
        {

            var wingNames = await db.addflats.Select(f => f.wingname).Distinct().ToListAsync();
            var userNames = await db.userregs
                        .Where(u => u.name != "Admin")
                        .Select(u => u.name)
                        .ToListAsync();

            ViewBag.WingNames = new SelectList(wingNames);
            ViewBag.UserNames = new SelectList(userNames);

            return View();
        }

        [HttpGet]
        public async Task<JsonResult> GetFloorNumbers(string wingName)
        {
           
            var floorNumbers = await db.addflats
                .Where(f => f.wingname == wingName)
                .Select(f => f.floorno)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync();

            return Json(floorNumbers);
        }

        [HttpGet]
        public async Task<JsonResult> GetFlatNumbers(string wingName, int floorNo)
        {
           
            var flatNumbers = await db.addflats
                .Where(f => f.wingname == wingName && f.floorno == floorNo)
                .Select(f => new { f.flatno, f.flattype })
                .ToListAsync();

            return Json(flatNumbers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlloteFlat(alloteflat model)
        {
            string userRole = HttpContext.Session.GetString("Role");
            if (userRole != "Admin")
            {
                return RedirectToAction("Login", "userreg");
            }
            if (ModelState.IsValid)
            {
                model.allotdate = DateTime.Now;
                db.Add(model);
                await db.SaveChangesAsync();
                return RedirectToAction(nameof(AlloteFlat)); // Or wherever you want to redirect after success
            }
            return View(model);
        }

        //  -------------------- Complaint -------------------
        //public IActionResult ViewComplaints()
        //{
        //    string userRole = HttpContext.Session.GetString("Role");
        //    if (userRole != "Admin")
        //    {
        //        return RedirectToAction("Login", "userreg");
        //    }
        //    var complaints = db.addcomplaints.ToList();
        //    return View(complaints);
        //}

        //[HttpPost]
        //public async Task<IActionResult> ResolveComplaint(int id)
        //{

        //    string userRole = HttpContext.Session.GetString("Role");
        //    if (userRole != "Admin")
        //    {
        //        return RedirectToAction("Login", "userreg");
        //    }
        //    var complaint = await db.addcomplaints.FindAsync(id);
        //    if (complaint != null)
        //    {
        //        complaint.complaintstatus = "Resolved";
        //        complaint.resolvedate = DateTime.Now;
        //        await db.SaveChangesAsync();
        //    }

        //    return RedirectToAction("ViewComplaints");
        //}


        // ----------------- Announcement -------------------

        [HttpGet]
        public IActionResult AddAnnouncement()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddAnnouncement(AnnouncementViewModel model)
        {
            string userRole = HttpContext.Session.GetString("Role");
            if (userRole != "Admin")
            {
                return RedirectToAction("Login", "userreg");
            }

            if (ModelState.IsValid)
            {
                var announcement = new announcement
                {
                    Announcement = model.Announcement,
                    AnnounceDate = DateTime.Now
                };

                db.announcements.Add(announcement);
                db.SaveChanges();
                return RedirectToAction("ViewAnnouncements");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ViewAnnouncements()
        {

            string userRole = HttpContext.Session.GetString("Role");
            if (userRole != "Admin")
            {
                return RedirectToAction("Login", "userreg");
            }
            // Automatically delete announcements older than 24 hours
            var announcements = db.announcements.ToList();
            var expiredAnnouncements = announcements.Where(a => a.AnnounceDate < DateTime.Now.AddHours(-24)).ToList();
            db.announcements.RemoveRange(expiredAnnouncements);
            db.SaveChanges();

            var activeAnnouncements = db.announcements.Where(a => a.AnnounceDate >= DateTime.Now.AddHours(-24)).ToList();
            return View(activeAnnouncements);
        }


        public IActionResult Dashboard()
        {

            string userRole = HttpContext.Session.GetString("Role");
            if (userRole != "Admin")
            {
                return RedirectToAction("Login", "userreg");
            }
            var pendingComplaintsCount = db.addcomplaints.Count(c => c.complaintstatus == "Pending");
            var avalibalevisitors = db.gatemanagements.Count(c => c.Status == "In");

            var dashboard = new Dashboard
            {
                TotalComplaints = pendingComplaintsCount, // Count only pending complaints
                TotalFlats = db.addflats.Count(),
                TotalAllotedFlats = db.alloteflats.Count(),
                TotalAnnouncements = db.announcements.Count(),
                TotalVisitors = db.gatemanagements.Count(),
                TotalUsers = db.userregs.Count(),
                PendingComplaints = pendingComplaintsCount, // Assign pending complaints count
                avalibalevisitors = avalibalevisitors
            };

            return View(dashboard);
        }

        [HttpGet]
        public JsonResult GetAnnouncementCount()
        {
            var count = db.announcements.Count(a => a.AnnounceDate >= DateTime.Now.AddHours(-24));
            return Json(count);
        }


        // -------------------- Add Bill -------------------

        [HttpGet]
        public async Task<IActionResult> AddBill()
        {
            ViewBag.WingNames = new SelectList(await db.alloteflats.Select(x => x.wingname).Distinct().ToListAsync(), "WingName");
            ViewBag.FlatNos = new SelectList(Enumerable.Empty<int>(), "FlatNo");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddBill(billmanagement bill)
        {
            if (ModelState.IsValid)
            {
                bill.BillReleaseDt = DateTime.Now;
                db.billmanagements.Add(bill);
                await db.SaveChangesAsync();
                return RedirectToAction("AddBill");
            }

            ViewBag.WingNames = new SelectList(await db.alloteflats.Select(x => x.wingname).Distinct().ToListAsync(), "WingName");
            ViewBag.FlatNos = new SelectList(Enumerable.Empty<int>(), "FlatNo");
            return View(bill);
        }

        [HttpGet]
        public async Task<JsonResult> GetFlatsByWing(string wingName)
        {
            var flats = await db.alloteflats
                .Where(x => x.wingname == wingName)
                .Select(x => x.flatno)
                .ToListAsync();

            return Json(new SelectList(flats));
        }

        ///      ---------------- REPORT ----------------- 

        [HttpGet]
        public IActionResult GenerateReport()
        {
            ViewBag.ReportTypes = new SelectList(new List<string> { "Bill", "Complaint", "Visitor" });
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GenerateReport(ReportViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (model.ReportFor == "Bill")
                {
                    var bills = await db.billmanagements
                        .Where(b => b.BillReleaseDt >= model.StartDate && b.BillReleaseDt <= model.EndDate)
                        .ToListAsync();
                    return View("ReportResultsBill", bills);
                }
                else if (model.ReportFor == "Complaint")
                {
                    var complaints = await db.addcomplaints
                        .Where(c => c.raisedate >= model.StartDate && c.raisedate <= model.EndDate)
                        .ToListAsync();
                    return View("ReportResultsComplaint", complaints);
                }
                else if (model.ReportFor == "Visitor")
                {
                    var visitors = await db.gatemanagements
                        .Where(v => v.InDateTime >= model.StartDate && v.InDateTime <= model.EndDate)
                        .ToListAsync();
                    return View("ReportResultsVisitor", visitors);
                }
            }

            ViewBag.ReportTypes = new SelectList(new List<string> { "Bill", "Complaint", "Visitor" });
            return View(model);
        }

        [HttpPost]
        public IActionResult ExportReportToCSV(string reportType)
        {
            var csv = new StringBuilder();

            if (reportType == "Bill")
            {
                var bills = db.billmanagements.ToList();
                csv.AppendLine("Bill Title,Flat Number,Bill Amount,Month,Paid Date,Status");

                foreach (var bill in bills)
                {
                    string formattedMonth = bill.Month.ToString("MMMM yyyy");
                    string formattedAmount = bill.AmountPay.ToString("F2", CultureInfo.InvariantCulture);
                    string formattedPaidDate = bill.BillSbmtDate?.ToString("dd-MM-yyyy");

                    csv.AppendLine($"{bill.Title},{bill.FlatNo},{formattedAmount},{formattedMonth},{formattedPaidDate},{bill.PaidStatus}");
                }
            }
            else if (reportType == "Complaint")
            {
                var complaints = db.addcomplaints.ToList();
                csv.AppendLine("Name,Flat Number,Complaint,Status,Raised Date,Resolved Date");

                foreach (var complaint in complaints)
                {
                    string formattedRaiseDate = complaint.raisedate.ToString("dd-MM-yyyy");
                    string formattedResolveDate = complaint.resolvedate?.ToString("dd-MM-yyyy");

                    csv.AppendLine($"{complaint.name},{complaint.flatno},{complaint.WriteComplaint},{complaint.complaintstatus},{formattedRaiseDate},{formattedResolveDate}");
                }
            }
            else if (reportType == "Visitor")
            {
                var visitors = db.gatemanagements.ToList();
                csv.AppendLine("Visitor Name,Flat Number,Wing Name,Phone,In DateTime,Out DateTime,Status");

                foreach (var visitor in visitors)
                {
                    string formattedInDateTime = visitor.InDateTime.ToString("dd-MM-yyyy HH:mm");
                    string formattedOutDateTime = visitor.OutDateTime?.ToString("dd-MM-yyyy HH:mm");

                    csv.AppendLine($"{visitor.VisitorName},{visitor.FlatNo},{visitor.WingName},{visitor.Phone},{formattedInDateTime},{formattedOutDateTime},{visitor.Status}");
                }
            }

            var fileBytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(fileBytes, "text/csv", $"{reportType}_Report.csv");
        }


        [HttpGet]
        public IActionResult GetAllFlat()
        {
            var data = db.addflats.ToList();
            return View(data);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var user = db.addflats.Find(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpPost]
        public IActionResult Edit(int id, addflat updatedFlat)
        {
            if (id != updatedFlat.Id)
            {
                return BadRequest();
            }

            var existingFlat = db.addflats.Find(id);
            if (existingFlat == null)
            {
                return NotFound();
            }

            // Update user details
            existingFlat.flatno = updatedFlat.flatno;
            existingFlat.floorno = updatedFlat.floorno;
            existingFlat.wingname = updatedFlat.wingname;
            //existingUser.role = updatedUser.role;
            existingFlat.flattype = updatedFlat.flattype;

            db.SaveChanges();
            return RedirectToAction("GetAllFlat");
        }
        [HttpPost]
        public IActionResult Delete(int id)
        {
            var flat = db.addflats.Find(id);
            if (flat == null)
            {
                TempData["ErrorMessage"] = "flat not found!";
                return RedirectToAction("GetAllFlat");
            }

            db.addflats.Remove(flat);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Flat deleted successfully!";
            return RedirectToAction("GetAllFlat");
        }


        //Allotment curd operations

        [HttpGet]
        public IActionResult GetAlloteFlat()
        {
            var data = db.alloteflats.ToList();
            return View(data);
        }
        [HttpPost]
        public IActionResult DeleteAlloteFlat(int id)
        {
            var allotflat = db.alloteflats.Find(id);
            if (allotflat == null)
            {
                TempData["ErrorMessage"] = "allotflat not found!";
                return RedirectToAction("GetAlloteFlat");
            }

            db.alloteflats.Remove(allotflat);
            db.SaveChanges();

            TempData["SuccessMessage"] = "alloteflats deleted successfully!";
            return RedirectToAction("GetAlloteFlat");


        }

        //[HttpGet]
        //public IActionResult EditAllot(int id)
        //{
        //    var allotflat = db.alloteflats.Find(id);
        //    if (allotflat == null)
        //    {
        //        return NotFound();
        //    }
        //    return View(allotflat);
        //}

        [HttpGet]
        public IActionResult EditAllot(int id)
        {
            var allotflat = db.alloteflats.Find(id);
            if (allotflat == null)
            {
                return NotFound();
            }
            return View(allotflat);
        }

        [HttpPost]
        public IActionResult EditAllot(int id, alloteflat updatedFlat)
        {
            if (id != updatedFlat.Id)
            {
                return BadRequest();
            }

            var existingFlat = db.alloteflats.Find(id);
            if (existingFlat == null)
            {
                return NotFound();
            }

            // Update the flat details
            existingFlat.name = updatedFlat.name;
            existingFlat.flatno = updatedFlat.flatno;
            existingFlat.floorno = updatedFlat.floorno;
            existingFlat.wingname = updatedFlat.wingname;
            existingFlat.flattype = updatedFlat.flattype;
            existingFlat.moveindate = updatedFlat.moveindate;
            existingFlat.allotdate = updatedFlat.allotdate;

            db.SaveChanges();
            return RedirectToAction("GetAlloteFlat"); // Redirect to the appropriate page
        }

        [HttpGet]
        public IActionResult GetNotifications()
        {
            var announcements = db.announcements
                .Where(a => a.AnnounceDate >= DateTime.Now.AddHours(-24))
                .OrderByDescending(a => a.AnnounceDate)
                .ToList();

            var complaints = db.addcomplaints
                .Where(c => c.complaintstatus == "Pending" && c.raisedate >= DateTime.Now.AddHours(-24))
                .OrderByDescending(c => c.raisedate)
                .ToList();

            return PartialView("_AdminNotificationsPartial", Tuple.Create(announcements, complaints));
        }

        [HttpGet]
        public JsonResult GetNotificationCount()
        {
            var announcementCount = db.announcements.Count(a => a.AnnounceDate >= DateTime.Now.AddHours(-24));
            var complaintCount = db.addcomplaints.Count(c => c.complaintstatus == "Pending" && c.raisedate >= DateTime.Now.AddHours(-24));

            return Json(announcementCount + complaintCount);
        }

        // ... other actions remain the same ...

        public IActionResult ViewComplaints()
        {
            string userRole = HttpContext.Session.GetString("Role");
            if (userRole != "Admin")
            {
                return RedirectToAction("Login", "userreg");
            }
            var complaints = db.addcomplaints.OrderByDescending(c => c.raisedate).ToList();
            return View(complaints);
        }

        [HttpPost]
        public async Task<IActionResult> ResolveComplaint(int id)
        {
            string userRole = HttpContext.Session.GetString("Role");
            if (userRole != "Admin")
            {
                return RedirectToAction("Login", "userreg");
            }
            var complaint = await db.addcomplaints.FindAsync(id);
            if (complaint != null)
            {
                complaint.complaintstatus = "Resolved";
                complaint.resolvedate = DateTime.Now;
                await db.SaveChangesAsync();
            }
            return RedirectToAction("ViewComplaints");
        }

		[HttpGet]
		public async Task<IActionResult> GetBillStatusData()
		{
			var paidCount = await db.billmanagements.CountAsync(b => b.PaidStatus == "Paid");
			var pendingCount = await db.billmanagements.CountAsync(b => b.PaidStatus == "Pending");

			return Json(new { paidCount, pendingCount });
		}


	}
}
