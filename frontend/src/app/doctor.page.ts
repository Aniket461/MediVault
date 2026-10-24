import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AccountService } from './account.service';
import { Opinion } from './care.models';
import { CareService } from './care.service';
import { Icon } from './icon';
import { NoticeInbox } from './notice-inbox';
import { FilePreview } from './preview';

@Component({
  selector: 'app-doctor',
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, Icon, FilePreview],
  templateUrl: './doctor.page.html',
})
export class DoctorPage {
  private readonly care = inject(CareService);
  private readonly inbox = inject(NoticeInbox);
  readonly accounts = inject(AccountService);
  private readonly formBuilder = inject(FormBuilder);

  readonly requests = signal<Opinion[]>([]);
  readonly selected = signal<Opinion | null>(null);
  readonly message = signal('');
  readonly saving = signal(false);
  readonly confirmingDecline = signal(false);
  readonly aidLoading = signal(false);
  readonly aidUnavailable = signal(false);
  readonly aidError = signal('');
  readonly form = this.formBuilder.nonNullable.group({
    opinion: ['', Validators.required],
  });

  constructor() {
    this.reload();
  }

  open(request: Opinion): void {
    this.message.set('');
    this.confirmingDecline.set(false);
    this.aidUnavailable.set(false);
    this.aidError.set('');
    this.aidLoading.set(false);
    this.form.reset({ opinion: request.opinion ?? '' });
    this.selected.set(request);
    if ((request.status === 'Open' || request.status === 'Answered') && !request.clinicalAid) {
      this.aidLoading.set(true);
      this.care.prepareAid(request.id).subscribe({
        next: (updated) => {
          this.aidLoading.set(false);
          this.selected.set(updated);
          this.requests.update((list) => list.map((item) => (item.id === updated.id ? updated : item)));
        },
        error: () => {
          this.aidLoading.set(false);
          this.aidUnavailable.set(true);
        },
      });
    }
  }

  refreshAid(): void {
    const request = this.selected();
    if (!request || this.aidLoading()) {
      return;
    }

    this.aidLoading.set(true);
    this.aidError.set('');
    this.aidUnavailable.set(false);
    this.care.prepareAid(request.id, true).subscribe({
      next: (updated) => {
        this.aidLoading.set(false);
        this.selected.set(updated);
        this.requests.update((list) => list.map((item) => (item.id === updated.id ? updated : item)));
      },
      error: (error: HttpErrorResponse) => {
        this.aidLoading.set(false);
        if (request.clinicalAid) {
          this.aidError.set(error.error?.message ?? 'Could not update the reading.');
        } else {
          this.aidUnavailable.set(true);
        }
      },
    });
  }

  close(): void {
    this.confirmingDecline.set(false);
    this.aidLoading.set(false);
    this.selected.set(null);
  }

  openFile(request: Opinion, fileId: string): void {
    this.care.openOpinionFile(request.id, fileId).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.target = '_blank';
        link.click();
      },
      error: () => this.message.set('Could not open that file.'),
    });
  }

  send(): void {
    const request = this.selected();
    const opinion = this.form.controls.opinion.value.trim();
    if (!request || !opinion) {
      this.message.set('Write your opinion.');
      return;
    }

    this.saving.set(true);
    this.message.set('');
    this.care.answer(request.id, opinion).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.selected.set(updated);
        this.requests.update((list) => list.map((item) => (item.id === updated.id ? updated : item)));
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.message.set(error.error?.message ?? 'Could not save the opinion.');
      },
    });
  }

  decline(): void {
    const request = this.selected();
    if (!request) {
      return;
    }

    if (!this.confirmingDecline()) {
      this.confirmingDecline.set(true);
      this.message.set('');
      return;
    }

    this.saving.set(true);
    this.message.set('');
    this.care.decline(request.id).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.confirmingDecline.set(false);
        this.selected.set(updated);
        this.requests.update((list) => list.map((item) => (item.id === updated.id ? updated : item)));
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.message.set(error.error?.message ?? 'Could not decline the request.');
      },
    });
  }

  statusLabel(request: Opinion): string {
    if (request.status === 'Refunded') return request.refundCause === 'Declined' ? 'Declined' : 'Cancelled';
    return request.status;
  }

  tone(status: Opinion['status']): string {
    if (status === 'Answered') return 'pill good';
    if (status === 'Refunded') return 'pill bad';
    return 'pill pending';
  }

  private reload(): void {
    this.care.listOpinions().subscribe({
      next: (requests) => {
        this.requests.set(requests);
        this.inbox.replace(requests);
      },
      error: () => this.message.set('Could not load requests.'),
    });
  }
}
