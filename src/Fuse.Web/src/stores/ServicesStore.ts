import { defineStore } from "pinia";
import { Client  } from "../httpClients/client.gen";
import type { ServiceManifest, CreateServiceCommand } from "../httpClients/client.gen";

const servicesClient = new Client();

export const useServicesStore = defineStore("services", {
    state: () => ({
        services: [] as Array<ServiceManifest>,
        loading: false,
        error: null as string | null,
    }),
    actions: {
        async loadAll() {
      this.loading = true; this.error = null;
      try {
        this.services = await servicesClient.servicesAll();
      } catch (e: any) {
        this.error = e?.message ?? 'Failed to load services';
      } finally {
        this.loading = false;
      }
    },
    async loadOne(id: string) {
      this.loading = true; this.error = null;
      try {
        const item = await servicesClient.servicesGET(id);
        return item; // could be null on 404
      } catch (e: any) {
        this.error = e?.message ?? 'Failed to load service';
        return null;
      } finally {
        this.loading = false;
      }
    },
    async create(payload: CreateServiceCommand) {
      this.loading = true; this.error = null;
      try {
        const created = await servicesClient.servicesPOST(payload);
        this.services.push(created);
        return created;
      } catch (e: any) {
        this.error = e?.message ?? 'Failed to create service';
        throw e;
      } finally {
        this.loading = false;
      }
    },
    async update(manifest: ServiceManifest) {
      this.loading = true; this.error = null;
      const id = manifest.id;
      if (!id) {
        this.error = 'Service ID is required for update';
        this.loading = false;
        throw new Error(this.error);
      }
      try {
        await servicesClient.servicesPUT(id, manifest);
        const idx = this.services.findIndex(x => x.id === manifest.id);
        if (idx >= 0) this.services[idx] = manifest;
      } catch (e: any) {
        this.error = e?.message ?? 'Failed to update service';
        throw e;
      } finally {
        this.loading = false;
      }
    },
    async remove(id: string) {
      this.loading = true; this.error = null;
      try {
        await servicesClient.servicesDELETE(id);
        this.services = this.services.filter(x => x.id !== id);
      } catch (e: any) {
        this.error = e?.message ?? 'Failed to delete service';
        throw e;
      } finally {
        this.loading = false;
      }
    }
    }
});
