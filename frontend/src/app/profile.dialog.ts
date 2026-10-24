import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { AccountProfile } from './account.models';
import { AccountService } from './account.service';
import { AuthService } from './auth.service';
import { Icon } from './icon';

@Component({
  selector: 'app-profile',
  imports: [DecimalPipe, Icon, ReactiveFormsModule],
  templateUrl: './profile.dialog.html',
})
export class ProfileDialog {
  readonly accounts = inject(AccountService);
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly message = signal('');
  readonly form = this.formBuilder.nonNullable.group({
    fullName: '',
    email: '',
    phone: '',
    address: '',
    hospital: '',
    payPalEmail: '',
    secondOpinionFee: '',
  });
  private filledFor = '';

  constructor() {
    effect(() => {
      const profile = this.accounts.profile();
      const key = profile?.id ?? '';
      const switched = key !== this.filledFor;
      if (switched) {
        this.filledFor = key;
        this.editing.set(false);
        this.message.set('');
      }

      if (profile?.self && (switched || !this.editing())) {
        this.fill(profile);
      }
    });
  }

  startEdit(): void {
    const profile = this.accounts.profile();
    if (!profile?.self) {
      return;
    }

    this.fill(profile);
    this.message.set('');
    this.editing.set(true);
  }

  cancelEdit(): void {
    const profile = this.accounts.profile();
    if (profile?.self) {
      this.fill(profile);
    }

    this.message.set('');
    this.editing.set(false);
  }

  save(): void {
    const profile = this.accounts.profile();
    if (!profile?.self) {
      return;
    }

    const fullName = this.form.controls.fullName.value.trim();
    const email = this.form.controls.email.value.trim();
    const phone = this.form.controls.phone.value.replace(/\s/g, '');
    const address = this.form.controls.address.value.trim();
    const hospital = this.form.controls.hospital.value.trim();
    const payPalEmail = this.form.controls.payPalEmail.value.trim();
    const feeText = this.form.controls.secondOpinionFee.value.trim();
    const fee = Number(feeText);

    if (!fullName) {
      this.message.set('Enter your name.');
      return;
    }

    if (!email.includes('@')) {
      this.message.set('Enter a valid email.');
      return;
    }

    if (profile.role !== 'Admin' && (phone.length < 7 || phone.length > 20)) {
      this.message.set('Enter a valid phone number.');
      return;
    }

    if (profile.role !== 'Admin' && (address.length < 5 || address.length > 300)) {
      this.message.set('Enter your address.');
      return;
    }

    if (profile.role === 'Doctor') {
      if (!hospital) {
        this.message.set('Enter the hospital you work with.');
        return;
      }

      if (!payPalEmail.includes('@')) {
        this.message.set('Enter the PayPal email that should receive your fee.');
        return;
      }

      if (!Number.isFinite(fee) || fee <= 0 || fee > 100000) {
        this.message.set('Enter a second-opinion fee greater than zero.');
        return;
      }
    }

    this.saving.set(true);
    this.message.set('');
    this.auth.updateProfile({
      fullName,
      email,
      phone,
      address,
      hospital: profile.role === 'Doctor' ? hospital : null,
      payPalEmail: profile.role === 'Doctor' ? payPalEmail : null,
      secondOpinionFee: profile.role === 'Doctor' ? Math.round(fee * 100) / 100 : null,
    }).subscribe({
      next: (user) => {
        this.accounts.patchSelf(user);
        this.editing.set(false);
        this.saving.set(false);
        this.message.set('Details saved.');
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.message.set(error.error?.message ?? 'Could not save those details.');
      },
    });
  }

  @HostListener('document:keydown.escape')
  close(): void {
    this.accounts.close();
  }

  private fill(profile: AccountProfile): void {
    this.form.setValue({
      fullName: profile.fullName,
      email: profile.email ?? '',
      phone: profile.phone ?? '',
      address: profile.address ?? '',
      hospital: profile.hospital ?? '',
      payPalEmail: profile.payPalEmail ?? '',
      secondOpinionFee: profile.secondOpinionFee == null ? '' : profile.secondOpinionFee.toFixed(2),
    });
  }
}
