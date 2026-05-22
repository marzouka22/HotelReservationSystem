using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HotelReservationSystem.Models
{
    public class Room
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Le numéro de chambre est requis.")]
        [Display(Name = "Numéro de chambre")]
        public string RoomNumber { get; set; } = string.Empty;

        [Display(Name = "Type")]
        public string Type { get; set; } = string.Empty;

        [Precision(10, 2)]
        [Range(0, 1000000, ErrorMessage = "Le prix doit être positif.")]
        [Display(Name = "Prix (€)")]
        public decimal Price { get; set; }

        [Range(1, 20, ErrorMessage = "La capacité doit être comprise entre 1 et 20.")]
        [Display(Name = "Capacité (personnes)")]
        public int Capacity { get; set; } = 2;

        [Display(Name = "Statut")]
        public RoomStatus Status { get; set; } = RoomStatus.Available;
    }
}
