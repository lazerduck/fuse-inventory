export const providerNames: Record<string, string> = {
  sql: "SQL Server",
  postgresql: "PostgreSQL",
  mongodb: "MongoDB",
  redis: "Redis",
  rabbitmq: "RabbitMQ",
  keycloak: "Keycloak",
  servicebus: "Azure Service Bus",
  cosmosdb: "Azure Cosmos DB",
};
export const reviewLabels: Record<string, string> = {
  Matched: "Matches expectation",
  Drift: "Drift detected",
  Unknown: "Unknown coverage",
};
export function reviewError(error: unknown): string {
  if (
    error &&
    typeof error === "object" &&
    "response" in error &&
    typeof error.response === "string"
  ) {
    try {
      const detail = JSON.parse(error.response);
      if (typeof detail.error === "string") return detail.error;
    } catch {
      /* Non-JSON response */
    }
  }
  return error instanceof Error
    ? error.message
    : "The request could not be completed.";
}

export function observationAge(
  checkedAt: Date,
  currentTime = Date.now(),
): string {
  const minutes = Math.max(
    0,
    Math.floor((currentTime - checkedAt.getTime()) / 60000),
  );
  if (minutes === 0) return "just now";
  const formatter = new Intl.RelativeTimeFormat(undefined, {
    numeric: "always",
  });
  if (minutes < 60) return formatter.format(-minutes, "minute");
  if (minutes < 1440)
    return formatter.format(-Math.floor(minutes / 60), "hour");
  return formatter.format(-Math.floor(minutes / 1440), "day");
}
