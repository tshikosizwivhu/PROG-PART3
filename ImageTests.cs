using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

public class ImageTests
{
    /// <summary>Stands in for Azure Blob Storage so the tests need no cloud account.</summary>
    private sealed class FakeBlobStorage : IBlobStorageService
    {
        public bool NotConfigured { get; set; }

        public Task<string> UploadImageAsync(Stream content, string contentType, string folder, string extension, CancellationToken cancellationToken = default)
        {
            if (NotConfigured) throw new BlobStorageNotConfiguredException();
            return Task.FromResult($"https://storage.test/raceday-images/{folder}/fake{extension}");
        }
    }

    private static IFormFile ImageFile(string contentType = "image/png", byte[]? bytes = null)
    {
        bytes ??= new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", "test.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task UploadBanner_AsOwner_SavesBlobUrlOnEvent()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var controller = new ImagesController(db, new FakeBlobStorage()).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.UploadBanner(ev.EventId, ImageFile());

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ImageUploadResultDto>(ok.Value);
        Assert.StartsWith("https://storage.test/raceday-images/banners/", dto.Url);
        Assert.Equal(dto.Url, (await db.Events.FindAsync(ev.EventId))!.BannerImageUrl);
    }

    [Fact]
    public async Task UploadBanner_ForAnotherOrganisersEvent_ReturnsForbidden()
    {
        using var db = TestHelpers.NewDb();
        var owner = await TestHelpers.AddOrganiserAsync(db);
        var other = await TestHelpers.AddOrganiserAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, owner.OrganiserId);
        var controller = new ImagesController(db, new FakeBlobStorage()).WithSession(other.OrganiserId, Roles.Organiser);

        var result = await controller.UploadBanner(ev.EventId, ImageFile());

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Null((await db.Events.FindAsync(ev.EventId))!.BannerImageUrl);
    }

    [Fact]
    public async Task UploadBanner_UnknownEvent_ReturnsNotFound()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var controller = new ImagesController(db, new FakeBlobStorage()).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.UploadBanner(999, ImageFile());

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UploadBanner_WrongContentType_ReturnsBadRequest()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var controller = new ImagesController(db, new FakeBlobStorage()).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.UploadBanner(ev.EventId, ImageFile("application/pdf"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UploadBanner_FileThatIsNotAnImage_ReturnsBadRequest()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var controller = new ImagesController(db, new FakeBlobStorage()).WithSession(organiser.OrganiserId, Roles.Organiser);
        var fakeImage = System.Text.Encoding.UTF8.GetBytes("this is just text pretending to be a picture");

        var result = await controller.UploadBanner(ev.EventId, ImageFile("image/png", fakeImage));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UploadBanner_StorageNotConfigured_Returns503()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var controller = new ImagesController(db, new FakeBlobStorage { NotConfigured = true })
            .WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.UploadBanner(ev.EventId, ImageFile());

        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task UploadProfilePicture_AsParticipant_SavesBlobUrl()
    {
        using var db = TestHelpers.NewDb();
        var participant = await TestHelpers.AddParticipantAsync(db);
        var controller = new ImagesController(db, new FakeBlobStorage()).WithSession(participant.ParticipantId, Roles.Participant);

        var result = await controller.UploadProfilePicture(participant.ParticipantId, ImageFile());

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ImageUploadResultDto>(ok.Value);
        Assert.StartsWith("https://storage.test/raceday-images/profiles/", dto.Url);
        Assert.Equal(dto.Url, (await db.Participants.FindAsync(participant.ParticipantId))!.ProfilePictureUrl);
    }

    [Fact]
    public async Task UploadProfilePicture_ForAnotherParticipant_ReturnsForbidden()
    {
        using var db = TestHelpers.NewDb();
        var me = await TestHelpers.AddParticipantAsync(db);
        var someoneElse = await TestHelpers.AddParticipantAsync(db);
        var controller = new ImagesController(db, new FakeBlobStorage()).WithSession(me.ParticipantId, Roles.Participant);

        var result = await controller.UploadProfilePicture(someoneElse.ParticipantId, ImageFile());

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task GetMyEvents_ReturnsOnlyOwnEventsWithEnrolmentCounts()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var other = await TestHelpers.AddOrganiserAsync(db);
        var mine = await TestHelpers.AddEventAsync(db, organiser.OrganiserId, name: "Mine");
        await TestHelpers.AddEventAsync(db, other.OrganiserId, name: "Not mine");
        var category = await TestHelpers.AddCategoryAsync(db, mine.EventId);
        var p1 = await TestHelpers.AddParticipantAsync(db);
        var p2 = await TestHelpers.AddParticipantAsync(db);
        await TestHelpers.AddEnrolmentAsync(db, p1.ParticipantId, category.CategoryId);
        await TestHelpers.AddEnrolmentAsync(db, p2.ParticipantId, category.CategoryId);
        var controller = new EventsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.GetMyEvents();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var events = Assert.IsAssignableFrom<IEnumerable<EventSummaryDto>>(ok.Value).ToList();
        var single = Assert.Single(events);
        Assert.Equal("Mine", single.EventName);
        Assert.Equal(2, single.TotalEnrolments);
    }
}
