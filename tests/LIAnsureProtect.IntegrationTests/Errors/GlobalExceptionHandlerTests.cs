using LIAnsureProtect.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace LIAnsureProtect.IntegrationTests.Errors;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task Unexpected_Exception_Returns_Safe_Coded_Problem_With_Correlation_Id()
    {
        ProblemDetailsContext? writtenContext = null;
        var problemDetailsService = new Mock<IProblemDetailsService>();
        problemDetailsService
            .Setup(service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(context => writtenContext = context)
            .Returns(() => ValueTask.FromResult(true));
        var logger = Mock.Of<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(problemDetailsService.Object, logger);
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "safe-support-id"
        };

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("internal database details must remain private"),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.NotNull(writtenContext);
        var problem = writtenContext.ProblemDetails;
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
        Assert.Equal("request.unexpected_error", problem.Extensions["code"]);
        Assert.Equal("safe-support-id", problem.Extensions["correlationId"]);
        Assert.DoesNotContain("database", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }
}
