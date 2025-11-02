export interface ServiceManifest {
    id: string;
    name: string;
    version: string | null;
    description: string | null;
    notes: string | null;
    author: string | null;
    framework: string | null;
    createdAt: Date | null;
    updatedAt: Date | null;
    repositoryUrl: string | null;
    type: ServiceTypes;
    deploymentPipelines: Array<DeploymentPipeline>;
    deployments: Array<Deployments>;
    tags: Array<string>;
}

interface DeploymentPipeline {
    name: string;
    url: string | null;
}

interface Deployments {
    environment: string;
    url: string | null;
    swaggerUrl: string | null;
    healthUrl: string | null;
    status: DeploymentStatuses;
}

const ServiceTypes = {
    WebApi: 'WebApi',
    Worker: 'Worker',
    FunctionApp: 'FunctionApp',
    Database: 'Database',
    Cache: 'Cache',
    MessageBroker: 'MessageBroker'
} as const;

export type ServiceTypes = typeof ServiceTypes[keyof typeof ServiceTypes];

const DeploymentStatuses = {
    Unknown: 'Unknown',
    Running: 'Running',
    Stopped: 'Stopped',
    Degraded: 'Degraded',
    Maintenance: 'Maintenance',
    Failed: 'Failed'
} as const;

export type DeploymentStatuses = typeof DeploymentStatuses[keyof typeof DeploymentStatuses];

export type ServiceManifestCreate = Omit<
	ServiceManifest,
	"id" | "createdAt" | "updatedAt"
> & {
	id?: string;
	createdAt?: Date | string | null;
	updatedAt?: Date | string | null;
};