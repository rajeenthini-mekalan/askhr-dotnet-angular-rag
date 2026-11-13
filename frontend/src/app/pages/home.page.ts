import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Welcome to AskHR</h1>
    @if (auth.user(); as user) {
      <p>Signed in as <strong>{{ user.name }}</strong> ({{ user.username }}).</p>
      <p>Roles: {{ user.roles.join(', ') }}</p>
    }
    <p>Ask your HR policy questions here (coming in M5).</p>
  `,
})
export class HomePage {
  protected readonly auth = inject(AuthService);
}
