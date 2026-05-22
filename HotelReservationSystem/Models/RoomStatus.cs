using System.ComponentModel.DataAnnotations;

namespace HotelReservationSystem.Models
{
    /// <summary>
    /// Statut administratif d'une chambre.
    /// (Le statut "Occupée" est calculé dynamiquement à partir des réservations.)
    /// </summary>
    public enum RoomStatus
    {
        [Display(Name = "Disponible")]
        Available = 0,

        [Display(Name = "En maintenance")]
        Maintenance = 1
    }
}
