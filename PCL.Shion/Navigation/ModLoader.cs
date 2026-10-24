using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using PCL.Shion.Controls;

namespace PCL.Shion.Navigation;

/// <summary>
///     加载器的状态。
/// </summary>
public enum LoadState
{
    Waiting,
    Loading,
    Finished,
    Failed,
    Aborted
}

/// <summary>
///     加载器主动取消时抛出的异常。
/// </summary>
public class CancelledException : Exception
{
}

/// <summary>
///     加载器框架。对应 WPF 的 ModLoader。
/// </summary>
public static class ModLoader
{
    /// <summary>
    ///     加载器的统一基类。
    /// </summary>
    public abstract class LoaderBase : ILoadingTrigger
    {
        public delegate void OnStateChangedThreadEventHandler(LoaderBase loader, LoadState newState,
            LoadState oldState);

        public delegate void OnStateChangedUiEventHandler(LoaderBase loader, LoadState newState, LoadState oldState);

        public delegate void PreviewFinishEventHandler(LoaderBase loader);

        // 等待结束
        public const string waitForExitTimeoutMessage = "等待加载器执行超时。";

        /// <summary>
        ///     用于状态改变检测的同步锁。
        /// </summary>
        public readonly object lockState = new();

        /// <summary>
        ///     使用 LoaderCombo 加载时，该任务是否会阻碍后续任务的进行。
        /// </summary>
        public bool block = true;

        public bool hasOnStateChangedThread;

        /// <summary>
        ///     当前加载器是否由 IsForceRestart 强制调起。
        ///     这个属性自身不会干任何事，而是提供给加载器执行的函数，使得加载器调用另一个加载器时，可以继承强制重启属性。
        /// </summary>
        public bool isForceRestarting;

        /// <summary>
        ///     加载器的名称。
        /// </summary>
        public string name;

        /// <summary>
        ///     父加载器。
        /// </summary>
        public LoaderBase? parent;

        /// <summary>
        ///     该加载器是否显示在列表中。
        /// </summary>
        public bool show = true;

        // 基础属性
        /// <summary>
        ///     加载器的标识编号。
        /// </summary>
        public int Uuid = GetUuid();

        public LoaderBase()
        {
            name = "未命名任务 " + Uuid + "#";
        }

