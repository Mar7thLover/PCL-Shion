using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Services;

namespace PCL.Shion.Test;

[TestClass]
public sealed class LifecycleTest
{
    [TestMethod]
    public void ServiceShutdownCancelsWorkAndCannotRecreateEngines()
    {
        var services = new GameServices(new AppSettings());
        var token = services.ShutdownToken;
        services.Dispose();
        services.Dispose();
        Assert.IsTrue(token.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => _ = services.Engine);
    }

    [TestMethod]
    public async Task ShutdownCancelsUpdateRequestAndReleasesCheckGuard()
    {
        using var cancel = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var service = new UpdateService(new AppSettings(), () => http, cancel.Token);
        var request = service.CheckAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await request.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreNotEqual(UpdateState.Checking, service.State);
    }

    [TestMethod]
    public async Task ConcurrentUpdateChecksShareOneRequestAndBadObserverIsIsolated()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        using var http = new HttpClient(new Handler(async token =>
        {
            Interlocked.Increment(ref requests);
            started.TrySetResult();
            await finish.Task.WaitAsync(token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
        }));
        var service = new UpdateService(new AppSettings(), () => http);
        var observed = 0;
        service.Changed += () => throw new InvalidOperationException("broken observer");
        service.Changed += () => Interlocked.Increment(ref observed);
        var first = service.CheckAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(service.CheckAsync)));
        finish.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, requests);
        Assert.AreEqual(UpdateState.UpToDate, service.State);
        Assert.AreEqual(2, observed);
    }

    [TestMethod]
    public async Task ShutdownBetweenStartingAndResolvingHttpDoesNotLeaveCheckingState()
    {
        using var cancel = new CancellationTokenSource();
        var service = new UpdateService(new AppSettings(), () =>
        {
            cancel.Cancel();
            throw new ObjectDisposedException("engine");
        }, cancel.Token);
        await service.CheckAsync();
        Assert.AreEqual(UpdateState.Idle, service.State);
    }

    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(cancellationToken);
    }
}
