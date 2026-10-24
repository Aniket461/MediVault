import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MedicalRecord } from './care.models';
import { CareService } from './care.service';
import { Icon } from './icon';
import { FilePreview } from './preview';

@Component({
  selector: 'app-records',
  imports: [ReactiveFormsModule, DatePipe, Icon, FilePreview],
  templateUrl: './records.page.html',
})
export class RecordsPage {
  private readonly care = inject(CareService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('filePicker');

  readonly records = signal<MedicalRecord[]>([]);
  readonly message = signal('');
  readonly uploading = signal(false);
  readonly showUpload = signal(false);
  readonly file = signal<File | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly pendingDelete = signal<MedicalRecord | null>(null);
  readonly pendingArchive = signal<MedicalRecord | null>(null);
  readonly form = this.formBuilder.nonNullable.group({
    description: ['', Validators.required],
  });
  readonly editForm = this.formBuilder.nonNullable.group({
    description: ['', Validators.required],
  });

  constructor() {
    this.reload();
  }

  toggleUpload(): void {
    this.showUpload.update((open) => !open);
    this.message.set('');
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
  }

  upload(): void {
    const description = this.form.controls.description.value.trim();
    const file = this.file();
    if (!description || !file) {
      this.message.set('Add a description and a file.');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      this.message.set('Upload a PDF or image under 5 MB.');
      return;
    }

    const body = new FormData();
    body.append('description', description);
    body.append('file', file);
    this.uploading.set(true);
    this.message.set('');
    this.care.upload(body).subscribe({
      next: () => {
        this.uploading.set(false);
        this.file.set(null);
        const input = this.fileInput()?.nativeElement;
        if (input) {
          input.value = '';
        }
        this.form.reset({ description: '' });
        this.showUpload.set(false);
        this.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.uploading.set(false);
        this.message.set(error.error?.message ?? 'Could not upload that file.');
      },
    });
  }

  openFile(record: MedicalRecord): void {
    this.care.openRecord(record.id).subscribe({
      next: (blob) => showFile(blob),
      error: () => this.message.set('Could not open that file.'),
    });
  }

  startEdit(record: MedicalRecord): void {
    this.editingId.set(record.id);
    this.editForm.setValue({ description: record.description });
    this.message.set('');
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  saveEdit(record: MedicalRecord): void {
    const description = this.editForm.controls.description.value.trim();
    if (!description) {
      this.message.set('Describe this file.');
      return;
    }

    this.care.updateRecord(record.id, description).subscribe({
      next: (updated) => {
        this.editingId.set(null);
        this.records.update((list) => list.map((item) => (item.id === updated.id ? updated : item)));
      },
      error: (error: HttpErrorResponse) => {
        this.message.set(error.error?.message ?? 'Could not save that description.');
      },
    });
  }

  askDelete(record: MedicalRecord): void {
    this.pendingDelete.set(record);
  }

  cancelDelete(): void {
    this.pendingDelete.set(null);
  }

  confirmDelete(): void {
    const record = this.pendingDelete();
    if (!record) {
      return;
    }

    this.care.deleteRecord(record.id).subscribe({
      next: () => {
        this.pendingDelete.set(null);
        this.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.pendingDelete.set(null);
        this.message.set(error.error?.message ?? 'Could not delete that file.');
      },
    });
  }

  askArchive(record: MedicalRecord): void {
    this.pendingArchive.set(record);
  }

  cancelArchive(): void {
    this.pendingArchive.set(null);
  }

  confirmArchive(): void {
    const record = this.pendingArchive();
    if (!record) {
      return;
    }

    this.care.archiveRecord(record.id).subscribe({
      next: (updated) => {
        this.pendingArchive.set(null);
        this.records.update((list) => list.map((item) => (item.id === updated.id ? updated : item)));
      },
      error: (error: HttpErrorResponse) => {
        this.pendingArchive.set(null);
        this.message.set(error.error?.message ?? 'Could not archive that file.');
      },
    });
  }

  restore(record: MedicalRecord): void {
    this.care.restoreRecord(record.id).subscribe({
      next: (updated) => {
        this.records.update((list) => list.map((item) => (item.id === updated.id ? updated : item)));
      },
      error: (error: HttpErrorResponse) => {
        this.message.set(error.error?.message ?? 'Could not restore that file.');
      },
    });
  }

  private reload(): void {
    this.care.listRecords().subscribe({
      next: (records) => this.records.set(records),
      error: () => this.message.set('Could not load records.'),
    });
  }
}

function showFile(blob: Blob): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.target = '_blank';
  link.click();
}
