using Microsoft.AspNetCore.Authentication;

namespace TemplateApp.Api.IntegrationTests.Helpers;

public class TestAuthOptions : AuthenticationSchemeOptions
{
    public bool AuthenticationSucceeds { get; set; } = true;
    public string? Email { get; set; }
}

