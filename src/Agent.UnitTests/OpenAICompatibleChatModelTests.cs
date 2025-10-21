using Agent.Core.LLM;
using Agent.Infrastructure.LLM;

namespace Agent.UnitTests;

[TestClass]
public class OpenAICompatibleChatModelTests
{
    [TestMethod]
    public async Task Completes_WhenLocalServerResponds()
    {
        var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:8080/v1") };
        var model = new OpenAICompatibleChatModel(http, "test-model", 64, 0.2, 0.9);

        var resp = await model.CompleteAsync(new[]
        {
            new ChatMessage(AuthorRole.System, "You are concise."),
            new ChatMessage(AuthorRole.User, "Say 'hi' once.")
        });

        Assert.IsFalse(string.IsNullOrWhiteSpace(resp.Text));
    }
}
