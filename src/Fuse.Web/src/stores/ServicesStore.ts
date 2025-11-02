import { defineStore } from "pinia";
import type { ServiceManifest } from "../classes/serviceManifest";

export const useServicesStore = defineStore("services", {
    state: () => ({
        services: [] as Array<ServiceManifest>,
    }),
    actions: {
    }
});
