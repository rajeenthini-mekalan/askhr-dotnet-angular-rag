import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Placeholder until M6. Only reachable with the hr-admin role (roleGuard in app.routes.ts). */
@Component({
  selector: 'app-documents-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Policy documents</h1>
    @if (documents.isLoading()) {
      <p>Loading…</p>
    } @else if (documents.error()) {
      <p class="error">Could not load documents (is the API running?).</p>
    } @else {
      <p data-testid="documents-ok">API call succeeded: {{ documents.value()?.length ?? 0 }} documents.</p>
    }
  `,
})
export class DocumentsPage {
  // httpResource: an HTTP GET exposed as signals (value, isLoading, error). It goes through
  // HttpClient, so the auth interceptor adds the bearer token.
  protected readonly documents = httpResource<unknown[]>(() => '/api/documents');
}
