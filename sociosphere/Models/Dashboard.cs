using System.ComponentModel.DataAnnotations;
namespace sociosphere.Models
{
    public class Dashboard
    {

        public int TotalComplaints { get; set; }
        public int TotalFlats { get; set; }
        public int TotalAllotedFlats { get; set; }
        public int TotalAnnouncements { get; set; }
        public int TotalVisitors { get; set; }
        public int TotalUsers { get; set; }
        public int PendingComplaints { get; set; }
         
        public int avalibalevisitors { get; set; }
    }
}
