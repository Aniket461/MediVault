import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { tap } from 'rxjs';
import { AuthResponse, MessageResponse, User, UserDetail } from './auth.models';

const tokenKey = 'mv_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  readonly user = signal<User | null>(null);

  token(): string | null {
    return localStorage.getItem(tokenKey);
  }

  register(body: FormData) {
    return this.http.post<MessageResponse>('/api/auth/register', body);
  }

  login(email: string, password: string) {
    return this.http.post<AuthResponse>('/api/auth/login', { email, password }).pipe(
      tap((response) => {
        localStorage.setItem(tokenKey, response.token);
        this.user.set(response.user);
      }),
    );
  }

  loadMe() {
    return this.http.get<User>('/api/auth/me').pipe(tap((user) => this.user.set(user)));
  }

  setPayPalEmail(payPalEmail: string) {
    return this.http.put<User>('/api/auth/paypal-email', { payPalEmail }).pipe(tap((user) => this.user.set(user)));
  }

  updateProfile(body: {
    fullName: string;
    email: string;
    phone: string;
    address: string;
    hospital: string | null;
    payPalEmail: string | null;
    secondOpinionFee: number | null;
  }) {
    return this.http.put<User>('/api/auth/profile', body).pipe(tap((user) => this.user.set(user)));
  }

  listUsers() {
    return this.http.get<User[]>('/api/admin/users');
  }

  getUser(id: string) {
    return this.http.get<UserDetail>(`/api/admin/users/${id}`);
  }

  document(userId: string, documentId: string) {
    return this.http.get(`/api/admin/users/${userId}/documents/${documentId}`, { responseType: 'blob' });
  }

  setStatus(id: string, status: 'approve' | 'reject') {
    return this.http.post<User>(`/api/admin/users/${id}/${status}`, {});
  }

  logout(): void {
    localStorage.removeItem(tokenKey);
    this.user.set(null);
  }
}
