namespace Fuse.Core.Enums
{
    
    public enum ServiceDeploymentStatus
    {
        Unknown,
        Running,
        Stopped,
        Degraded,
        Maintenance,
        Failed
    }

    public enum ServiceType
    {
        WebApi,
        Worker,
        FunctionApp,
        Database,
        Cache,
        MessageBroker
    }
}