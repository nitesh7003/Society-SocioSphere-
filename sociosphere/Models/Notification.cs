using System.ComponentModel.DataAnnotations;

namespace sociosphere.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public bool IsRead { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; } // This will be 24 hours from CreatedAt
    }
}
