namespace Fuse.Core.Models;

public enum TargetKind { Application, DataStore, External, MessageBroker }

public enum DependencyAuthKind { None, Account, Identity, ApiKey }

public enum DependencySeverity { Partial, Full }

public enum RiskTargetType { Application, ApplicationInstance, Dependency, DataStore, Account, ExternalResource }