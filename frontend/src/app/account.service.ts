import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { AccountProfile } from './account.models';
import { User } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);

  readonly profile = signal<AccountProfile | null>(null);
  readonly error = signal('');

  openSelf(): void {
    this.load('/api/account/me');
  }

  open(id: string): void {
    this.load(`/api/account/${id}`);
  }

  close(): void {
    this.profile.set(null);
    this.error.set('');
  }

  patchSelf(user: User): void {
    const current = this.profile();
    if (!current?.self || current.id !== user.id) {
      return;
    }

    this.profile.set({
      ...current,
      fullName: user.fullName,
      email: user.email,
      phone: user.phone,
      address: user.address,
      hospital: user.hospital,
      payPalEmail: user.payPalEmail,
      secondOpinionFee: user.secondOpinionFee,
    });
  }

  private load(url: string): void {
    this.error.set('');
    this.http.get<AccountProfile>(url).subscribe({
      next: (profile) => this.profile.set(profile),
      error: () => this.error.set('Could not open that account.'),
    });
  }
}
