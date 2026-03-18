using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HotelReservationSystem.Models
{
    using Microsoft.AspNetCore.Identity;

    public class Reservation
    {
        public int Id { get; set; }

        [Required]
        public DateTime CheckIn { get; set; }

        [Required]
        public DateTime CheckOut { get; set; }

        [Required(ErrorMessage = "Veuillez sélectionner une chambre")]
        public int RoomId { get; set; }

        // Make ClientId optional because reservations will be linked to Identity users (UserId)
        public int? ClientId { get; set; }

        // Link to the authenticated Identity user (optional)
        public string? UserId { get; set; }

        [ForeignKey("RoomId")]
        [ValidateNever]
        public Room? Room { get; set; }

        [ForeignKey("ClientId")]
        [ValidateNever]
        public Client? Client { get; set; }

        // Navigation to Identity user (optional)
        [ValidateNever]
        public IdentityUser? User { get; set; }
    }
}