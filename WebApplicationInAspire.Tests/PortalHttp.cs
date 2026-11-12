using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApplicationInAspire.Tests;

/// <summary>Testklient bruker ekte innlogging, cookies og antiforgery; ingen falsk auth-handler.</summary>
public sealed class PortalHttp : IDisposable
{
    public const string Password = "OnlyForTests123";
    public HttpClient Client { get; }
    public PortalHttp(PortalFactory factory) => Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, HandleCookies = true });

    public async Task<HttpResponseMessage> PostAsync(string path, Dictionary<string, string> fields, string tokenPath)
    {
        var html = await Client.GetStringAsync(tokenPath);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        fields = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token) };
        return await Client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    public async Task LoginAsync(string email)
    {
        var response = await PostAsync("/Account/Login", new() { ["Email"] = email, ["Password"] = Password }, "/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    public static Dictionary<string, string> ResourceFields(string name) => new()
    {
        ["Name"] = name, ["Type"] = "Pumpe", ["Region"] = "Agder", ["Description"] = "Testressurs",
        ["ContactName"] = "Testperson", ["PhoneNumber"] = "+4799999999", ["Latitude"] = "58.1467", ["Longitude"] = "7.9956",
        ["IsAvailable"] = "true", ["AvailableFrom"] = "2026-11-01T08:00", ["AvailableUntil"] = "2026-11-30T18:00"
    };
    public void Dispose() => Client.Dispose();
}
