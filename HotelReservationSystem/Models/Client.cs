using System.ComponentModel.DataAnnotations;

namespace HotelReservationSystem.Models
{
    public class Client
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }
    }
}