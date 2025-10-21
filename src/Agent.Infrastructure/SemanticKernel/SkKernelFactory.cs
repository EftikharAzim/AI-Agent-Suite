using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Agent.Core.LLM;

namespace Agent.Infrastructure.SemanticKernel;

public static class SkKernelFactory
{
    public static Kernel Create(IServiceProvider sp)
    {
        var builder = Kernel.CreateBuilder();

        // Use our adapter as the ChatCompletionService
        var chatService = new SkLocalChatService(sp.GetRequiredService<IChatModel>());
        builder.Services.AddSingleton<IChatCompletionService>(chatService);

        return builder.Build();
    }
}
