import type { ServiceManifest, ServiceManifestCreate } from "../classes/ServiceManifest";

// Tiny contract
// - Base URL: defaults to import.meta.env.VITE_API_BASE_URL or '/api'
// - Resource path: '/Services'
// - Methods mirror API: list, get, create, update, delete
// - Dates (createdAt/updatedAt) are revived to Date instances


export interface ServicesClientOptions {
	baseUrl?: string; // e.g. http://localhost:5188/api or just '/api'
	headers?: HeadersInit;
	fetchFn?: typeof fetch;
}

export class ServicesClient {
	private readonly baseUrl: string;
	private readonly fetchFn: typeof fetch;
	private readonly defaultHeaders: HeadersInit;

	constructor(options: ServicesClientOptions = {}) {
		const envBase = (import.meta as any)?.env?.VITE_API_BASE_URL as
			| string
			| undefined;
		const base = options.baseUrl ?? envBase ?? "/api";
		// Normalize: no trailing slash
		this.baseUrl = base.replace(/\/$/, "");
		this.fetchFn = options.fetchFn ?? fetch.bind(globalThis);
		this.defaultHeaders = {
			"Content-Type": "application/json",
			...(options.headers ?? {}),
		};
	}

	// GET /api/Services
	async list(): Promise<ServiceManifest[]> {
		const res = await this.request<ServiceManifest[]>("/Services");
		return res.map(reviveServiceManifestDates);
	}

	// GET /api/Services/{id}
	async get(id: string): Promise<ServiceManifest | null> {
		try {
			const res = await this.request<ServiceManifest>(`/Services/${id}`);
			return reviveServiceManifestDates(res);
		} catch (err: any) {
			if (err?.status === 404) return null;
			throw err;
		}
	}

	// POST /api/Services
	async create(payload: ServiceManifestCreate): Promise<ServiceManifest> {
		const body = JSON.stringify(serializeDates(payload));
		const res = await this.request<ServiceManifest>("/Services", {
			method: "POST",
			body,
		});
		return reviveServiceManifestDates(res);
	}

	// PUT /api/Services/{id}
	async update(manifest: ServiceManifest): Promise<void> {
		if (!manifest?.id) {
			throw new Error("update(manifest) requires manifest.id");
		}
		const body = JSON.stringify(serializeDates(manifest));
		await this.request<void>(`/Services/${manifest.id}`, {
			method: "PUT",
			body,
		});
	}

	// DELETE /api/Services/{id}
	async delete(id: string): Promise<void> {
		await this.request<void>(`/Services/${id}`, { method: "DELETE" });
	}

	private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
		const url = `${this.baseUrl}${path}`;
		const res = await this.fetchFn(url, {
			...init,
			headers: { ...this.defaultHeaders, ...(init.headers ?? {}) },
		});

		if (!res.ok) {
			const text = await safeReadText(res);
			const error: any = new Error(
				`HTTP ${res.status} ${res.statusText} for ${url}${text ? `: ${text}` : ""}`
			);
			error.status = res.status;
			error.body = text;
			throw error;
		}

		if (res.status === 204) return undefined as unknown as T;

		const contentType = res.headers.get("content-type") || "";
		if (contentType.includes("application/json")) {
			return (await res.json()) as T;
		}
		// Fallback: try text
		return (await (res.text() as unknown as Promise<T>)) as T;
	}
}

// Factory with sane defaults
export function createServicesClient(options?: ServicesClientOptions) {
	return new ServicesClient(options);
}

// Helpers
function safeReadText(res: Response): Promise<string | null> {
	try {
		return res.text();
	} catch {
		return Promise.resolve(null);
	}
}

function reviveDate(value: unknown): Date | null {
	if (value == null) return null;
	if (value instanceof Date) return value;
	const s = String(value);
	// Basic ISO date detection
	if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(s)) {
		const d = new Date(s);
		return isNaN(d.getTime()) ? null : d;
	}
	return null;
}

function reviveServiceManifestDates(m: ServiceManifest): ServiceManifest {
	return {
		...m,
		createdAt: reviveDate(m.createdAt) ?? null,
		updatedAt: reviveDate(m.updatedAt) ?? null,
	};
}

function serializeDates<T extends Record<string, any>>(obj: T): T {
	// Convert Date instances to ISO strings, leave others as-is
	const out: Record<string, any> = Array.isArray(obj) ? [] : {};
	for (const [k, v] of Object.entries(obj)) {
		if (v instanceof Date) out[k] = v.toISOString();
		else if (Array.isArray(v)) out[k] = v.map((x) => serializeDates(x as any));
		else if (v && typeof v === "object") out[k] = serializeDates(v as any);
		else out[k] = v;
	}
	return out as T;
}

export default ServicesClient;