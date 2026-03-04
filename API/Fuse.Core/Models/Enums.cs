namespace Fuse.Core.Models;

public enum TargetKind { Application, DataStore, External, MessageBroker }

public enum DependencyAuthKind { None, Account, Identity }

public enum RiskTargetType { Application, ApplicationInstance, Dependency, DataStore, Account, ExternalResource }