import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from './auth.service';
import { Icon } from './icon';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, Icon],
  templateUrl: './register.page.html',
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('filePicker');

  readonly message = signal('');
  readonly error = signal('');
  readonly submitting = signal(false);
  readonly files = signal<File[]>([]);
  readonly form = this.formBuilder.nonNullable.group({
    fullName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    phone: ['', Validators.required],
    address: ['', Validators.required],
    role: this.formBuilder.nonNullable.control<'Patient' | 'Doctor'>('Patient'),
    hospital: [''],
    payPalEmail: [''],
    secondOpinionFee: [''],
  });

  onFiles(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.files.set(Array.from(input.files ?? []));
  }

  submit(): void {
    const raw = this.form.getRawValue();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const phone = raw.phone.replace(/\s/g, '');
    if (phone.length < 7 || phone.length > 20 || /[^\d+]/.test(phone)) {
      this.error.set('Enter a valid phone number.');
      return;
    }

    if (raw.address.trim().length < 5) {
      this.error.set('Enter your address.');
      return;
    }

    const fee = raw.role === 'Doctor' ? Number(raw.secondOpinionFee) : null;
    if (raw.role === 'Doctor' && !raw.hospital.trim()) {
      this.error.set('Enter the hospital you work with.');
      return;
    }

    if (raw.role === 'Doctor' && !raw.payPalEmail.includes('@')) {
      this.error.set('Enter the PayPal email that should receive your fee.');
      return;
    }

    if (raw.role === 'Doctor' && (!fee || fee <= 0)) {
      this.error.set('Enter a second-opinion fee greater than zero.');
      return;
    }

    if (raw.role === 'Doctor' && (this.files().length < 1 || this.files().length > 4)) {
      this.error.set('Add one to four identification images.');
      return;
    }

    const body = new FormData();
    body.append('fullName', raw.fullName.trim());
    body.append('email', raw.email.trim());
    body.append('password', raw.password);
    body.append('phone', phone);
    body.append('address', raw.address.trim());
    body.append('role', raw.role);
    if (raw.role === 'Doctor') {
      body.append('hospital', raw.hospital.trim());
      body.append('payPalEmail', raw.payPalEmail.trim());
      body.append('secondOpinionFee', String(fee));
      for (const file of this.files()) {
        body.append('documents', file);
      }
    }

    this.submitting.set(true);
    this.message.set('');
    this.error.set('');
    this.auth.register(body).subscribe({
      next: (response) => {
        this.submitting.set(false);
        this.message.set(response.message);
        this.files.set([]);
        const input = this.fileInput()?.nativeElement;
        if (input) {
          input.value = '';
        }
        this.form.reset({
          fullName: '',
          email: '',
          password: '',
          phone: '',
          address: '',
          role: 'Patient',
          hospital: '',
          payPalEmail: '',
          secondOpinionFee: '',
        });
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.error.set(error.error?.message ?? 'Could not create the account.');
      },
    });
  }
}
