import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { DoctorOption, MedicalRecord, Opinion, OpinionRequestBody, PayPalConfig } from './care.models';

@Injectable({ providedIn: 'root' })
export class CareService {
  private readonly http = inject(HttpClient);

  listRecords() {
    return this.http.get<MedicalRecord[]>('/api/records');
  }

  upload(body: FormData) {
    return this.http.post<MedicalRecord>('/api/records', body);
  }

  openRecord(id: string) {
    return this.http.get(`/api/records/${id}`, { responseType: 'blob' });
  }

  updateRecord(id: string, description: string) {
    return this.http.put<MedicalRecord>(`/api/records/${id}`, { description });
  }

  archiveRecord(id: string) {
    return this.http.post<MedicalRecord>(`/api/records/${id}/archive`, {});
  }

  restoreRecord(id: string) {
    return this.http.post<MedicalRecord>(`/api/records/${id}/restore`, {});
  }

  deleteRecord(id: string) {
    return this.http.delete(`/api/records/${id}`);
  }

  listDoctors() {
    return this.http.get<DoctorOption[]>('/api/doctors');
  }

  listOpinions() {
    return this.http.get<Opinion[]>('/api/opinions');
  }

  createOpinion(body: OpinionRequestBody) {
    return this.http.post<Opinion>('/api/opinions', body);
  }

  deleteOpinion(id: string) {
    return this.http.delete(`/api/opinions/${id}`);
  }

  acknowledge(id: string) {
    return this.http.post(`/api/opinions/${id}/seen`, {});
  }

  paypalConfig() {
    return this.http.get<PayPalConfig>('/api/paypal/config');
  }

  createPayPalOrder(id: string) {
    return this.http.post<{ orderId: string }>(`/api/opinions/${id}/paypal/order`, {});
  }

  capturePayPal(id: string, orderId: string) {
    return this.http.post<Opinion>(`/api/opinions/${id}/paypal/capture`, { orderId });
  }

  answer(id: string, opinion: string) {
    return this.http.post<Opinion>(`/api/opinions/${id}/opinion`, { opinion });
  }

  decline(id: string) {
    return this.http.post<Opinion>(`/api/opinions/${id}/decline`, {});
  }

  prepareAid(id: string, refresh = false) {
    return this.http.post<Opinion>(`/api/opinions/${id}/aid`, {}, { params: { refresh } });
  }

  openOpinionFile(requestId: string, recordId: string) {
    return this.http.get(`/api/opinions/${requestId}/records/${recordId}`, { responseType: 'blob' });
  }
}
