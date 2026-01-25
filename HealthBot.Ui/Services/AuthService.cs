using System.Net.Http.Json;

public class AuthService
{
    private readonly HttpClient _http;

    public string? Token { get; private set; }
    public string? Role { get; private set; }

    public AuthService(HttpClient http)
    {
        _http = http;
    }

    public async Task Login(string username)
    {
        var res = await _http.PostAsJsonAsync("/auth/token", new { username });
        var data = await res.Content.ReadFromJsonAsync<AuthResponse>();

        Token = data!.access_token;
        Role = data.role;

        await SetAuthHeader();
    }

    private Task SetAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

        return Task.CompletedTask;
    }
}

public record AuthResponse(string access_token, string role);
