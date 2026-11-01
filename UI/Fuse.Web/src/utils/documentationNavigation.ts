/** Only the local documentation catalogue can be used as an entity's return destination. */
export function validateDocumentationOrigin(value: unknown): string | undefined {
  return typeof value === 'string' && (value === '/view' || value.startsWith('/view?')) ? value : undefined
}
