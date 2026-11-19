export const providerNames: Record<string, string> = {
  sql: 'SQL Server', postgresql: 'PostgreSQL', mongodb: 'MongoDB', redis: 'Redis',
  rabbitmq: 'RabbitMQ', keycloak: 'Keycloak', servicebus: 'Azure Service Bus', cosmosdb: 'Azure Cosmos DB'
}
export const reviewLabels: Record<string, string> = {
  Matched: 'Matches expectation', Drift: 'Drift detected', Unknown: 'Unknown coverage'
}
export function reviewError(error: unknown): string {
  if (error && typeof error === 'object' && 'response' in error && typeof error.response === 'string') {
    try { const detail = JSON.parse(error.response); if (typeof detail.error === 'string') return detail.error } catch { /* Non-JSON response */ }
  }
  return error instanceof Error ? error.message : 'The request could not be completed.'
}
