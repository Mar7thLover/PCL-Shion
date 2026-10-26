using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Navigation;

namespace PCL.Shion.Test;

[TestClass]
public sealed class LoaderTaskTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task SynchronousCompletionNeverSeesAnUnpublishedTask()
    {
        var loader = new ModLoader.LoaderTask<int, int>("fast", task =>
        {
            Assert.IsNotNull(task.lastRunningTask);
            Assert.AreEqual(Task.CurrentId, task.lastRunningTask.Id);
            Assert.IsFalse(task.IsAborted);
            Assert.IsTrue(task.AbortedToken.CanBeCanceled);
            task.output = task.input;
        });

        for (var i = 1; i <= 100; i++)
        {
            loader.Start(i, true);
            await loader.lastRunningTask!.WaitAsync(Timeout);
            Assert.AreEqual(LoadState.Finished, loader.State);
            Assert.AreEqual(i, loader.output);
            Assert.IsNull(loader.Error);
        }
    }

    [TestMethod]
    public async Task ForceRestartKeepsPerRunInputAndTokenAndRejectsStaleWrites()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finishFirst = new ManualResetEventSlim();
        using var finishSecond = new ManualResetEventSlim();
        CancellationToken firstToken = default;
        CancellationToken secondToken = default;
        var oldInputAfterRestart = 0;
        var oldTokenStillCanceled = false;
        var loader = new ModLoader.LoaderTask<int, int>("restart", task =>
        {
            if (task.input == 1)
            {
                firstToken = task.AbortedToken;
                firstStarted.SetResult();
                Assert.IsTrue(finishFirst.Wait(Timeout));
                oldInputAfterRestart = task.input;
                oldTokenStillCanceled = task.AbortedToken == firstToken && task.AbortedToken.IsCancellationRequested;
                task.output = 111;
                task.input = 111;
                task.Error = new IOException("stale failure");
                task.Progress = 0.99;
                task.State = LoadState.Failed;
            }
            else
            {
                secondToken = task.AbortedToken;
                secondStarted.SetResult();
                Assert.IsTrue(finishSecond.Wait(Timeout));
                task.output = task.input;
            }
        });

        loader.Start(1);
        var first = loader.lastRunningTask!;
        try
        {
            await firstStarted.Task.WaitAsync(Timeout);
            loader.Start(2, true);
            var second = loader.lastRunningTask!;
            await secondStarted.Task.WaitAsync(Timeout);
            Assert.IsTrue(firstToken.IsCancellationRequested);
            Assert.IsFalse(secondToken.IsCancellationRequested);
            Assert.AreNotEqual(firstToken, secondToken);

            finishFirst.Set();
            await first.WaitAsync(Timeout);
            Assert.AreEqual(1, oldInputAfterRestart);
            Assert.IsTrue(oldTokenStillCanceled);
            Assert.AreEqual(2, loader.input);
            Assert.AreEqual(LoadState.Loading, loader.State);
            Assert.IsNull(loader.Error);
            Assert.AreEqual(0.02, loader.Progress);
            Assert.AreEqual(0, loader.output);

            finishSecond.Set();
            await second.WaitAsync(Timeout);
            Assert.AreEqual(LoadState.Finished, loader.State);
            Assert.AreEqual(2, loader.output);
            Assert.IsNull(loader.Error);
        }
        finally
        {
            finishFirst.Set();
            finishSecond.Set();
            loader.Abort();
            await first.WaitAsync(Timeout);
            await loader.lastRunningTask!.WaitAsync(Timeout);
        }
    }

    [TestMethod]
    public async Task AbortCancelsWorkWithoutPublishingPartialOutput()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new ModLoader.LoaderTask<int, int>("cancel", task =>
        {
            task.output = 123;
            started.SetResult();
            Task.Delay(System.Threading.Timeout.Infinite, task.AbortedToken).GetAwaiter().GetResult();
        });
        loader.Start(1);
        await started.Task.WaitAsync(Timeout);
        var token = loader.AbortedToken;

        loader.Abort();
        await loader.lastRunningTask!.WaitAsync(Timeout);

        Assert.AreEqual(LoadState.Aborted, loader.State);
        Assert.IsTrue(token.IsCancellationRequested);
        Assert.IsNull(loader.Error);
        Assert.AreEqual(0, loader.output);
        Assert.Throws<ObjectDisposedException>(() => _ = token.WaitHandle);
    }

    [TestMethod]
    public async Task OperationCanceledExceptionIsAnAbortedState()
    {
        var loader = new ModLoader.LoaderTask<int, int>("canceled", _ => throw new OperationCanceledException());

        loader.Start(1);
        await loader.lastRunningTask!.WaitAsync(Timeout);

        Assert.AreEqual(LoadState.Aborted, loader.State);
        Assert.IsNull(loader.Error);
        Assert.IsTrue(loader.AbortedToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task FailedRefreshRetainsLastSuccessfulResult()
    {
        var loader = new ModLoader.LoaderTask<int, int>("refresh", task =>
        {
            task.output = task.input;
            if (task.input == 2) throw new IOException("failed refresh");
        });
        loader.Start(1);
        await loader.lastRunningTask!.WaitAsync(Timeout);
        loader.Start(2);
        await loader.lastRunningTask!.WaitAsync(Timeout);

        Assert.AreEqual(LoadState.Failed, loader.State);
        Assert.IsInstanceOfType<IOException>(loader.Error);
        Assert.AreEqual(1, loader.output);

        loader.Start(3);
        await loader.lastRunningTask!.WaitAsync(Timeout);
        Assert.AreEqual(LoadState.Finished, loader.State);
        Assert.AreEqual(3, loader.output);
        Assert.IsNull(loader.Error);
    }

    [TestMethod]
    public async Task PreviewFinishCanReadCompletedOutputOutsideTheWorkerContext()
    {
        var observed = 0;
        var loader = new ModLoader.LoaderTask<int, int>("preview", task => task.output = task.input);
        loader.PreviewFinish += _ =>
        {
            // UI Dispatcher 不需要传播 AsyncLocal，PreviewFinish 仍必须读到此次完成的结果。
            Task observer;
            using (ExecutionContext.SuppressFlow())
                observer = Task.Run(() => observed = loader.output);
            observer.GetAwaiter().GetResult();
        };

        loader.Start(42);
        await loader.lastRunningTask!.WaitAsync(Timeout);

        Assert.AreEqual(42, observed);
        Assert.AreEqual(LoadState.Finished, loader.State);
    }
}
