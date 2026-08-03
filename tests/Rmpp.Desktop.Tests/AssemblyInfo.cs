using Xunit;

// WPF 的 Dispatcher、系统画刷和渲染目标包含进程级状态，桌面测试必须串行运行以避免跨 STA 干扰。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