        /// <summary>
        ///     最上级的加载器。
        /// </summary>
        public LoaderBase? RealParent
        {
            get
            {
                try
                {
                    var realParentRet = parent;
                    while (realParentRet is not null && realParentRet.parent is not null)
                        realParentRet = realParentRet.parent;
                    return realParentRet;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        ///     简易的在 UI 线程添加触发事件的方式。主要用于在新建 Loader 时直接使用 With 绑定事件，以及进行老代码兼容。
        /// </summary>
        public Action<LoaderBase> OnStateChanged
        {
            set => OnStateChangedUi += (loader, newState, oldState) => value(loader);
        }

        // 状态监控
        private LoadState _state = LoadState.Waiting;

        /// <summary>
        ///     加载器的状态。
        /// </summary>
        public LoadState State
        {
            get => _state;
            set
            {
                if (_state == value)
                    return;
                var oldState = _state;
                _state = value;
                // 实现 ILoadingTrigger 接口与 OnStateChanged 回调
                RunInUi(() =>
                {
                    switch (value)
                    {
                        case LoadState.Loading:
                        {
                            LoadingState = MyLoading.MyLoadingState.Run;
                            break;
                        }
                        case LoadState.Failed:
                        {
                            LoadingState = MyLoading.MyLoadingState.Error;
                            break;
                        }

                        default:
                        {
                            LoadingState = MyLoading.MyLoadingState.Stop;
                            break;
                        }
                    }

                    OnStateChangedUi?.Invoke(this, value, oldState);
                });
                if (hasOnStateChangedThread)
                    RunInThread(() => OnStateChangedThread?.Invoke(this, value, oldState));
            }
        }

        /// <summary>
        ///     若加载器出错，可提供给外部参考的异常。
        /// </summary>
        public Exception? Error { get; set; }

        // 进度监控
        private double _progress = -1d;

        /// <summary>
        ///     加载器的执行进度，为 0 至 1 的小数。
        /// </summary>
        public virtual double Progress
        {
            get
            {
                switch (State)
                {
                    case LoadState.Waiting:
                    {
                        return 0d;
                    }
                    case LoadState.Loading:
                    {
                        return _progress == -1d ? 0.02d : _progress;
                    }

                    default:
                    {
                        return 1d;
                    }
                }
            }
            set
            {
                if (_progress == value)
                    return;
                var oldValue = _progress;
                _progress = value;
                ProgressChanged?.Invoke(value, oldValue);
            }
        }

        /// <summary>
        ///     计算总进度时的权重。它应该为预计时间（秒）。
        /// </summary>
        public double ProgressWeight { get; set; } = 1d;

        public bool IsLoader { get; } = true;

        private MyLoading.MyLoadingState _loadingState = MyLoading.MyLoadingState.Stop;

        public MyLoading.MyLoadingState LoadingState
        {
            get => _loadingState;
            set
            {
                if (_loadingState == value)
                    return;
                var oldState = _loadingState;
                _loadingState = value;
                LoadingStateChanged?.Invoke(value, oldState);
            }
        }

        public event ILoadingTrigger.LoadingStateChangedEventHandler? LoadingStateChanged;
        public event ILoadingTrigger.ProgressChangedEventHandler? ProgressChanged;

        public virtual void InitParent(LoaderBase? parent)
        {
            this.parent = parent;
        }

        // 事件

        /// <summary>
        ///     当状态改变时，在工作线程触发代码。在添加事件后，必须将 HasOnStateChangedThread 设为 True。
        /// </summary>
        public event OnStateChangedThreadEventHandler? OnStateChangedThread;

        /// <summary>
        ///     当状态改变时，在 UI 线程触发代码。
        /// </summary>
        public event OnStateChangedUiEventHandler? OnStateChangedUi;

        /// <summary>
        ///     在加载器目标事件执行完成，加载器状态即将变为 Finish 时调用。可以视为扩展加载器目标事件。
        /// </summary>
        public event PreviewFinishEventHandler? PreviewFinish;

        protected void RaisePreviewFinish()
        {
            PreviewFinish?.Invoke(this);
        }

        // 状态变化
        public abstract void Start(object? input = null, bool isForceRestart = false);

        public abstract void Abort();

        /// <summary>
        ///     无限期地等待加载器完成，直到结束或抛出异常。若加载器尚未开始，则会开始执行。
        /// </summary>
        public void WaitForExit(object? input = null, LoaderBase? loaderToSyncProgress = null,
            bool isForceRestart = false)
        {
            Start(input, isForceRestart);
            while (State == LoadState.Loading)
            {
                if (loaderToSyncProgress is not null)
                    loaderToSyncProgress.Progress = Progress;
                Thread.Sleep(10);
            }

            if (State == LoadState.Finished)
            {
            }
            else if (State == LoadState.Aborted)
            {
                throw new ThreadInterruptedException("加载器执行已中断。");
            }
            else if (Error is null)
            {
                throw new InvalidOperationException("未知错误！");
            }
            else
            {
                throw Error;
            }
        }

        /// <summary>
        ///     等待加载器完成，直到结束、抛出异常或超时。若加载器尚未开始，则会开始执行。
        /// </summary>
        /// <param name="timeout">等待的超时时间，以毫秒为单位。</param>
        /// <param name="timeoutMessage">若执行超时，将会抛出的异常信息。</param>
        public void WaitForExitTime(int timeout, object? input = null, string timeoutMessage = waitForExitTimeoutMessage,
            LoaderBase? loaderToSyncProgress = null, bool isForceRestart = false)
        {
            Start(input, isForceRestart);
            while (State == LoadState.Loading)
            {
                if (loaderToSyncProgress is not null)
                    loaderToSyncProgress.Progress = Progress;
                Thread.Sleep(10);
                timeout -= 10;
                if (timeout < 0)
                    throw new TimeoutException(timeoutMessage);
            }

            if (State == LoadState.Finished)
            {
            }
            else if (State == LoadState.Aborted)
            {
                throw new ThreadInterruptedException("加载器执行已中断。");
            }
            else if (Error is null)
            {
                throw new InvalidOperationException("未知错误！");
            }
            else
            {
                throw Error;
            }
        }

        // 相同重载
        public override bool Equals(object? obj)
        {
            var @base = obj as LoaderBase;
            return @base is not null && Uuid == @base.Uuid;
        }

        public override int GetHashCode() => Uuid;
    }

    /// <summary>
    ///     加载器任务的无泛型基类。
    /// </summary>
    public abstract class LoaderTask : LoaderBase
    {
        /// <summary>
        ///     上次完成加载时的时间。
        /// </summary>
        public long lastFinishedTime;

        /// <summary>
        ///     最后一次运行加载器的线程。可能为 Nothing，或线程已结束。
        /// </summary>
        public Task? lastRunningTask;

        /// <summary>
        ///     在输入相同时使用原有结果的超时，单位为毫秒。
        /// </summary>
        public int reloadTimeout = -1;

        // 状态指示
        /// <summary>
        ///     当前执行线程是否应当中断。只应用在加载器的工作线程中判断，不可跨线程调用。
        /// </summary>
        public bool IsAborted => IsAbortedWithThread(Task.CurrentId ?? -1);

        /// <summary>
        ///     当前执行线程是否应当中断。需要手动提供加载器线程，用于需要跨线程检查的情况。
        /// </summary>
        public bool IsAbortedWithThread(int compareTaskId)
        {
            return lastRunningTask is null || compareTaskId != lastRunningTask.Id ||
                   State == LoadState.Aborted;
        }

        public abstract bool ShouldStart(ref object? input, bool isForceRestart = false,
            bool ignoreReloadTimeout = false);

        // 装箱！装箱！装箱圣地！
        public abstract object? StartGetInputNoType(object? input = null, Func<object?>? inputDelegate = null);
    }

    /// <summary>
    ///     用于异步执行并监控单一函数的加载器。
    /// </summary>
    public class LoaderTask<InputType, OutputType> : LoaderTask
    {
        // 输入输出
        public InputType? input;
        protected internal Func<InputType?>? inputDelegate;

        // 执行事件
        protected internal Action<LoaderTask<InputType, OutputType>> loadDelegate;
        public OutputType? output = default;

        private CancellationTokenSource? cancelToken;

        public CancellationToken AbortedToken => cancelToken?.Token ?? new CancellationToken(true);

        // 线程设定
        protected internal ThreadPriority threadPriority;

        public LoaderTask(string name, Action<LoaderTask<InputType, OutputType>> loadDelegate,
            Func<InputType?>? inputDelegate = null, ThreadPriority priority = ThreadPriority.Normal)
        {
            this.name = name;
            this.loadDelegate = loadDelegate;
            this.inputDelegate = inputDelegate;
        }

        // 获取输入
        public InputType? StartGetInput(InputType? input = default, Func<InputType?>? inputDelegate = null)
        {
            inputDelegate ??= this.inputDelegate;
            // 按照龙猫的逻辑，此处将 input 与默认值直接进行等价比较，若相等则认为 input 未传入具体值，而调用 inputDelegate 获取
            // 这种逻辑未考虑值类型恰好传入 default 值 (如 double 传了 0.0) 的情况，这是一个陷阱，可能会产生 undefined behavior
            if (EqualityComparer<InputType?>.Default.Equals(input, default) && inputDelegate is not null)
                RunInUiWait(() => input = inputDelegate());
            return input;
        }

        public override object? StartGetInputNoType(object? input = null, Func<object?>? inputDelegate = null)
        {
            return StartGetInput(input is null ? default : (InputType?)input,
                inputDelegate is null ? null : () => (InputType?)inputDelegate());
        }

        // 代码执行
        public override bool ShouldStart(ref object? input, bool isForceRestart = false,
            bool ignoreReloadTimeout = false)
        {
            // 获取输入
            try
            {
                input = StartGetInput(ToGenericParameter<InputType>(input));
            }
            catch (Exception ex)
            {
                Error = ex;
                lock (lockState)
                {
                    State = LoadState.Failed;
                }
            }

            // 检验输入以确定情况
            if (isForceRestart)
                return true; // 强制要求重启
            if (input is null != this.input is null || (input is not null && !input.Equals(this.input)))
                return true; // 输入不同
            if ((State == LoadState.Loading || State == LoadState.Finished) &&
                (ignoreReloadTimeout || reloadTimeout == -1 || lastFinishedTime == 0L ||
                 Environment.TickCount64 - lastFinishedTime < reloadTimeout)) // 正在加载或已结束
                // 没有超时
                return false; // 则不重试

            return true;
            // 需要开始
        }

        public override void Start(object? input = null, bool isForceRestart = false)
        {
            // 确认是否开始加载
            if (ShouldStart(ref input, isForceRestart))
            {
                // 输入不同或失败，开始加载
                if (State == LoadState.Loading)
                    TriggerThreadAbort();
                this.input = ToGenericParameter<InputType>(input);
                lock (lockState)
                {
                    State = LoadState.Loading;
                    Progress = -1;
                }
            }
            else
            {
                return;
            }

            // 如果线程是因为判断到 IsAborted 而提前中止，则代表已有新线程被重启，此时不应当改为 Aborted
            // 如果线程是在没有 IsAborted 时手动引发了 ThreadInterruptedException，则代表没有重启线程，这通常代表用户手动取消，应当改为 Aborted
            lastRunningTask = Task.Run(() =>
            {
                try
                {
                    isForceRestarting = isForceRestart;
                    loadDelegate(this);
                    if (IsAborted)
                        return;

                    RaisePreviewFinish();
                    State = LoadState.Finished;
                    lastFinishedTime = Environment.TickCount64;
                }
                catch (CancelledException)
                {
                    if (!IsAborted) State = LoadState.Aborted;
                }
                catch (ThreadInterruptedException)
                {
                    if (!IsAborted) State = LoadState.Aborted;
                }
                catch (Exception ex)
                {
                    if (IsAborted) return;
                    Error = ex;
                    State = LoadState.Failed;
                }
            }, (cancelToken ??= new CancellationTokenSource()).Token); // 未中断，本次输出有效
        }

        public override void Abort()
        {
            if (State != LoadState.Loading)
                return;
            lock (lockState)
            {
                State = LoadState.Aborted;
            }

            TriggerThreadAbort();
        }

        private void TriggerThreadAbort()
        {
            if (lastRunningTask is null) return;
            if (!lastRunningTask.IsCompleted) cancelToken?.Cancel();
            lastRunningTask = null;
            cancelToken = null;
        }

        private static T? ToGenericParameter<T>(object? value)
        {
            return value is null ? default : (T?)value;
        }
    }

    /// <summary>
    ///     支持多个加载器连续运作的复合加载器。
    /// </summary>
    public class LoaderCombo : LoaderBase
    {
        public object? input;

        public List<LoaderBase> loaders = new();

        public LoaderCombo(string name, IEnumerable<LoaderBase> loaders)
        {
            this.loaders.Clear();
            foreach (var Loader in loaders)
                if (Loader is not null)
                {
                    this.loaders.Add(Loader);
                    Loader.OnStateChangedThread += SubTaskStateChanged;
                    Loader.hasOnStateChangedThread = true;
                }

            InitParent(null);
            this.name = name;
        }

        public override double Progress
        {
            get
            {
                switch (State)
                {
                    case LoadState.Waiting:
                    {
                        return 0d;
                    }
                    case LoadState.Loading:
                    {
                        var total = 0d;
                        var finished = 0d;
                        foreach (var Loader in loaders)
                        {
                            total += Loader.ProgressWeight;
                            finished += Loader.ProgressWeight * Loader.Progress;
                        }

                        if (total == 0d)
                            return 0d;
                        return finished / total;
                    }

                    default:
                    {
                        return 1d;
                    }
                }
            }
            set => throw new Exception("多重加载器不支持设置进度");
        }

        public override void InitParent(LoaderBase? parent)
        {
            this.parent = parent;
            foreach (var Loader in loaders)
                Loader.InitParent(this);
        }

        public override void Start(object? input = null, bool isForceRestart = false)
        {
            isForceRestarting = isForceRestart;
            lock (lockState)
            {
                if (State == LoadState.Loading) return;

                State = LoadState.Loading;
            }

            // 启动加载
            this.input = input;
            if (isForceRestart)
                foreach (var Loader in loaders)
                    Loader.State = LoadState.Waiting;
            RunInThread(Update);
        }

        public override void Abort()
        {
            lock (lockState)
            {
                if (State != LoadState.Loading && State != LoadState.Waiting)
                    return;
            }

            foreach (var Loader in loaders) Loader.Abort();

            lock (lockState)
            {
                if (State == LoadState.Loading || State == LoadState.Waiting)
                    State = LoadState.Aborted;
            }
        }

        /// <summary>
        ///     子任务状态变更。
        /// </summary>
        private void SubTaskStateChanged(LoaderBase loader, LoadState newState, LoadState oldState)
        {
            switch (newState)
            {
                case LoadState.Loading:
                {
                    break;
                }
                // 开始，啥都不干
                case LoadState.Waiting:
                {
                    break;
                }
                // 子加载器可能由于外部输入改变而暂时变为 Waiting，之后会立即重新启动
                // 所以啥都不干就行
                case LoadState.Finished:
                {
                    // 正常结束，触发刷新
                    RunInThread(Update);
                    break;
                }
                case LoadState.Aborted:
                {
                    // 被中断，这个任务也中断
                    Abort();
                    break;
                }

                default:
                {
                    // 完蛋，出错了
                    lock (lockState)
                    {
                        if (State >= LoadState.Finished)
                            return;
                        Error = new Exception(loader.name + "失败", loader.Error);
                        State = loader.State;
                    }

                    foreach (var currentLoader in loaders)
                        currentLoader.Abort();

                    return;
                }
            }
        }

        /// <summary>
        ///     触发一次更新，以启动新加载器或完成。
        /// </summary>
        private void Update()
        {
            if (State == LoadState.Finished
                || State == LoadState.Failed
                || State == LoadState.Aborted)
                return;

            var isFinished = true;
            var blocked = false;
            object? input = this.input;

            foreach (var loader in loaders)
            {
                var restart = false;
                switch (loader.State)
                {
                    case LoadState.Finished:
                    {
                        if (loader is LoaderTask task)
                        {
                            var genericArg = task.GetType().GenericTypeArguments.FirstOrDefault();
                            var shouldInput = input is not null && genericArg == input.GetType()
                                ? input
                                : null;

                            if (task.ShouldStart(ref shouldInput, false, true))
                            {
                                restart = true;
                                break;
                            }

                            input = GetOutput(loader);
                        }

                        if (loader.block && !isFinished)
                            blocked = true;

                        break;
                    }

                    case LoadState.Loading:
                    {
                        if (loader is LoaderTask task)
                        {
                            var genericArg = loader.GetType().GenericTypeArguments.FirstOrDefault();
                            var shouldInput = input is not null && genericArg == input.GetType()
                                ? input
                                : null;
                            if (task.ShouldStart(ref shouldInput, false, true))
                            {
                                restart = true;
                                break;
                            }
                        }

                        isFinished = false;
                        blocked = true;
                        break;
                    }

                    default:
                        restart = true;
                        break;
                }

                if (restart)
                {
                    isFinished = false;

                    if (blocked)
                        continue;

                    if (input is not null)
                    {
                        switch (loader)
                        {
                            case LoaderTask:
                            case LoaderCombo:
                                var genericArg = loader.GetType().GenericTypeArguments.FirstOrDefault();

                                loader.Start(genericArg == input.GetType() ? input : null, isForceRestarting);
                                break;
                            default:
                                loader.Start(null, isForceRestarting);
                                break;
                        }
                    }
                    else
                    {
                        loader.Start(isForceRestart: isForceRestarting);
                    }

                    if (loader.block)
                        blocked = true;
                }
            }

            if (isFinished)
            {
                RaisePreviewFinish();
                State = LoadState.Finished;
            }
        }

        /// <summary>
        ///     获得最底层的，应被显示给用户的加载器列表，并追加于 List。
        /// </summary>
        public static void GetLoaderList(LoaderCombo loader, ref List<LoaderBase> list, bool requireShow = true)
        {
            foreach (var SubLoader in loader.loaders)
            {
                if (SubLoader.show || !requireShow)
                    list.Add(SubLoader);
                if (SubLoader is LoaderCombo combo)
                    GetLoaderList(combo, ref list, requireShow);
            }
        }

        /// <summary>
        ///     获得最底层的，应被显示给用户的加载器列表，并追加于 List。
        /// </summary>
        public void GetLoaderList(ref List<LoaderBase> list, bool requireShow = true)
        {
            GetLoaderList(this, ref list, requireShow);
        }

        /// <summary>
        ///     获得最底层的，应被显示给用户的加载器列表。
        /// </summary>
        public List<LoaderBase> GetLoaderList(bool requireShow = true)
        {
            var list = new List<LoaderBase>();
            GetLoaderList(ref list, requireShow);
            return list;
        }
    }

    /// <summary>
    ///     支持多个加载器连续运作的复合加载器（泛型版本）。
    /// </summary>
    public class LoaderCombo<InputType> : LoaderCombo
    {
        public InputType? typedInput;

        public LoaderCombo(string name, IEnumerable<LoaderBase> loaders) : base(name, loaders)
        {
        }

        public override void Start(object? input = null, bool isForceRestart = false)
        {
            typedInput = input is null ? default : (InputType?)input;
            base.Start(typedInput, isForceRestart);
        }
    }

    #region 任务列表与文件夹加载

    /// <summary>
    ///     文件夹检测类加载器的运行方式。对应 WPF 的 ModLoader.LoaderFolderRunType。
    /// </summary>
    public enum LoaderFolderRunType
    {
        /// <summary>文件夹有更新时才运行。</summary>
        RunOnUpdated,

        /// <summary>无论是否有更新都强制运行。</summary>
        ForceRun,

        /// <summary>只更新检测记录，不运行加载器。</summary>
        UpdateOnly
    }

    private sealed record LoaderFolderEntry(string FolderPath, DateTime? LastCheckTime);

    private static readonly object taskbarLock = new();

    /// <summary>
    ///     当前处于任务列表中的加载器。对应 WPF 的 ModLoader.loaderTaskbar。
    /// </summary>
    public static readonly List<LoaderBase> loaderTaskbar = new();

    /// <summary>
    ///     平滑后的总进度。对应 WPF 的 ModLoader.loaderTaskbarProgress。
    /// </summary>
    public static double loaderTaskbarProgress;

    // 文件夹检测类加载器的上次检测记录
    private static readonly Dictionary<LoaderBase, LoaderFolderEntry> loaderFolderDictionary = new();

    /// <summary>
    ///     任务列表中是否有正在进行的任务。对应 WPF 的 ModNet.HasDownloadingTask。
    /// </summary>
    public static bool HasDownloadingTask()
    {
        lock (taskbarLock)
            return loaderTaskbar.Any(l => l.State == LoadState.Loading);
    }

    /// <summary>
    ///     将加载器加入任务列表。
    /// </summary>
    public static void LoaderTaskbarAdd<T>(LoaderCombo<T> loader)
    {
        // TODO(shion): WPF 版会同时从速度页左侧的任务列表中移除该加载器，Shion 的速度页尚未提供对应的实例入口。
        lock (taskbarLock)
        {
            if (!loaderTaskbar.Contains(loader))
                loaderTaskbar.Add(loader);
        }

        ModBase.Log($"[Taskbar] {loader.name} 已加入任务列表");
    }

    /// <summary>
    ///     刷新任务列表：移除已结束或已中止的加载器，并更新平滑后的进度。
    /// </summary>
    public static void LoaderTaskbarProgressRefresh()
    {
        try
        {
            var removed = new List<LoaderBase>();
            lock (taskbarLock)
            {
                var allStopped = loaderTaskbar.All(l => l.State != LoadState.Loading);
                foreach (var task in loaderTaskbar.ToList())
                {
                    if (allStopped || task.State == LoadState.Waiting || task.State == LoadState.Aborted)
                    {
                        loaderTaskbar.Remove(task);
                        removed.Add(task);
                    }
                }
            }

            foreach (var task in removed)
                ModBase.Log($"[Taskbar] {task.name} 已移出任务列表");

            // 更新平滑后的进度
            var newProgress = LoaderTaskbarProgressGet();
            if (newProgress <= 0d || newProgress >= 1d || loaderTaskbarProgress > newProgress)
                loaderTaskbarProgress = newProgress;
            else
                loaderTaskbarProgress = loaderTaskbarProgress * 0.9d + newProgress * 0.1d;

            // TODO(shion): 任务栏进度条与下载按钮进度在 Avalonia 中尚未实现，这里仅维护进度数值。
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新任务栏进度显示失败", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     获取任务列表中所有加载器的平均进度，范围为 0 到 1。任务列表为空时返回 1。
    /// </summary>
    public static double LoaderTaskbarProgressGet()
    {
        try
        {
            lock (taskbarLock)
            {
                if (loaderTaskbar.Count == 0)
                    return 1d;
                return Math.Clamp(loaderTaskbar.Select(l => l.Progress).Average(), 0d, 1d);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取任务栏进度出错", ModBase.LogLevel.Feedback);
            return 0.5d;
        }
    }

    /// <summary>
    ///     执行以文件夹检测作为输入的加载器。加载器需以文件夹路径为输入值。
    ///     返回是否执行了加载器。
    /// </summary>
    /// <param name="loader">要执行的加载器。</param>
    /// <param name="folderPath">需要检测的文件夹路径。</param>
    /// <param name="type">运行方式。</param>
    /// <param name="maxDepth">检测文件夹修改时间的最大子目录深度。</param>
    /// <param name="extraPath">用于检查文件夹修改的额外路径。该路径不会传入加载器。</param>
    /// <param name="waitForExit">是否等待加载器结束。</param>
    /// <param name="loaderInput">如果不想要文件夹路径为输入值，则传入期望数据。</param>
    public static bool LoaderFolderRun(LoaderBase loader, string folderPath, LoaderFolderRunType type, int maxDepth = 0,
        string extraPath = "", bool waitForExit = false, object? loaderInput = null)
    {
        var fullPath = folderPath + extraPath;
        DateTime? lastCheckTime = null;
        try
        {
            // 获取数据
            var folderInfo = new System.IO.DirectoryInfo(fullPath);
            lastCheckTime = folderInfo.Exists ? GetActualLastWriteTimeUtc(folderInfo, maxDepth) : null;
            // 如果已经检查过，则跳过
            if (type == LoaderFolderRunType.RunOnUpdated && loaderFolderDictionary.TryGetValue(loader, out var last))
            {
                if (folderInfo.Exists)
                {
                    if (last.LastCheckTime is not null &&
                        new LoaderFolderEntry(fullPath, lastCheckTime) == last)
                        return false;
                }
                else if (last.LastCheckTime is null)
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "文件夹加载器启动检测出错");
        }

        // 写入检查数据
        loaderFolderDictionary[loader] = new LoaderFolderEntry(fullPath, lastCheckTime);
        // 开始检查
        if (type == LoaderFolderRunType.UpdateOnly)
            return false;
        if (waitForExit)
            loader.WaitForExit(loaderInput ?? folderPath, isForceRestart: true);
        else
            loader.Start(loaderInput ?? folderPath, true);
        return true;
    }

    private static DateTime GetActualLastWriteTimeUtc(System.IO.DirectoryInfo folderInfo, int maxDepth)
    {
        var time = folderInfo.LastWriteTimeUtc;
        if (maxDepth > 0)
            foreach (var folder in folderInfo.EnumerateDirectories())
            {
                var folderTime = GetActualLastWriteTimeUtc(folder, maxDepth - 1);
                if (folderTime > time)
                    time = folderTime;
            }

        return time;
    }

    #endregion

    #region 基础工具

    private static int uuidSeed;

    internal static int GetUuid() => Interlocked.Increment(ref uuidSeed);

    internal static void RunInUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    internal static void RunInUiWait(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Invoke(action);
    }

    internal static void RunInThread(Action action)
    {
        Task.Run(action);
    }

    private static object? GetOutput(LoaderBase loader)
    {
        return loader.GetType().GetField("output")?.GetValue(loader);
    }

    #endregion
}
