import { Injectable, computed, inject, signal } from '@angular/core';
import { Opinion } from './care.models';
import { CareService } from './care.service';

@Injectable({ providedIn: 'root' })
export class NoticeInbox {
  private readonly care = inject(CareService);
  private readonly requests = signal<Opinion[]>([]);
  private readonly hidden = signal<ReadonlySet<string>>(new Set());
  private load = 0;

  readonly pending = computed(() => this.requests().filter((request) => request.notice && !this.hidden().has(request.id)));
  readonly count = computed(() => this.pending().length);

  refresh(): void {
    const ticket = ++this.load;
    this.care.listOpinions().subscribe({
      next: (requests) => {
        if (ticket === this.load) {
          this.replace(requests);
        }
      },
    });
  }

  replace(requests: Opinion[]): void {
    const stillPending = new Set(requests.filter((request) => request.notice).map((request) => request.id));
    this.hidden.update((ids) => new Set([...ids].filter((id) => stillPending.has(id))));
    this.requests.set(requests);
  }

  reset(): void {
    this.load++;
    this.requests.set([]);
    this.hidden.set(new Set());
  }

  dismiss(id: string): void {
    this.hidden.update((ids) => new Set(ids).add(id));
    this.care.acknowledge(id).subscribe({
      error: () => {
        this.hidden.update((ids) => {
          const next = new Set(ids);
          next.delete(id);
          return next;
        });
      },
    });
  }
}
