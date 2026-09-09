namespace Wolpertinger.Edge.Kernel;

internal interface IKernelProcessClientFactory
{
    IKernelProcessClient Create();
}

public sealed class KernelProcessClientFactory : IKernelProcessClientFactory
{
    private readonly KernelProcessOptions _options;
    public KernelProcessClientFactory(KernelProcessOptions options)
        => _options = options ?? throw new ArgumentNullException(nameof(options));

    IKernelProcessClient IKernelProcessClientFactory.Create()
        => new KernelProcessClient(_options);
}
