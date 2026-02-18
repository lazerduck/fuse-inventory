namespace Fuse.Core.Models;

public enum TargetKind { Application, DataStore, External }

public enum DependencyAuthKind { None, Account, Identity }

public enum RiskTargetType { Application, ApplicationInstance, Dependency, DataStore, Account, ExternalResource }