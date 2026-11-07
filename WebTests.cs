using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using RaceDay.Web.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Tests;

/// <summary>Tests for the MVC project's role-based navigation protection, error handling and display helpers.</summary>
public class WebTests
{
    private static ActionContext NewActionContext(TestSession session, string method = "GET")
    {
        var http = new DefaultHttpContext { Session = session };
        http.Request.Method = method;
        return new ActionContext(http, new RouteData(), new ActionDescriptor());
    }

    private static TestSession LoggedIn(string role)
    {
        var session = new TestSession();
        session.SetInt32(SessionKeys.UserId, 7);
        session.SetString(SessionKeys.Role, role);
        return session;
    }

    [Fact]
    public void RequireRole_NotLoggedIn_RedirectsToLogin()
    {
        var context = new AuthorizationFilterContext(NewActionContext(new TestSession()), new List<IFilterMetadata>());

        new RequireRoleAttribute(Roles.Organiser).OnAuthorization(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Account", redirect.ControllerName);
    }

    [Fact]
    public void RequireRole_PostWhenNotLoggedIn_DoesNotKeepAReturnUrl()
    {
        var context = new AuthorizationFilterContext(NewActionContext(new TestSession(), "POST"), new List<IFilterMetadata>());

        new RequireRoleAttribute(Roles.Participant).OnAuthorization(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Null(redirect.RouteValues!["returnUrl"]);
    }

    [Fact]
    public void RequireRole_WrongRole_RedirectsToAccessDenied()
    {
        var context = new AuthorizationFilterContext(NewActionContext(LoggedIn(Roles.Participant)), new List<IFilterMetadata>());

        new RequireRoleAttribute(Roles.Organiser).OnAuthorization(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("AccessDenied", redirect.ActionName);
    }

    [Fact]
    public void RequireRole_CorrectRole_AllowsTheRequest()
    {
        var context = new AuthorizationFilterContext(NewActionContext(LoggedIn(Roles.Organiser)), new List<IFilterMetadata>());

        new RequireRoleAttribute(Roles.Organiser).OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void RequireRole_NoRoleSpecified_AllowsAnyLoggedInUser()
    {
        var context = new AuthorizationFilterContext(NewActionContext(LoggedIn(Roles.Participant)), new List<IFilterMetadata>());

        new RequireRoleAttribute().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void ApiExceptionFilter_Unauthorized_ClearsSessionAndRedirectsToLogin()
    {
        var session = LoggedIn(Roles.Participant);
        var context = new ExceptionContext(NewActionContext(session), new List<IFilterMetadata>())
        {
            Exception = new ApiException("Please log in to continue.", 401)
        };

        new ApiExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.False(session.IsLoggedIn());
    }

    [Fact]
    public void ApiExceptionFilter_OtherApiError_ShowsFriendlyProblemPage()
    {
        var context = new ExceptionContext(NewActionContext(LoggedIn(Roles.Participant)), new List<IFilterMetadata>())
        {
            Exception = new ApiException("That item could not be found.", 404)
        };

        new ApiExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Problem", redirect.ActionName);
        Assert.Equal("That item could not be found.", redirect.RouteValues!["message"]);
    }

    [Fact]
    public void ApiExceptionFilter_UnrelatedException_IsLeftAlone()
    {
        var context = new ExceptionContext(NewActionContext(new TestSession()), new List<IFilterMetadata>())
        {
            Exception = new InvalidOperationException("boom")
        };

        new ApiExceptionFilter().OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(47, "47th")]
    [InlineData(112, "112th")]
    public void Ordinal_FormatsPositionsLikeA_RaceResult(int number, string expected)
    {
        Assert.Equal(expected, Display.Ordinal(number));
    }

    [Fact]
    public void FinishTime_FormatsAsHoursMinutesSeconds()
    {
        Assert.Equal("00:24:15", Display.FinishTime(new TimeSpan(0, 24, 15)));
        Assert.Equal("-", Display.FinishTime(null));
    }

    [Fact]
    public void Money_FormatsRandAmounts()
    {
        Assert.Equal("R120.00", Display.Money(120m));
        Assert.Equal("Free", Display.Money(0m));
    }
}
