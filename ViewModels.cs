using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your full name."), StringLength(100, MinimumLength = 2, ErrorMessage = "Your name must be 2 to 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a password."), StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password."), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Participant";

    [Display(Name = "Organiser invite code")]
    public string? OrganiserCode { get; set; }
}

public class HomeViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? EventType { get; set; }
    public string? Location { get; set; }
}

public class EventDetailsViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Please enter the event name."), StringLength(150)]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose the event date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the location."), StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose the event type.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = string.Empty;

    [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public string? CurrentBannerUrl { get; set; }
}

public class CategoryFormViewModel
{
    public int? CategoryId { get; set; }
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please enter a category name."), StringLength(50)]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; }

    [Range(0, 100000, ErrorMessage = "Entry fee must be zero or more.")]
    [Display(Name = "Entry fee (R)")]
    public decimal EntryFee { get; set; }

    [Range(1, 100000, ErrorMessage = "Maximum participants must be at least 1.")]
    [Display(Name = "Maximum participants")]
    public int MaxParticipants { get; set; } = 500;
}

public class CategoriesPageViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public CategoryFormViewModel Form { get; set; } = new();
}

public class DashboardViewModel
{
    public List<EventSummaryDto> Events { get; set; } = new();
    public int TotalEvents => Events.Count;
    public int TotalEnrolments => Events.Sum(e => e.TotalEnrolments);
    public List<EventSummaryDto> Upcoming => Events.Where(e => e.EventDate.Date >= DateTime.Today).OrderBy(e => e.EventDate).ToList();
}

public class EnrolmentsPageViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultRowViewModel
{
    public EnrolmentDto Enrolment { get; set; } = new();
    public ResultDto? Result { get; set; }
}

public class ResultsPageViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public List<ResultRowViewModel> Rows { get; set; } = new();
    public bool EventHasFinished => Event.EventDate.Date <= DateTime.Today;
}

public class ResultInputModel
{
    public int EventId { get; set; }
    public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }
    public string? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
}

public class ProfileViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please enter your full name."), StringLength(100, MinimumLength = 2, ErrorMessage = "Your name must be 2 to 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    public string? ProfilePictureUrl { get; set; }

    [Display(Name = "Profile picture")]
    public IFormFile? Picture { get; set; }
}

public class ProblemViewModel
{
    public string Message { get; set; } = "Something went wrong.";
}
