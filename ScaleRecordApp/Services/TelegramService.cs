using System.Net.Http.Headers;

namespace ScaleRecordApp.Services;

public class TelegramService
{
    private readonly HttpClient _http;
    private readonly string _token;
    private string ApiBase => $"https://api.telegram.org/bot{_token}/";

    public TelegramService(string token, HttpClient http)
    {
        _token = token ?? throw new ArgumentNullException(nameof(token));
        _http = http ?? new HttpClient();
    }

    /// <summary>
    /// Отправляет документ (multipart/form-data) в telegram.
    /// chatIdOrUsername может быть numeric chat_id (строка с цифрами) или @username.
    /// Важно: бот может отправлять только тем пользователям, которые хотя бы раз писали боту.
    /// </summary>
    public async Task SendDocumentAsync(string chatIdOrUsername, string filePath, string caption = null)
    {
        if (string.IsNullOrWhiteSpace(chatIdOrUsername))
            throw new ArgumentException("chatIdOrUsername required.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        using var fs = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(chatIdOrUsername), "chat_id");

        var fileContent = new StreamContent(fs);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "document", Path.GetFileName(filePath));

        if (!string.IsNullOrEmpty(caption))
            content.Add(new StringContent(caption), "caption");

        var res = await _http.PostAsync(ApiBase + "sendDocument", content);
        if (!res.IsSuccessStatusCode)
        {
            var txt = await res.Content.ReadAsStringAsync();
            throw new Exception($"Telegram API error: {res.StatusCode} {txt}");
        }
    }
}
