import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, inject, signal } from '@angular/core';
import { User, UserDetail } from './auth.models';
import { AuthService } from './auth.service';
import { Icon } from './icon';

@Component({
  selector: 'app-admin',
  imports: [DecimalPipe, Icon],
  templateUrl: './admin.page.html',
})
export class AdminPage {
  private readonly auth = inject(AuthService);

  readonly users = signal<User[]>([]);
  readonly selected = signal<UserDetail | null>(null);
  readonly photos = signal<{ id: string; url: string; name: string }[]>([]);
  readonly message = signal('');
  readonly busyId = signal<string | null>(null);

  constructor() {
    this.reload();
  }

  @HostListener('document:keydown.escape')
  close(): void {
    for (const photo of this.photos()) {
      URL.revokeObjectURL(photo.url);
    }
    this.photos.set([]);
    this.selected.set(null);
  }

  open(user: User): void {
    this.close();
    this.auth.getUser(user.id).subscribe({
      next: (detail) => {
        this.selected.set(detail);
        for (const document of detail.documents) {
          this.auth.document(detail.id, document.id).subscribe({
            next: (blob) => {
              if (this.selected()?.id !== detail.id) {
                return;
              }
              const url = URL.createObjectURL(blob);
              this.photos.update((list) => [...list, { id: document.id, url, name: document.fileName }]);
            },
          });
        }
      },
      error: (error: HttpErrorResponse) => {
        this.message.set(error.error?.message ?? 'Could not open that account.');
      },
    });
  }

  decide(status: 'approve' | 'reject'): void {
    const person = this.selected();
    if (!person) {
      return;
    }

    this.busyId.set(person.id);
    this.message.set('');
    this.auth.setStatus(person.id, status).subscribe({
      next: (updated) => {
        this.users.update((list) => list.map((user) => (user.id === updated.id ? updated : user)));
        this.selected.update((current) =>
          current && current.id === updated.id ? { ...current, status: updated.status } : current,
        );
        this.busyId.set(null);
      },
      error: (error: HttpErrorResponse) => {
        this.busyId.set(null);
        this.message.set(error.error?.message ?? 'Could not update that account.');
      },
    });
  }

  private reload(): void {
    this.auth.listUsers().subscribe({
      next: (users) => {
        this.users.set(users);
        this.busyId.set(null);
      },
      error: (error: HttpErrorResponse) => {
        this.message.set(error.error?.message ?? 'Could not load accounts.');
      },
    });
  }
}
