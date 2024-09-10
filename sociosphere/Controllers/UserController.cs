using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Razorpay.Api;
using sociosphere.Data;
using sociosphere.Models;
using System.Drawing.Printing;
using System.Net.Mail;
using System.Xml.Linq;

namespace sociosphere.Controllers
{
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RazorpaySettings _razorpaySettings;
        private readonly EmailSettings _emailSettings;
		private readonly IWebHostEnvironment _environment;
		public UserController(ApplicationDbContext context, IOptions<RazorpaySettings> razorpaySettings, IOptions<EmailSettings> emailSettings, IWebHostEnvironment environment)
        {
            _context = context;
            _razorpaySettings = razorpaySettings.Value;
            _emailSettings = emailSettings.Value;
			_environment = environment;
		}


        [HttpGet]
        public IActionResult AddComplaint()
        {
            // Get the current user's name from the session
            var userName = HttpContext.Session.GetString("name");
            if (userName == null)
            {
                return RedirectToAction("Login", "userreg");
            }

            // Find the flat number associated with the logged-in user
            var userFlat = _context.alloteflats.FirstOrDefault(x => x.name == userName);

            // Populate the model
            var model = new addcomplaint
            {
                name = userName,
                flatno = userFlat?.flatno ?? 0
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddComplaint(addcomplaint model)
        {
            if (ModelState.IsValid)
            {
                _context.addcomplaints.Add(model);
                _context.SaveChanges();

                // return RedirectToAction("ComplaintSuccess"); // Redirect to a success page or list of complaints
            }

            return View(model);
        }

        // ------------- View Announcements -------------------

      

        [HttpGet]
        public async Task<IActionResult> ViewVisitors()
        {
            // Assume the user's wing name and flat number are stored in session
            string userWingName = HttpContext.Session.GetString("WingName");
            int userFlatNo = int.Parse(HttpContext.Session.GetString("FlatNo"));

            var visitors = await _context.gatemanagements
                .Where(v => v.WingName == userWingName && v.FlatNo == userFlatNo)
                .ToListAsync();

            return View(visitors);
        }


        [HttpGet]
        public IActionResult GetAnnouncements()
        {
            var announcements = _context.announcements
                .Where(a => a.AnnounceDate >= DateTime.Now.AddHours(-24) && !a.IsRead)
                .OrderByDescending(a => a.AnnounceDate)
                .ToList();
            return PartialView("_AnnouncementsPartial", announcements);
        }


        [HttpGet]
        public IActionResult ViewAnnouncements()
        {
            // Automatically delete announcements older than 24 hours
            var announcements = _context.announcements.ToList();
            var expiredAnnouncements = announcements.Where(a => a.AnnounceDate < DateTime.Now.AddHours(-24)).ToList();
            _context.announcements.RemoveRange(expiredAnnouncements);
            _context.SaveChanges();

            // Retrieve announcements that are still within the 24-hour period
            var activeAnnouncements = _context.announcements
                .Where(a => a.AnnounceDate >= DateTime.Now.AddHours(-24))
                .OrderByDescending(a => a.AnnounceDate)
                .ToList();
            return View(activeAnnouncements);
        }

        [HttpGet]
        public IActionResult ViewBills()
        {
            var userName = HttpContext.Session.GetString("name");
            var userFlatDetails = _context.alloteflats.FirstOrDefault(x => x.name == userName);
            if (userFlatDetails != null)
            {
                var userWingName = userFlatDetails.wingname;
                var userFlatNo = userFlatDetails.flatno;
                var userBills = _context.billmanagements
                                        .Where(b => b.WingName == userWingName && b.FlatNo == userFlatNo)
                                        .ToList();
                return View(userBills);
            }
            else
            {
                ModelState.AddModelError(string.Empty, "No flat details found for the user.");
                return View();
            }
        }

        [HttpPost]
        public IActionResult PayBill(int billId)
        {
            var bill = _context.billmanagements.Find(billId);
            if (bill == null || bill.PaidStatus == "Paid")
            {
                return RedirectToAction("ViewBills");
            }

            // Calculate the amount in paise (Razorpay expects the amount in the smallest currency unit)
            var amountInPaise = (int)(bill.AmountPay * 100);

            if (amountInPaise < 100)
            {
                // Handle case where amount is too low
                TempData["Error"] = "Invalid amount. Minimum value is 1 INR.";
                return RedirectToAction("ViewBills");
            }

            // Create Razorpay order
            var client = new RazorpayClient("rzp_test_Kl7588Yie2yJTV", "6dN9Nqs7M6HPFMlL45AhaTgp");
            var options = new Dictionary<string, object>
            {
                { "amount", amountInPaise },
                { "currency", "INR" },
                { "receipt", "rcptid_" + billId }
            };
            Order order = client.Order.Create(options);

            // Store the order ID in the ViewBag to use it in the payment form
            ViewBag.OrderId = order["id"].ToString();
            ViewBag.Amount = bill.AmountPay; // Pass the amount for display in INR
            ViewBag.BillId = billId;

            return View("PaymentPage");
        }


        // PaymentSuccess action: Called by Razorpay on successful payment
        [HttpPost]
        public IActionResult PaymentSuccess(string razorpayPaymentId, string razorpayOrderId, string razorpaySignature, int billId)
        {
            var bill = _context.billmanagements.FirstOrDefault(b => b.Id == billId);
            if (bill != null && bill.PaidStatus == "Pending")
            {
                // Update the bill status to "Paid"
                bill.PaidStatus = "Paid";
                bill.BillSbmtDate = DateTime.Now;
                _context.SaveChanges();

                SendInvoice(bill);

            }

            return RedirectToAction("ViewBills");
        }


        //private void SendInvoice(billmanagement bill)
        //{
        //    var userEmail = HttpContext.Session.GetString("Email");
        //    var userName = HttpContext.Session.GetString("name"); // Assuming you have the user's name in the session
        //    var flatNo = HttpContext.Session.GetString("FlatNo"); // Assuming you have the flat number in the session

        //    // Create invoice as a PDF file
        //    var pdfPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "invoices", $"Invoice_{bill.Id}.pdf");
        //    GeneratePDF(pdfPath, bill, userName, flatNo);

        //    // Send email
        //    using (MailMessage mail = new MailMessage())
        //    {
        //        mail.From = new MailAddress(_emailSettings.SenderEmail);
        //        mail.To.Add(userEmail);
        //        mail.Subject = "Your Invoice";
        //        mail.Body = "Please find your invoice attached.";
        //        mail.Attachments.Add(new Attachment(pdfPath));

        //        using (SmtpClient smtp = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort))
        //        {
        //            smtp.Credentials = new System.Net.NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword);
        //            smtp.EnableSsl = true;
        //            smtp.Send(mail);
        //        }
        //    }
        //}


        public FileResult SendInvoice(billmanagement bill, bool sendEmail = true)
        {
            var userEmail = HttpContext.Session.GetString("Email");
            var userName = HttpContext.Session.GetString("name");
            var flatNo = HttpContext.Session.GetString("FlatNo");

            // Create invoice as a PDF file
            var fileName = $"SocioSphere_Invoice_{bill.Id}.pdf";
            var pdfPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "invoices", fileName);
            GeneratePDF(pdfPath, bill, userName, flatNo);

            // Send email if requested
            if (sendEmail)
            {
                SendInvoiceEmail(userEmail, pdfPath, fileName, bill, userName, flatNo);
            }

            // Return the file for download
            byte[] fileBytes = System.IO.File.ReadAllBytes(pdfPath);
            return File(fileBytes, "application/pdf", fileName);
        }
        private void SendInvoiceEmail(string userEmail, string pdfPath, string fileName, billmanagement bill, string userName, string flatNo)
        {
            string emailTemplate = GetEmailTemplate();

            // Replace placeholders in the template
            emailTemplate = emailTemplate.Replace("{UserName}", userName)
                                         .Replace("{BillTitle}", bill.Title)
                                         .Replace("{InvoiceNumber}", bill.Id.ToString("D5"))
                                         .Replace("{Amount}", bill.AmountPay.ToString("C"))
                                         .Replace("{DueDate}", bill.BillReleaseDt.ToString("dd-MM-yyyy"))
                                         .Replace("{FlatNo}", flatNo);

            using (MailMessage mail = new MailMessage())
            {
                mail.From = new MailAddress(_emailSettings.SenderEmail, "SocioSphere");
                mail.To.Add(userEmail);
                mail.Subject = $"SocioSphere Invoice - {bill.Title}";
                mail.Body = emailTemplate;
                mail.IsBodyHtml = true;
                mail.Attachments.Add(new Attachment(pdfPath));

                using (SmtpClient smtp = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort))
                {
                    smtp.Credentials = new System.Net.NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword);
                    smtp.EnableSsl = true;
                    smtp.Send(mail);
                }
            }
        }

        private string GetEmailTemplate()
        {
            return @"
    <!DOCTYPE html>
    <html lang='en'>
    <head>
        <meta charset='UTF-8'>
        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        <title>SocioSphere - Your Invoice</title>
        <style>
            body {
                font-family: Arial, sans-serif;
                line-height: 1.6;
                color: #333;
            }
            .container {
                max-width: 600px;
                margin: 0 auto;
                padding: 20px;
                background-color: #f9f9f9;
            }
            .header {
                background-color: #4a90e2;
                color: #ffffff;
                padding: 20px;
                text-align: center;
            }
            .logo {
                max-width: 150px;
                margin-bottom: 10px;
            }
            .content {
                background-color: #ffffff;
                padding: 20px;
                border-radius: 5px;
                margin-top: 20px;
            }
            .footer {
                text-align: center;
                margin-top: 20px;
                font-size: 12px;
                color: #888;
            }
        </style>
    </head>
    <body>
        <div class='container'>
            <div class='header'>
                <h1>SocioSphere Invoice</h1>
            </div>
            <div class='content'>
                <p>Dear {UserName},</p>
                <p>Thank you for being a valued member of SocioSphere. Your invoice for {BillTitle} is now available.</p>
                <p>Invoice Details:</p>
                <ul>
                    <li>Invoice Number: SOC-{InvoiceNumber}</li>
                    <li>Amount: {Amount}</li>
                    <li>Due Date: {DueDate}</li>
                    <li>Flat Number: {FlatNo}</li>
                </ul>
                <p>Please find your invoice attached to this email. If you have any questions about this invoice or any SocioSphere services, please don't hesitate to contact our support team.</p>
                <p>We appreciate your prompt payment and your contribution to our community.</p>
                <p>Best regards,<br>TheSocioSphere Team</p>
            </div>
            <div class='footer'>
                <p>This is an automated email from SocioSphere. Please do not reply directly to this message.</p>
                <p>© 2024 SocioSphere. All rights reserved.</p>
            </div>
        </div>
    </body>
    </html>";
        }

        private void GeneratePDF(string pdfPath, billmanagement bill, string userName, string flatNo)
        {
            using (FileStream fs = new FileStream(pdfPath, FileMode.Create, FileAccess.Write))
            {
                using (Document document = new Document(PageSize.A4, 36, 36, 54, 36))
                {
                    PdfWriter writer = PdfWriter.GetInstance(document, fs);
                    document.Open();

                    // Fonts
                    var fontTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 24, BaseColor.DARK_GRAY);
                    var fontHeader = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE);
                    var fontSubHeader = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.DARK_GRAY);
                    var fontBody = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);

                    // Add logo (replace with your actual logo path)
                    string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Content", "Images", "Untitled_design-removebg-preview.png");
                    if (System.IO.File.Exists(logoPath))  // Use System.IO.File instead of File
                    {
                        Image logo = Image.GetInstance(logoPath);
                        logo.ScaleToFit(100f, 100f);
                        logo.Alignment = Element.ALIGN_LEFT;
                        document.Add(logo);
                    }

                    // Add an invoice title
                    Paragraph title = new Paragraph("INVOICE", fontTitle);
                    title.Alignment = Element.ALIGN_RIGHT;
                    document.Add(title);

                    // Add some space
                    document.Add(new Paragraph("\n"));

                    // Add user details
                    PdfPTable userTable = new PdfPTable(2);
                    userTable.WidthPercentage = 100;
                    userTable.SetWidths(new float[] { 1, 1 });

                    AddCellToBody(userTable, $"Name: {userName}", fontBody);
                    AddCellToBody(userTable, $"Flat No: {flatNo}", fontBody);
                    AddCellToBody(userTable, $"Invoice Date: {DateTime.Now:dd-MM-yyyy}", fontBody);
                    AddCellToBody(userTable, $"Invoice No: INV-{bill.Id:D5}", fontBody);

                    document.Add(userTable);

                    // Add some space
                    document.Add(new Paragraph("\n"));

                    // Add a table for bill details
                    PdfPTable table = new PdfPTable(2);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 1, 2 });

                    // Add table headers
                    AddCellToHeader(table, "Description", fontHeader);
                    AddCellToHeader(table, "Details", fontHeader);

                    // Add table body
                    AddCellToBody(table, "Title", fontBody);
                    AddCellToBody(table, bill.Title, fontBody);

                    AddCellToBody(table, "Amount", fontBody);
                    AddCellToBody(table, bill.AmountPay.ToString("C"), fontBody);

                    AddCellToBody(table, "Month", fontBody);
                    AddCellToBody(table, bill.Month.ToString("MMMM yyyy"), fontBody);

                    AddCellToBody(table, "Status", fontBody);
                    AddCellToBody(table, bill.PaidStatus, fontBody);

                    AddCellToBody(table, "Bill Release Date", fontBody);
                    AddCellToBody(table, bill.BillReleaseDt.ToString("dd-MM-yyyy"), fontBody);

                    AddCellToBody(table, "Submission Date", fontBody);
                    AddCellToBody(table, bill.BillSbmtDate?.ToString("dd-MM-yyyy") ?? "N/A", fontBody);

                    document.Add(table);

                    // Add some space
                    document.Add(new Paragraph("\n"));

                    // Add total amount
                    PdfPTable totalTable = new PdfPTable(2);
                    totalTable.WidthPercentage = 30;
                    totalTable.HorizontalAlignment = Element.ALIGN_RIGHT;
                    AddCellToHeader(totalTable, "Total Amount", fontSubHeader);
                    AddCellToBody(totalTable, bill.AmountPay.ToString("C"), fontSubHeader);
                    document.Add(totalTable);

                    // Add footer text
                    Paragraph footer = new Paragraph("Thank you for your payment!", fontBody);
                    footer.Alignment = Element.ALIGN_CENTER;
                    document.Add(footer);

                    // Add page numbers
                    int pageN = writer.PageNumber;
                    var cb = writer.DirectContent;
                    var pageSize = document.PageSize;
                    cb.SetRGBColorFill(100, 100, 100);
                    cb.BeginText();
                    cb.SetFontAndSize(BaseFont.CreateFont(), 8);
                    cb.ShowTextAligned(PdfContentByte.ALIGN_CENTER, "Page " + pageN, pageSize.GetRight(30), pageSize.GetBottom(30), 0);
                    cb.EndText();

                    document.Close();
                }
            }
        }

        private void AddCellToHeader(PdfPTable table, string text, Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = new BaseColor(41, 128, 185); // A nice blue color
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 8;
            table.AddCell(cell);
        }

        private void AddCellToBody(PdfPTable table, string text, Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.HorizontalAlignment = Element.ALIGN_LEFT;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 8;
            table.AddCell(cell);
        }

        [HttpGet]
        public IActionResult GetNotifications()
        {
            var userName = HttpContext.Session.GetString("name");
            var userFlatDetails = _context.alloteflats.FirstOrDefault(x => x.name == userName);

            if (userFlatDetails != null)
            {
                var announcements = _context.announcements
                    .Where(a => a.AnnounceDate >= DateTime.Now.AddHours(-24) && !a.IsRead)
                    .OrderByDescending(a => a.AnnounceDate)
                    .ToList();

                var bills = _context.billmanagements
                    .Where(b => b.WingName == userFlatDetails.wingname &&
                                b.FlatNo == userFlatDetails.flatno &&
                                b.PaidStatus == "Pending" &&
                                !b.IsRead)
                    .OrderByDescending(b => b.BillReleaseDt)
                    .ToList();

                return PartialView("_AnnouncementsPartial", Tuple.Create(announcements, bills));
            }

            return PartialView("_AnnouncementsPartial", Tuple.Create(new List<announcement>(), new List<billmanagement>()));
        }

        [HttpGet]
        public JsonResult GetNotificationCount()
        {
            var userName = HttpContext.Session.GetString("name");
            var userFlatDetails = _context.alloteflats.FirstOrDefault(x => x.name == userName);

            if (userFlatDetails != null)
            {
                var announcementCount = _context.announcements.Count(a => a.AnnounceDate >= DateTime.Now.AddHours(-24) && !a.IsRead);
                var billCount = _context.billmanagements.Count(b => b.WingName == userFlatDetails.wingname &&
                                                                    b.FlatNo == userFlatDetails.flatno &&
                                                                    b.PaidStatus == "Pending" &&
                                                                    !b.IsRead);

                return Json(announcementCount + billCount);
            }

            return Json(0);
        }

        [HttpPost]
        public IActionResult MarkAnnouncementAsRead(int id)
        {
            var announcement = _context.announcements.Find(id);
            if (announcement != null)
            {
                announcement.IsRead = true;
                _context.SaveChanges();
            }
            return Ok();
        }



        [HttpPost]
        public IActionResult MarkBillAsRead(int id)
        {
            var bill = _context.billmanagements.Find(id);
            if (bill != null)
            {
                bill.IsRead = true;
                _context.SaveChanges();
            }
            return Ok();
        }


		

	}
}
