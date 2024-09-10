using System.ComponentModel.DataAnnotations;

namespace sociosphere.Models
{
    public class ContactFormModel
    {
        public int ID { get; set; }
        [Required]
        public string Name { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, Phone]
        public string Phone { get; set; }

        public string Project { get; set; }

        [Required]
        public string Subject { get; set; }

        [Required]
        public string Message { get; set; }
    }

}
