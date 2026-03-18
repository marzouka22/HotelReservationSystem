using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HotelReservationSystem.Models
{
    public class Room
    {
        public int Id { get; set; }

        [Required]
        public string RoomNumber { get; set; }

        public string Type { get; set; }

        [Precision(10, 2)]
        public decimal Price { get; set; }


        public bool IsAvailable { get; set; }
    }
}