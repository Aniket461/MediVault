import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, ElementRef, HostListener, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AccountService } from './account.service';
import { DoctorOption, MedicalRecord, Opinion } from './care.models';
import { CareService } from './care.service';
import { Icon } from './icon';
import { NoticeInbox } from './notice-inbox';
import { FilePreview } from './preview';

@Component({
  selector: 'app-patient',
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, Icon, RouterLink, FilePreview],
  templateUrl: './patient.page.html',
})
export class PatientPage {
  private readonly care = inject(CareService);
  private readonly inbox = inject(NoticeInbox);
  readonly accounts = inject(AccountService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly records = signal<MedicalRecord[]>([]);
  readonly shareable = computed(() => this.records().filter((record) => !record.archived));
  readonly doctors = signal<DoctorOption[]>([]);
  readonly requests = signal<Opinion[]>([]);
  readonly message = signal('');
  readonly askError = signal('');
  readonly asking = signal(false);
  readonly sending = signal(false);
  readonly selectedIds = signal<string[]>([]);
  readonly selectedRequest = signal<Opinion | null>(null);
  readonly paying = signal<Opinion | null>(null);
  readonly payNotice = signal<'success' | 'failure' | ''>('');
  readonly payError = signal('');
  readonly pendingDelete = signal<Opinion | null>(null);
  readonly deleting = signal(false);
  readonly deleteError = signal('');
  private readonly paypalHost = viewChild<ElementRef<HTMLElement>>('paypalHost');
  private paypalMountedFor: string | null = null;
  private paymentSettled = false;
  readonly symptoms = signal<string[]>([]);
  readonly symptomChoices = [
    'Chest pain',
    'Shortness of breath',
    'Cough',
    'Fever',
    'Headache',
    'Dizziness',
    'Fatigue',
    'Nausea',
    'Abdominal pain',
    'Back pain',
    'Joint pain',
    'Rash',
    'Swelling',
    'Palpitations',
    'Weight change',
    'Sleep trouble',
    'Low mood',
    'Numbness or tingling',
    'Vision change',
    'Other',
  ];
  readonly askForm = this.formBuilder.nonNullable.group({
    doctorId: ['', Validators.required],
    feeling: ['', Validators.required],
    duration: ['', Validators.required],
    intensity: ['', Validators.required],
    consulted: ['', Validators.required],
    consultedNote: [''],
    message: ['', Validators.required],
  });

  constructor() {
    this.reload();
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      if (!params.has('ask')) {
        return;
      }

      this.openAsk();
      void this.router.navigate([], { queryParams: { ask: null }, queryParamsHandling: 'merge', replaceUrl: true });
    });
    effect(() => {
      const host = this.paypalHost();
      const paying = this.paying();
      const selected = this.selectedRequest();
      const request = paying ?? (selected?.status === 'AwaitingPayment' ? selected : null);
      if (!host || !request) {
        return;
      }
      void this.mountPayPal(host.nativeElement, request.id);
    });
  }

  openAsk(): void {
    this.askError.set('');
    this.selectedIds.set([]);
    this.symptoms.set([]);
    this.askForm.reset({
      doctorId: '',
      feeling: '',
      duration: '',
      intensity: '',
      consulted: '',
      consultedNote: '',
      message: '',
    });
    this.asking.set(true);
  }

  closeAsk(): void {
    this.asking.set(false);
  }

  toggleFile(id: string): void {
    this.selectedIds.update((ids) => (ids.includes(id) ? ids.filter((item) => item !== id) : [...ids, id]));
  }

  toggleSymptom(name: string): void {
    this.symptoms.update((items) => (items.includes(name) ? items.filter((item) => item !== name) : [...items, name]));
  }

  selectedDoctor(): DoctorOption | undefined {
    return this.doctors().find((doctor) => doctor.id === this.askForm.controls.doctorId.value);
  }

  sendAsk(): void {
    const raw = this.askForm.getRawValue();
    const consulted = raw.consulted;
    const note = raw.consultedNote.trim();
    if (!raw.doctorId || this.selectedIds().length === 0 || this.symptoms().length === 0) {
      this.askError.set('Choose a doctor, at least one file, and the symptoms that fit.');
      return;
    }

    if (!raw.feeling.trim() || !raw.duration || !raw.intensity || !consulted || !raw.message.trim()) {
      this.askError.set('Fill in how this feels, how long, how strong, and what you want the doctor to know.');
      return;
    }

    if (consulted !== 'No' && !note) {
      this.askError.set('Write what you were told.');
      return;
    }

    this.askError.set('');
    this.sending.set(true);
    this.care.createOpinion({
      doctorId: raw.doctorId,
      recordIds: this.selectedIds(),
      message: raw.message.trim(),
      symptoms: this.symptoms(),
      feeling: raw.feeling.trim(),
      duration: raw.duration,
      intensity: raw.intensity,
      consulted,
      consultedNote: consulted === 'No' ? '' : note,
    }).subscribe({
      next: (created) => {
        this.sending.set(false);
        this.asking.set(false);
        this.payError.set('');
        this.paying.set(created);
        this.requests.update((list) => [created, ...list.filter((item) => item.id !== created.id)]);
        this.care.listRecords().subscribe({
          next: (records) => this.records.set(records),
        });
      },
      error: (error: HttpErrorResponse) => {
        this.sending.set(false);
        this.askError.set(error.error?.message ?? 'Could not send that request.');
      },
    });
  }

  openRequest(request: Opinion): void {
    this.selectedRequest.set(request);
  }

  closeRequest(): void {
    this.paypalMountedFor = null;
    this.payError.set('');
    this.selectedRequest.set(null);
  }

  closePayment(): void {
    this.paypalMountedFor = null;
    this.payError.set('');
    this.paying.set(null);
  }

  closeNotice(): void {
    this.payNotice.set('');
  }

  askDelete(request: Opinion, event?: Event): void {
    event?.stopPropagation();
    if (request.status !== 'AwaitingPayment') {
      return;
    }

    this.deleteError.set('');
    this.pendingDelete.set(request);
  }

  cancelDelete(): void {
    this.deleteError.set('');
    this.pendingDelete.set(null);
  }

  confirmDelete(): void {
    const request = this.pendingDelete();
    if (!request || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    this.deleteError.set('');
    this.care.deleteOpinion(request.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.pendingDelete.set(null);
        this.requests.update((list) => list.filter((item) => item.id !== request.id));
        if (this.paying()?.id === request.id) {
          this.closePayment();
        }
        if (this.selectedRequest()?.id === request.id) {
          this.closeRequest();
        }
        this.care.listRecords().subscribe({
          next: (records) => this.records.set(records),
        });
      },
      error: (error: HttpErrorResponse) => {
        this.deleting.set(false);
        this.deleteError.set(error.error?.message ?? 'Could not delete that request.');
      },
    });
  }

  @HostListener('document:keydown.escape')
  dismissPayment(): void {
    if (this.pendingDelete()) {
      this.cancelDelete();
      return;
    }

    if (this.payNotice()) {
      this.closeNotice();
      return;
    }

    if (this.paying()) {
      this.closePayment();
    }
  }

  openRequestFile(request: Opinion, fileId: string): void {
    this.care.openOpinionFile(request.id, fileId).subscribe({
      next: (blob) => showFile(blob),
      error: () => this.message.set('Could not open that file.'),
    });
  }

  private async mountPayPal(host: HTMLElement, requestId: string): Promise<void> {
    if (this.paypalMountedFor === requestId) {
      return;
    }

    this.paypalMountedFor = requestId;
    this.paymentSettled = false;
    this.payError.set('');
    host.replaceChildren();
    try {
      const config = await firstValueFrom(this.care.paypalConfig());
      if (!config.enabled) {
        this.payError.set('PayPal is not configured.');
        this.paypalMountedFor = null;
        return;
      }

      await loadPayPal(config.clientId, config.currency);
      const paypal = (window as Window & { paypal?: PayPalButtons }).paypal;
      if (!paypal) {
        throw new Error('PayPal did not load.');
      }

      await paypal.Buttons({
        createOrder: async () => {
          const order = await firstValueFrom(this.care.createPayPalOrder(requestId));
          return order.orderId;
        },
        onApprove: async (data) => {
          try {
            const updated = await firstValueFrom(this.care.capturePayPal(requestId, data.orderID));
            this.requests.update((list) => list.map((item) => (item.id === updated.id ? updated : item)));
            this.finishPayment('success');
          } catch {
            this.finishPayment('failure');
          }
        },
        onCancel: () => undefined,
        onError: () => this.finishPayment('failure'),
      }).render(host);
    } catch {
      this.paypalMountedFor = null;
      this.payError.set('PayPal could not be loaded.');
    }
  }

  statusLabel(request: Opinion): string {
    if (request.status === 'AwaitingPayment') return 'Awaiting payment';
    if (request.status === 'Refunded') return request.refundCause === 'Declined' ? 'Declined' : 'Refunded';
    return request.status;
  }

  tone(status: Opinion['status']): string {
    if (status === 'Answered') return 'pill good';
    if (status === 'Refunded') return 'pill bad';
    return 'pill pending';
  }

  private finishPayment(result: 'success' | 'failure'): void {
    if (this.paymentSettled) {
      return;
    }

    this.paymentSettled = true;
    this.paypalMountedFor = null;
    this.payError.set('');
    this.paying.set(null);
    this.selectedRequest.set(null);
    this.payNotice.set(result);
  }

  private reload(): void {
    this.care.listRecords().subscribe({
      next: (records) => this.records.set(records),
      error: () => this.message.set('Could not load records.'),
    });
    this.care.listDoctors().subscribe({
      next: (doctors) => this.doctors.set(doctors),
    });
    this.care.listOpinions().subscribe({
      next: (requests) => {
        this.requests.set(requests);
        this.inbox.replace(requests);
      },
    });
  }
}

interface PayPalButtons {
  Buttons(options: {
    createOrder: () => Promise<string>;
    onApprove: (data: { orderID: string }) => Promise<void>;
    onCancel?: () => void;
    onError: () => void;
  }): { render: (element: HTMLElement) => Promise<void> };
}

function loadPayPal(clientId: string, currency: string): Promise<void> {
  const paypalWindow = window as Window & { paypal?: PayPalButtons };
  if (paypalWindow.paypal) {
    return Promise.resolve();
  }

  return new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = `https://www.paypal.com/sdk/js?client-id=${encodeURIComponent(clientId)}&currency=${encodeURIComponent(currency)}&intent=capture`;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error('PayPal did not load.'));
    document.head.appendChild(script);
  });
}

function showFile(blob: Blob): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.target = '_blank';
  link.click();
}
