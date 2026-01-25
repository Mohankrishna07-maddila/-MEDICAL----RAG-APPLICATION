using System.Net.Http.Json;

public class ApiTestService
{
    private readonly HttpClient _http;

    public ApiTestService(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> Ping()
    {
        var response = await _http.GetAsync("/ping");
        return response.IsSuccessStatusCode ? "API Connected" : "API Failed";
    }
}
