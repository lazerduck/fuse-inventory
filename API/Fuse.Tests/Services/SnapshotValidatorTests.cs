
using System;
using System.Collections.Generic;
using System.Linq;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Xunit;

namespace Fuse.Tests.Services;

public class SnapshotValidatorTests
{
    [Fact]
    public void Validate_ReturnsNoErrors_ForValidSnapshot()
    {
        var tagId = Guid.NewGuid();
        var envId = Guid.NewGuid();
        var platformId = Guid.NewGuid();

        var tags = new List<Tag> { new Tag(tagId, "tag1", null, null) };
        var envs = new List<EnvironmentInfo> { new EnvironmentInfo(envId, "env1", null, new HashSet<Guid>()) };
        var platforms = new List<Platform> {
            new Platform(platformId, "platform1", "host.example.com", "linux", PlatformKind.Server, null, null, new HashSet<Guid>{ tagId }, DateTime.UtcNow, DateTime.UtcNow)
        };

        var snapshot = new Snapshot(
            new List<Application>(),
            new List<DataStore>(),
            platforms,
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            tags,
            envs,
            new List<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ReturnsNoErrors_ForComplexValidSnapshot()
    {
        // IDs
        var tagId = Guid.NewGuid();
        var envId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var dsId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var instId = Guid.NewGuid();
        var extId = Guid.NewGuid();

        // Entities
        var tags = new List<Tag> { new Tag(tagId, "t1", null, null) };
        var envs = new List<EnvironmentInfo> { new EnvironmentInfo(envId, "env", null, new HashSet<Guid>()) };
        var platforms = new List<Platform> { new Platform(platformId, "srv", "host.example.com", "linux", PlatformKind.Server, null, null, new HashSet<Guid>{ tagId }, DateTime.UtcNow, DateTime.UtcNow) };
        var dataStores = new List<DataStore> { new DataStore(dsId, "db", null, "postgres", envId, platformId, new Uri("postgres://host"), new HashSet<Guid>{ tagId }, DateTime.UtcNow, DateTime.UtcNow) };
        var externals = new List<ExternalResource> { new ExternalResource(extId, "ext", null, new Uri("https://ext"), new HashSet<Guid>{ tagId }, DateTime.UtcNow, DateTime.UtcNow) };

        var instDeps = new List<ApplicationInstanceDependency>
        {
            new ApplicationInstanceDependency(Guid.NewGuid(), dsId, TargetKind.DataStore, null, DependencyAuthKind.None, null, null),
            new ApplicationInstanceDependency(Guid.NewGuid(), extId, TargetKind.External, null, DependencyAuthKind.None, null, null)
        };
        var instances = new List<ApplicationInstance>
        {
            new ApplicationInstance(instId, envId, platformId, new Uri("https://app"), null, null, "1.0", instDeps, new HashSet<Guid>{ tagId }, DateTime.UtcNow, DateTime.UtcNow)
        };
        var apps = new List<Application> { new Application(appId, "app", "1.0", null, null, null, null, null, null, new HashSet<Guid>{ tagId }, instances, new List<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow) };

        var accounts = new List<Account>
        {
            new Account(Guid.NewGuid(), instId, TargetKind.Application, AuthKind.None, new SecretBinding(SecretBindingKind.PlainReference, "secret", null), null, null, new List<Grant>(), new HashSet<Guid>{ tagId }, DateTime.UtcNow, DateTime.UtcNow),
            new Account(Guid.NewGuid(), dsId, TargetKind.DataStore, AuthKind.None, new SecretBinding(SecretBindingKind.PlainReference, "secret", null), null, null, new List<Grant>(), new HashSet<Guid>{ tagId }, DateTime.UtcNow, DateTime.UtcNow),
            new Account(Guid.NewGuid(), extId, TargetKind.External, AuthKind.None, new SecretBinding(SecretBindingKind.PlainReference, "secret", null), null, null, new List<Grant>(), new HashSet<Guid>{ tagId }, DateTime.UtcNow, DateTime.UtcNow)
        };

        var security = new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>());

    var snapshot = new Snapshot(apps, dataStores, platforms, externals, accounts, new List<Identity>(), tags, envs, new List<KumaIntegration>(), new List<SecretProvider>(), new List<SqlIntegration>(), security);

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ExternalResource_Reports_MissingTags()
    {
        var erId = Guid.NewGuid();
        var missingTag = Guid.NewGuid();
        var snapshot = new Snapshot(
            new List<Application>(),
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource> { new ExternalResource(erId, "ext", null, new Uri("https://ext"), new HashSet<Guid> { missingTag }, DateTime.UtcNow, DateTime.UtcNow) },
            new List<Account>(),
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo>(),
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("ExternalResource") && e.Contains("tag") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_Account_TargetMissing_Application()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), TargetKind.Application, AuthKind.None, new SecretBinding(SecretBindingKind.PlainReference, "secret", null), null, null, new List<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var snapshot = new Snapshot(
            new List<Application>(),
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account> { account },
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo>(),
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("Account") && e.Contains("Application") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_Account_TargetMissing_DataStore()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), TargetKind.DataStore, AuthKind.None, new SecretBinding(SecretBindingKind.PlainReference, "secret", null), null, null, new List<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var snapshot = new Snapshot(
            new List<Application>(),
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account> { account },
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo>(),
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("Account") && e.Contains("DataStore") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_Account_TargetMissing_External()
    {
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), TargetKind.External, AuthKind.None, new SecretBinding(SecretBindingKind.PlainReference, "secret", null), null, null, new List<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var snapshot = new Snapshot(
            new List<Application>(),
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account> { account },
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo>(),
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("Account") && e.Contains("External") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_AppInstance_DependencyMissing_Application()
    {
        var envId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var instId = Guid.NewGuid();
        var missingAppId = Guid.NewGuid();

        var inst = new ApplicationInstance(instId, envId, null, new Uri("https://svc"), null, null, null,
            new List<ApplicationInstanceDependency>{ new ApplicationInstanceDependency(Guid.NewGuid(), missingAppId, TargetKind.Application, null, DependencyAuthKind.None, null, null) },
            new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);

        var snapshot = new Snapshot(
            new List<Application> { new Application(appId, "app", null, null, null, null, null, null, null, new HashSet<Guid>(), new List<ApplicationInstance> { inst }, new List<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow) },
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo> { new EnvironmentInfo(envId, "env", null, new HashSet<Guid>()) },
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("ApplicationInstance") && e.Contains("dependency") && e.Contains("Application"));
    }

    [Fact]
    public void Validate_AppInstance_DependencyMissing_DataStore()
    {
        var envId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var instId = Guid.NewGuid();
        var missingDsId = Guid.NewGuid();

        var inst = new ApplicationInstance(instId, envId, null, new Uri("https://svc"), null, null, null,
            new List<ApplicationInstanceDependency>{ new ApplicationInstanceDependency(Guid.NewGuid(), missingDsId, TargetKind.DataStore, null, DependencyAuthKind.None, null, null) },
            new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);

        var snapshot = new Snapshot(
            new List<Application> { new Application(appId, "app", null, null, null, null, null, null, null, new HashSet<Guid>(), new List<ApplicationInstance> { inst }, new List<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow) },
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo> { new EnvironmentInfo(envId, "env", null, new HashSet<Guid>()) },
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("ApplicationInstance") && e.Contains("dependency") && e.Contains("DataStore"));
    }

    [Fact]
    public void Validate_AppInstance_DependencyMissing_External()
    {
        var envId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var instId = Guid.NewGuid();
        var missingExtId = Guid.NewGuid();

        var inst = new ApplicationInstance(instId, envId, null, new Uri("https://svc"), null, null, null,
            new List<ApplicationInstanceDependency>{ new ApplicationInstanceDependency(Guid.NewGuid(), missingExtId, TargetKind.External, null, DependencyAuthKind.None, null, null) },
            new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);

        var snapshot = new Snapshot(
            new List<Application>{ new Application(appId, "app", null, null, null, null, null, null, null, new HashSet<Guid>(), new List<ApplicationInstance>{ inst }, new List<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow) },
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo>{ new EnvironmentInfo(envId, "env", null, new HashSet<Guid>()) },
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("ApplicationInstance") && e.Contains("dependency") && e.Contains("External"));
    }

    [Fact]
    public void Validate_Application_Reports_MissingTags()
    {
        var envId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var snapshot = new Snapshot(
            new List<Application>
            {
                new Application(appId, "app", null, null, null, null, null, null, null, new HashSet<Guid>{ Guid.NewGuid() }, new List<ApplicationInstance>(), new List<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow)
            },
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo>{ new EnvironmentInfo(envId, "env", null, new HashSet<Guid>()) },
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("Application") && e.Contains("tag") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_ApplicationInstance_Reports_MissingEnvironment()
    {
        var appId = Guid.NewGuid();
        var instId = Guid.NewGuid();
        var snapshot = new Snapshot(
            new List<Application>
            {
                new Application(appId, "app", null, null, null, null, null, null, null, new HashSet<Guid>(),
                    new List<ApplicationInstance>{ new ApplicationInstance(instId, Guid.NewGuid(), null, new Uri("https://example.com"), null, null, null, new List<ApplicationInstanceDependency>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow) },
                    new List<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow)
            },
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo>(),
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("ApplicationInstance") && e.Contains("environment") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_ApplicationInstance_Reports_MissingPlatform_WhenReferenced()
    {
        var envId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var instId = Guid.NewGuid();
        var missingPlatformId = Guid.NewGuid();
        var snapshot = new Snapshot(
            new List<Application>
            {
                new Application(appId, "app", null, null, null, null, null, null, null, new HashSet<Guid>(),
                    new List<ApplicationInstance>{ new ApplicationInstance(instId, envId, missingPlatformId, new Uri("https://example.com"), null, null, null, new List<ApplicationInstanceDependency>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow) },
                    new List<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow)
            },
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo>{ new EnvironmentInfo(envId, "env", null, new HashSet<Guid>()) },
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("ApplicationInstance") && e.Contains("platform") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_ApplicationInstance_Reports_MissingTags()
    {
        var envId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var instId = Guid.NewGuid();
        var missingTag = Guid.NewGuid();
    var snapshot = new Snapshot(
            new List<Application>
            {
                new Application(appId, "app", null, null, null, null, null, null, null, new HashSet<Guid>(),
                    new List<ApplicationInstance>{ new ApplicationInstance(instId, envId, null, new Uri("https://example.com"), null, null, null, new List<ApplicationInstanceDependency>(), new HashSet<Guid>{ missingTag }, DateTime.UtcNow, DateTime.UtcNow) },
                    new List<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow)
            },
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo>{ new EnvironmentInfo(envId, "env", null, new HashSet<Guid>()) },
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("ApplicationInstance") && e.Contains("tag") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_DataStore_Reports_MissingEnvironment()
    {
        var envId = Guid.NewGuid();
        var dsId = Guid.NewGuid();
        var snapshot = new Snapshot(
            new List<Application>(),
            new List<DataStore>
            {
                new DataStore(dsId, "db", null, "postgres", envId, null, new Uri("postgres://host"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow)
            },
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo>(),
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("DataStore") && e.Contains("environment") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_DataStore_Reports_MissingPlatform_WhenReferenced()
    {
        var envId = Guid.NewGuid();
        var missingPlatformId = Guid.NewGuid();
        var dsId = Guid.NewGuid();
        var snapshot = new Snapshot(
            new List<Application>(),
            new List<DataStore>
            {
                new DataStore(dsId, "db", null, "postgres", envId, missingPlatformId, new Uri("postgres://host"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow)
            },
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            new List<Tag>(),
            new List<EnvironmentInfo>{ new EnvironmentInfo(envId, "env", null, new HashSet<Guid>()) },
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains(errors, e => e.Contains("DataStore") && e.Contains("platform") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_ReturnsError_WhenTagMissing()
    {
        var envId = Guid.NewGuid();
        var missingTagId = Guid.NewGuid();
        var platformId = Guid.NewGuid();

        var tags = new List<Tag>();
        var envs = new List<EnvironmentInfo> { new EnvironmentInfo(envId, "env1", null, new HashSet<Guid>()) };
        var platforms = new List<Platform> {
            new Platform(platformId, "platform1", "host.example.com", "linux", PlatformKind.Server, null, null, new HashSet<Guid>{ missingTagId }, DateTime.UtcNow, DateTime.UtcNow)
        };

        var snapshot = new Snapshot(
            new List<Application>(),
            new List<DataStore>(),
            platforms,
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            tags,
            envs,
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains("Platform", errors[0]);
        Assert.Contains("not found", errors[0]);
    }



    [Fact]
    public void Validate_ReturnsError_WhenDuplicateTagIds()
    {
        var id = Guid.NewGuid();
        var tags = new List<Tag> { new Tag(id, "tag1", null, null), new Tag(id, "tag2", null, null) };
        var snapshot = new Snapshot(
            new List<Application>(),
            new List<DataStore>(),
            new List<Platform>(),
            new List<ExternalResource>(),
            new List<Account>(),
            new List<Identity>(),
            tags,
            new List<EnvironmentInfo>(),
            new List<KumaIntegration>(),
            new List<SecretProvider>(),
            new List<SqlIntegration>(),
            new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var errors = SnapshotValidator.Validate(snapshot);
        Assert.Contains("Duplicate Tag Ids detected", errors);
    }
}
