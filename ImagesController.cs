using Azure;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Image uploads. Files are validated here, stored in Azure Blob Storage and the resulting URL is saved in the database.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class ImagesController : ApiControllerBase
{
    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _blobs;

    public ImagesController(RaceDayDbContext db, IBlobStorageService blobs)
    {
        _db = db;
        _blobs = blobs;
    }

    /// <summary>Uploads (or replaces) the banner image of one of the organiser's events.</summary>
    [HttpPost("events/{id:int}/banner")]
    [SessionAuthorize(Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUploadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ImageUploadResultDto>> UploadBanner(int id, IFormFile file)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new ApiError("Event not found."));
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only upload banners for your own events.");

        var upload = await StoreAsync(file, "banners");
        if (upload.Error != null) return upload.Error;

        ev.BannerImageUrl = upload.Url;
        await _db.SaveChangesAsync();
        return Ok(new ImageUploadResultDto { Url = upload.Url! });
    }

    /// <summary>Uploads (or replaces) the logged-in participant's profile picture.</summary>
    [HttpPost("participants/{id:int}/picture")]
    [SessionAuthorize(Roles.Participant)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUploadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ImageUploadResultDto>> UploadProfilePicture(int id, IFormFile file)
    {
        if (CurrentUserId != id) return Forbidden("You can only change your own profile picture.");

        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return NotFound(new ApiError("Participant not found."));

        var upload = await StoreAsync(file, "profiles");
        if (upload.Error != null) return upload.Error;

        participant.ProfilePictureUrl = upload.Url;
        await _db.SaveChangesAsync();
        return Ok(new ImageUploadResultDto { Url = upload.Url! });
    }

    private async Task<(string? Url, ActionResult? Error)> StoreAsync(IFormFile? file, string folder)
    {
        if (file == null || file.Length == 0)
            return (null, BadRequest(new ApiError("Please choose an image file to upload.")));
        if (file.Length > MaxBytes)
            return (null, BadRequest(new ApiError("The image must be 5 MB or smaller.")));
        if (!AllowedTypes.TryGetValue(file.ContentType ?? string.Empty, out var extension))
            return (null, BadRequest(new ApiError("Only JPG, PNG, WEBP or GIF images are allowed.")));

        try
        {
            await using var stream = file.OpenReadStream();
            if (!await LooksLikeImageAsync(stream))
                return (null, BadRequest(new ApiError("The file is not a valid image.")));

            stream.Position = 0;
            var url = await _blobs.UploadImageAsync(stream, file.ContentType!, folder, extension);
            return (url, null);
        }
        catch (BlobStorageNotConfiguredException)
        {
            return (null, StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiError("Image storage is not configured on the server.")));
        }
        catch (RequestFailedException)
        {
            return (null, StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiError("The image could not be saved to cloud storage. Please try again later.")));
        }
    }

    /// <summary>Checks the first bytes of the file (JPEG, PNG, GIF or WEBP signature) so a renamed file is rejected.</summary>
    private static async Task<bool> LooksLikeImageAsync(Stream stream)
    {
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (read < 4) return false;

        var jpeg = header[0] == 0xFF && header[1] == 0xD8;
        var png = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        var gif = header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46;
        var webp = read >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                   && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
        return jpeg || png || gif || webp;
    }
}
